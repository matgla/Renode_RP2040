/**
 * rp2040_xip_ssi.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

// Based on Antmicro GenericSpiFlash licensed as:
//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.SPI.NORFlash;
using Antmicro.Renode.Core.Structure.Registers;
using System;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.SPI
{

    public class W25QXX : ISPIPeripheral, IGPIOReceiver
    {
        public W25QXX(MappedMemory underlyingMemory)
        {
            if (!Misc.IsPowerOfTwo((ulong)underlyingMemory.Size))
            {
                throw new ConstructionException("W25QXX underlaying memory size must be power of 2");
            }
            this.underlyingMemory = underlyingMemory;

            statusRegister = new ByteRegister(this)
                .WithFlag(0, name: "WIP")
                .WithFlag(1, out writeEnable, name: "WE");

            Reset();
        }

        public virtual byte Transmit(byte data)
        {
            this.Log(LogLevel.Noisy, "Transmitting data 0x{0:X}, current state: {1}", data, currentOperation.State);
            switch (currentOperation.State)
            {
                case DecodedOperation.OperationState.RecognizeOperation:
                    RecognizeOperation(data);
                    break;
                case DecodedOperation.OperationState.AccumulateCommandAddressBytes:
                    if (currentOperation.TryAccumulateAddress(data))
                    {
                        currentOperation.State = DecodedOperation.OperationState.HandleCommand;
                    }
                    break;
                case DecodedOperation.OperationState.AccumulateNoDataCommandAddressBytes:
                    if (currentOperation.TryAccumulateAddress(data))
                    {
                        currentOperation.State = DecodedOperation.OperationState.HandleNoDataCommand;
                    }
                    break;
                case DecodedOperation.OperationState.HandleCommand:
                    return HandleCommand(data);

            }
            return 0;
        }

        public virtual void FinishTransmission()
        {
            switch (currentOperation.State)
            {
                case DecodedOperation.OperationState.RecognizeOperation:
                case DecodedOperation.OperationState.AccumulateCommandAddressBytes:
                case DecodedOperation.OperationState.AccumulateNoDataCommandAddressBytes:
                    this.Log(LogLevel.Warning, "Transmission finished in unexpected state: {0}", currentOperation.State);
                    break;
            }

            switch (currentOperation.Operation)
            {
                case DecodedOperation.OperationType.Program:
                case DecodedOperation.OperationType.Erase:
                case DecodedOperation.OperationType.WriteRegister:
                    writeEnable.Value = false;
                    break;
            }
            currentOperation.State = DecodedOperation.OperationState.RecognizeOperation;
            currentOperation = default;
        }

        protected int GetDummyBytes(Command command)
        {
            switch (command)
            {
                case Command.ReleasePowerDown: return 3;
                case Command.ManufacturerId: return 2;
                case Command.ReadUniqueId: return 4;
                case Command.FastRead: return 1;
                case Command.ReadSFDPRegister: return 1;
                case Command.ReadSecurityRegister: return 1;
                case Command.FastReadDualOutput: return 1;
                case Command.FastReadDualIO: return 1;
                case Command.ManufacturerIdDualId: return 1;
                case Command.FastReadQuadOutput: return 2;
                case Command.ManufacturerIdQuadIO: return 3;
                case Command.FastReadQuadIO: return 3;
                case Command.SetBurstWithWrap: return 3;
            }
            return 0;
        }

        private void RecognizeOperation(byte operation)
        {
            currentOperation.Operation = DecodedOperation.OperationType.None;
            currentOperation.State = DecodedOperation.OperationState.HandleCommand;
            currentOperation.DummyBytesRemaining = GetDummyBytes((Command)operation);

            if (continuousReadMode.HasValue && continuousReadMode.Value)
            {
                currentOperation.DummyBytesRemaining = originalCommandDummyBytes;
                currentOperation.Operation = DecodedOperation.OperationType.ReadFast;
                currentOperation.State = DecodedOperation.OperationState.AccumulateCommandAddressBytes;
                currentOperation.AddressLength = 3;
                // determine if still in continuous mode after address completion
                continuousReadMode = false;
                currentOperation.TryAccumulateAddress(operation);
                return;
            }
            switch ((Command)operation)
            {
                case Command.JEDECId:
                    currentOperation.Operation = DecodedOperation.OperationType.ReadID;
                    break;
                case Command.ReadSFDPRegister:
                    currentOperation.Operation = DecodedOperation.OperationType.ReadSerialFlashDiscoveryParameter;
                    currentOperation.State = DecodedOperation.OperationState.AccumulateCommandAddressBytes;
                    currentOperation.AddressLength = 3;
                    break;
                case Command.ReadData:
                case Command.FastRead:
                case Command.FastReadDualOutput:
                case Command.FastReadQuadOutput:
                    currentOperation.Operation = DecodedOperation.OperationType.ReadFast;
                    currentOperation.State = DecodedOperation.OperationState.AccumulateCommandAddressBytes;
                    currentOperation.AddressLength = 3;
                    break;
                case Command.FastReadQuadIO:
                case Command.FastReadDualIO:
                    currentOperation.Operation = DecodedOperation.OperationType.ReadFast;
                    currentOperation.State = DecodedOperation.OperationState.AccumulateCommandAddressBytes;
                    currentOperation.AddressLength = 3;
                    continuousReadMode = false;
                    break;
                case Command.PageProgram:
                    currentOperation.Operation = DecodedOperation.OperationType.Program;
                    currentOperation.AddressLength = 3;
                    currentOperation.State = DecodedOperation.OperationState.AccumulateCommandAddressBytes;
                    break;
                case Command.WriteEnable:
                    writeEnable.Value = true;
                    return;
                case Command.WriteDisable:
                    writeEnable.Value = false;
                    return;
                case Command.SectorErase4K:
                    currentOperation.Operation = DecodedOperation.OperationType.Erase;
                    currentOperation.EraseSize = DecodedOperation.OperationEraseSize.Subsector4K;
                    currentOperation.AddressLength = 3;
                    currentOperation.State = DecodedOperation.OperationState.AccumulateNoDataCommandAddressBytes;
                    break;
                case Command.BlockErase32K:
                    currentOperation.Operation = DecodedOperation.OperationType.Erase;
                    currentOperation.EraseSize = DecodedOperation.OperationEraseSize.Subsector32K;
                    currentOperation.AddressLength = 3;
                    currentOperation.State = DecodedOperation.OperationState.AccumulateNoDataCommandAddressBytes;
                    break;
                case Command.BlockErase64K:
                    currentOperation.Operation = DecodedOperation.OperationType.Erase;
                    currentOperation.EraseSize = DecodedOperation.OperationEraseSize.Sector;
                    currentOperation.AddressLength = 3;
                    currentOperation.State = DecodedOperation.OperationState.AccumulateNoDataCommandAddressBytes;
                    break;
                case Command.BulkErase:
                case Command.ChipErase:
                    EraseChip();
                    break;
                case Command.ReadStatusRegister1:
                    currentOperation.Operation = DecodedOperation.OperationType.ReadRegister;
                    currentOperation.Register = (uint)Register.Status1;
                    break;
                case Command.ReadStatusRegister2:
                    currentOperation.Operation = DecodedOperation.OperationType.ReadRegister;
                    currentOperation.Register = (uint)Register.Status2;
                    break;
                case Command.ReadStatusRegister3:
                    currentOperation.Operation = DecodedOperation.OperationType.ReadRegister;
                    currentOperation.Register = (uint)Register.Status3;
                    break;
                case Command.WriteStatusRegister1:
                    currentOperation.Operation = DecodedOperation.OperationType.WriteRegister;
                    currentOperation.Register = (uint)Register.Status1;
                    break;
                case Command.WriteStatusRegister2:
                    currentOperation.Operation = DecodedOperation.OperationType.WriteRegister;
                    currentOperation.Register = (uint)Register.Status2;
                    break;
                case Command.WriteStatusRegister3:
                    currentOperation.Operation = DecodedOperation.OperationType.WriteRegister;
                    currentOperation.Register = (uint)Register.Status3;
                    break;

                default:
                    this.Log(LogLevel.Warning, "Unhandled operation: 0x{0:X}", operation);
                    return;
            }
            this.Log(LogLevel.Noisy, "Decoded operation: {0}, write enabled: {1}", currentOperation.Operation, writeEnable.Value);
        }

        private void EraseChip()
        {
            this.Log(LogLevel.Noisy, "Whole chip erasing");
            if (lockedRange.HasValue)
            {
                this.Log(LogLevel.Error, "Chip erase blocked due to locked range");
                return;
            }
            underlyingMemory.ZeroAll();
        }
        protected virtual byte ReadFromMemory()
        {
            var position = currentOperation.ExecutionAddress + currentOperation.CommandBytesHandled;
            if (position > underlyingMemory.Size)
            {
                this.Log(LogLevel.Error, "Reading from address 0x{0:X} exceedes its size", position);
                return 0;
            }
            return underlyingMemory.ReadByte(position);
        }

        protected bool TryVerifyWriteToMemory(out long position)
        {
            position = currentOperation.ExecutionAddress + currentOperation.CommandBytesHandled;
            if (position > underlyingMemory.Size)
            {
                this.Log(LogLevel.Error, "Writing to address 0x{0:X} exceedes its size", position);
                return false;
            }
            if (lockedRange.HasValue && lockedRange.Value.Contains(position))
            {
                this.Log(LogLevel.Error, "Writing to address 0x{0:X} is in locked range", position);
                return false;
            }
            return true;
        }
        protected virtual byte WriteMemory(byte data)
        {
            if (!writeEnable.Value)
            {
                this.Log(LogLevel.Error, "Can't write, writing is disabled");
                return 0;
            }
            if (!TryVerifyWriteToMemory(out var position))
            {
                return 0;
            }

            underlyingMemory.WriteByte(position, data);
            return data;
        }

        protected virtual byte HandleCommand(byte data)
        {
            if (currentOperation.DummyBytesRemaining > 0)
            {
                currentOperation.DummyBytesRemaining--;
                if (continuousReadMode.HasValue && continuousReadMode.Value == false)
                {
                    if (((data >> 4) & 0x3) == 0b10)
                    {
                        this.Log(LogLevel.Noisy, "Continuous read mode enabled");
                        originalCommandDummyBytes = currentOperation.DummyBytesRemaining + 1;
                        continuousReadMode = true;
                        return 0;
                    }
                    else
                    {
                        continuousReadMode = null;
                    }
                }
                this.Log(LogLevel.Noisy, "Consuming dummy bytes in 0x{0:x}, left: {1}", currentOperation.Operation, currentOperation.DummyBytesRemaining);
                return 0;
            }

            byte result = 0;
            switch (currentOperation.Operation)
            {
                case DecodedOperation.OperationType.Read:
                case DecodedOperation.OperationType.ReadFast:
                    result = ReadFromMemory();
                    break;
                case DecodedOperation.OperationType.ReadID:
                    this.Log(LogLevel.Info, "TODO: implement READ ID");
                    break;
                case DecodedOperation.OperationType.ReadSerialFlashDiscoveryParameter:
                    this.Log(LogLevel.Info, "TODO: implement READ SFDP");
                    break;
                case DecodedOperation.OperationType.Program:
                    WriteMemory(data);
                    result = data;
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unhandled operation while processing byte: 0x{0:X}", (byte)currentOperation.Operation);
                    break;
            }
            currentOperation.CommandBytesHandled++;
            this.Log(LogLevel.Noisy, "Handled command: 0x{0:x}, readed: 0x{1:x}", currentOperation.Operation, result);
            return result;
        }

        public void OnGPIO(int number, bool value)
        {
            if (number == 0 && value)
            {
                this.Log(LogLevel.Noisy, "CS# deasserted");
                FinishTransmission();
            }
        }

        public void Reset()
        {
            currentOperation = default;
            writeEnable.Value = false;
            continuousReadMode = null;
            originalCommandDummyBytes = 0;
        }
        public MappedMemory UnderlyingMemory => underlyingMemory;

        protected DecodedOperation currentOperation;
        protected readonly MappedMemory underlyingMemory;

        protected enum Command : byte
        {
            WriteEnable = 0x06,
            VolatileSRWriteEnable = 0x50,
            WriteDisable = 0x04,
            ReleasePowerDown = 0xab,
            ManufacturerId = 0x90,
            JEDECId = 0x9f,
            ReadUniqueId = 0x4b,
            ReadData = 0x03,
            FastRead = 0x0b,
            PageProgram = 0x02,
            SectorErase4K = 0x20,
            BlockErase32K = 0x52,
            BlockErase64K = 0xd8,
            ChipErase = 0xc7,
            BulkErase = 0x60,
            ReadStatusRegister1 = 0x05,
            WriteStatusRegister1 = 0x01,
            ReadStatusRegister2 = 0x35,
            WriteStatusRegister2 = 0x31,
            ReadStatusRegister3 = 0x15,
            WriteStatusRegister3 = 0x11,
            ReadSFDPRegister = 0x5a,
            EraseSecurityRegister = 0x44,
            ProgramSecurityRegister = 0x42,
            ReadSecurityRegister = 0x48,
            GlobalBlockLock = 0x7e,
            GlobalBlockUnlock = 0x98,
            ReadBlockLock = 0x3d,
            IndividualBlockLock = 0x36,
            IndividualBlockUnlock = 0x39,
            EraseProgramSuspend = 0x75,
            EraseProgramResume = 0x7a,
            PowerDown = 0xb9,
            EnableReset = 0x66,
            ResetDevice = 0x99,
            FastReadDualOutput = 0x3b,
            FastReadDualIO = 0xbb,
            ManufacturerIdDualId = 0x92,
            QuadInputPageProgram = 0x32,
            FastReadQuadOutput = 0x6b,
            ManufacturerIdQuadIO = 0x94,
            FastReadQuadIO = 0xeb,
            SetBurstWithWrap = 0x77
        }

        private enum Register : uint
        {
            Status1 = 1,
            Status2,
            Status3
        }

        private readonly ByteRegister statusRegister;
        private readonly IFlagRegisterField writeEnable;
        private bool? continuousReadMode;
        private int originalCommandDummyBytes;
        protected Range? lockedRange;

        private const byte manufacturerId = 0xEF;
        private const byte memoryType = 0x28;
    }

}
