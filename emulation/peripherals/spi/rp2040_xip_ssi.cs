/**
 * rp2040_xip_ssi.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;

using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities.Collections;
using Antmicro.Renode.Logging;
using IronPython.Modules;
using Antmicro.Renode.PlatformDescription.Syntax;
using System.Xml.Serialization;
using Antmicro.Renode.UI;
using System.Runtime.InteropServices;
using Microsoft.Scripting.Interpreter;
using System.Linq;
using Dynamitey;
using Microsoft.Scripting.Utils;
using Antmicro.Renode.Peripherals.Miscellaneous.S32K3XX_FlexIOModel;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.PlatformDescription;
using System.Data.Common;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.SPI
{
  // Slave mode is not yet supported, XIP mode is not implemented since underlaying memory is mapped
  public class RP2040XIPSSI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
  {
    public long Size { get { return 0x1000; } }

    public const ulong aliasSize = 0x1000;
    public const ulong xorAliasOffset = 0x1000;
    public const ulong setAliasOffset = 0x2000;
    public const ulong clearAliasOffset = 0x3000;
    public RP2040XIPSSI(IMachine machine, ulong address, IGPIOReceiver chipSelect) : base(machine)
    {
      registers = new DoubleWordRegisterCollection(this);
      receiveBuffer = new CircularBuffer<UInt32>(16);
      transmitBuffer = new CircularBuffer<UInt32>(16);
      machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + xorAliasOffset, aliasSize, "XOR"));
      machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + setAliasOffset, aliasSize, "SET"));
      machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + clearAliasOffset, aliasSize, "CLEAR"));
      DmaTransmitDreq = new GPIO();
      DmaStreamDreq = new GPIO();
      dreqThread = machine.ObtainManagedThread(ProcessDreq, 1);
      dreqThread.Stop();
      dreqThread.Frequency = 1000000; // just for now, maybe I should connect that to real clock frequency
      clockingThread = machine.ObtainManagedThread(TransferClock, 1);
      clockingThread.Frequency = 10000000;
      rxDmaEnabled = false;
      txDmaEnabled = false;
      this.chipSelect = chipSelect;
      DefineRegisters();
    }

    void ProcessDreq()
    {
      DmaTransmitDreq.Toggle();
    }

    [ConnectionRegion("XOR")]
    public virtual void WriteDoubleWordXor(long offset, uint value)
    {
      registers.Write(offset, registers.Read(offset) ^ value);
    }

    [ConnectionRegion("SET")]
    public virtual void WriteDoubleWordSet(long offset, uint value)
    {
      registers.Write(offset, registers.Read(offset) | value);
    }

    [ConnectionRegion("CLEAR")]
    public virtual void WriteDoubleWordClear(long offset, uint value)
    {
      registers.Write(offset, registers.Read(offset) & (~value));
    }

    [ConnectionRegion("XOR")]
    public virtual uint ReadDoubleWordXor(long offset)
    {
      return registers.Read(offset);
    }

    [ConnectionRegion("SET")]
    public virtual uint ReadDoubleWordSet(long offset)
    {
      return registers.Read(offset);
    }

    [ConnectionRegion("CLEAR")]
    public virtual uint ReadDoubleWordClear(long offset)
    {
      return registers.Read(offset);
    }

    public uint ReadDoubleWord(long offset)
    {
      return registers.Read(offset);
    }

    public void WriteDoubleWord(long offset, uint value)
    {
      registers.Write(offset, value);
    }

    public override void Reset()
    {
      bytesToTransfer = 0;
      ssiEnabled = false;
      commandBytesTransferred = 0;
    }

    private ulong PeripheralDataRead()
    {
      lock (receiveBuffer)
      {
        if (receiveBuffer.TryDequeue(out var value))
        {
          this.Log(LogLevel.Error, "Deq: " + value);
          return value;
        }
        return 0;
      }
    }

    private void ProcessXipTranmission()
    {
      var peripheral = RegisteredPeripheral;
      this.Log(LogLevel.Error, "XIP to peripheral: " + dataFrameSize.Value + ", 32: " + dataFrameSize32.Value + ", NDF: " + numberOfDataFrames.Value + ", CFS: " + controlFrameSize.Value + ", XIP_CMD: " + xipCmd.Value + ", INST_L: " + instructionLength.Value + ", address: " + addressLength.Value);
      // transmit XIP command 
      peripheral.Transmit((byte)xipCmd.Value);
    }

    private enum State
    {
      Wait,
      Instruction,
      Address,
      WaitCycles,
      Data
    };

    private State state;


    private void PeripheralDataWrite(ulong data)
    {
      this.Log(LogLevel.Error, "TMOD: {0}, data: {1:x}", tmod.Value, data);
      if (transmitBuffer.Count < 16)
      {
        transmitBuffer.Enqueue((uint)data);
      }
    }

    void PushToReceiveFifo(uint data)
    {
      if (receiveBuffer.Count < 16)
      {
        receiveBuffer.Enqueue(data);
      }
      if (rxDmaEnabled)
      {
        DmaStreamDreq.Toggle();
      }
    }

    uint WriteToDevice(uint data, int bits)
    {
      uint received = 0;
      for (int i = 0; i < bits; i += 8)
      {
        int offset = bits - 8 - i;
        received |= (uint)RegisteredPeripheral.Transmit((byte)(data >> offset)) << i;
      }
      return received;
    }
    void ProcessReceive()
    {
      switch (state)
      {
        case State.Instruction:
          {
            if (!transmitBuffer.TryDequeue(out var data))
            {
              clockingThread.Stop();
              return;
            }

            this.Log(LogLevel.Noisy, "Sending command from FIFO: {0:X}", data);

            if (instructionLength.Value == 0)
            {
              // this is XIP special mode with appending instruction after address, taking from 32-bit address 
              // for XIP operations addressing is limited to 32 bits
              int bytes = (int)Math.Ceiling((double)addressLength.Value / 2);
              
              this.Log(LogLevel.Noisy, "Sending continuation code: {0}", bytes); 
              // first byte goes at end 
              WriteToDevice(data, bytes); 
              cyclesToWait = (int)Math.Ceiling((double)waitCycles.Value / 8);
              if (cyclesToWait > 0)
              {
                state = State.WaitCycles;
              }
              else 
              {
                state = State.Data;
                framesToTransfer = (int)numberOfDataFrames.Value + 1;
              }
              return;
            }
            int bits = 1 << (int)(1 + instructionLength.Value);
            this.Log(LogLevel.Noisy, "Writing instruction with size: " + bits + ", instru: " + instructionLength.Value); 
            // this is just instruction, no address bytes yet 
            WriteToDevice(data, bits); 
            state = State.Address;
            addressBytes = (int)Math.Ceiling((double)addressLength.Value / 2);
            return;
          }
        case State.Address:
          {
            if (!transmitBuffer.TryDequeue(out var data))
            {
              this.Log(LogLevel.Error, "Requested address transmission, but there is no data in FIFO");
              return;
            }

            this.Log(LogLevel.Noisy, "Transmitting address bytes: {0:X}, size: {1}", data, addressBytes);
            WriteToDevice(data, addressBytes * 8); 
            if (waitCycles.Value != 0)
            {
              state = State.WaitCycles;
              cyclesToWait = (int)Math.Ceiling((double)waitCycles.Value / 8);
              this.Log(LogLevel.Noisy, "Wait cycles are necessary, waiting for: {0}", cyclesToWait);
              return;
            }
            state = State.Data;
            framesToTransfer = (int)numberOfDataFrames.Value + 1;
            this.Log(LogLevel.Noisy, "Data frames to transfer: {0}", framesToTransfer);
            return;
          }
        case State.WaitCycles:
        {
          for (int i = 0; i < cyclesToWait; ++i)
          {
            RegisteredPeripheral.Transmit(0x00);
          }
          state = State.Data;
          framesToTransfer = (int)numberOfDataFrames.Value + 1;
          return;
        }
        case State.Data:
        {
          this.Log(LogLevel.Noisy, "Transmiting data frames left: " + framesToTransfer);
          int dataSize = (int)Math.Ceiling((double)dataFrameSize32.Value / 8);
          // if tmod is read only
          PushToReceiveFifo(WriteToDevice(0, (int)dataFrameSize32.Value + 1));
          if (--framesToTransfer <= 0)
          {
            clockingThread.Stop();
          }
          return;
        }
      }
    }

    void ProcessTransmit()
    {
      if (!transmitBuffer.TryDequeue(out var data))
      {
        return;
      }
      uint received = 0;
      busy.Value = true;
      for (int i = 0; i < (int)Math.Ceiling((double)dataFrameSize32.Value/8); ++i)
      {
        received |= RegisteredPeripheral.Transmit((byte)(data >> (i * 8)));
      }

      this.Log(LogLevel.Error, "TR: " + data + ", rcs: " + receiveBuffer.Count);
      PushToReceiveFifo(received); 
      busy.Value = false;
    }

    // This is designed to transfer up to 4 bits per clock to reduce overhead 
    // It may be not 100% clock accurate with real HW, but should be good enough
    private void TransferClock()
    {
      if (tmod.Value == 0)
      {
        ProcessTransmit();
      }
      else if (tmod.Value == 1)
      {

      }
      else if (tmod.Value == 3)
      {
        ProcessReceive();
      }
    }

    private void DefineRegisters()
    {
      Registers.CTRLR0.Define(registers)
        .WithValueField(0, 4, out dataFrameSize, name: "DFS")
        .WithValueField(4, 2, name: "FRF")
        .WithTaggedFlag("SCPH", 6)
        .WithTaggedFlag("SCPOL", 7)
        .WithValueField(8, 2, out tmod, name: "TMOD")
        .WithTaggedFlag("SLV_OE", 10)
        .WithTaggedFlag("SRL", 11)
        .WithValueField(12, 4, out controlFrameSize, name: "CFS")
        .WithValueField(16, 5, out dataFrameSize32, name: "DFS_32")
        .WithValueField(21, 2, name: "SPI_FRF")
        .WithReservedBits(23, 1)
        .WithTaggedFlag("SSTE", 24)
        .WithValueField(25, 7);

      Registers.CTRLR1.Define(registers)
        .WithValueField(0, 16, out numberOfDataFrames, name: "NDF")
        .WithReservedBits(16, 16);

      Registers.SSIENR.Define(registers)
        .WithFlag(0, writeCallback: (_, value) =>
        {
          if (!value)
          {
            commandBytesTransferred = 0;
          }

          if (ssiEnabled != value)
          {
            receiveBuffer.Clear();
            transmitBuffer.Clear();
            chipSelect.OnGPIO(0, value);
            if (value)
            {
              state = State.Instruction;
              this.Log(LogLevel.Noisy, "Enabling clocking thread, transmission started");
              clockingThread.Start();
            }
            else
            {
              this.Log(LogLevel.Noisy, "Disabling clocking thread, transmission finished");
              clockingThread.Stop();
            }
          }
          ssiEnabled = value;
        }, valueProviderCallback: _ => ssiEnabled, name: "ENABLED")
        .WithReservedBits(1, 31);

      Registers.MWCR.Define(registers)
        .WithTaggedFlag("MWMOD", 0)
        .WithTaggedFlag("MDD", 1)
        .WithTaggedFlag("MHS", 2)
        .WithReservedBits(3, 29);

      Registers.SER.Define(registers)
        .WithFlag(0, out slaveEnabled, writeCallback: (_, value) =>
        {
          if (value)
          {
            commandBytesTransferred = 0;
          }
          chipSelect.OnGPIO(0, value);
        }, name: "CS")
        .WithReservedBits(1, 31);

      Registers.BAUDR.Define(registers)
        .WithValueField(0, 16, name: "SCKDV")
        .WithReservedBits(16, 16);

      Registers.TXFTLR.Define(registers)
        .WithValueField(0, 8, out transmitFifoThreshold, name: "TFT")
        .WithReservedBits(8, 24);

      Registers.RXFTLR.Define(registers)
        .WithValueField(0, 8, out receiveFifoThreshold, name: "RFT")
        .WithReservedBits(8, 24);

      Registers.TXFLR.Define(registers)
        .WithValueField(0, 8, valueProviderCallback: _ => (byte)transmitBuffer.Count, name: "TXFTL")
        .WithReservedBits(8, 24);

      Registers.RXFLR.Define(registers)
        .WithValueField(0, 8, valueProviderCallback: _ => (byte)receiveBuffer.Count, name: "RXFTL")
        .WithReservedBits(8, 24);

      Registers.SR.Define(registers)
        .WithFlag(0, out busy, name: "BUSY")
        .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => transmitBuffer.Count < 16,
            name: "TFNF")
        .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => transmitBuffer.Count == 0,
            name: "TFE")
        .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => receiveBuffer.Count > 0,
            name: "RFNE")
        .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => receiveBuffer.Count == 16,
            name: "RFF")
        .WithTaggedFlag("TXE", 5)
        .WithTaggedFlag("DCOL", 6)
        .WithReservedBits(7, 25);

      Registers.IMR.Define(registers)
              .WithTaggedFlag("TXEIM", 0)
              .WithTaggedFlag("TXOIM", 1)
              .WithTaggedFlag("RXUIM", 2)
              .WithTaggedFlag("RXOIM", 3)
              .WithTaggedFlag("RXFIM", 4)
              .WithTaggedFlag("MSTIM", 5)
              .WithReservedBits(6, 26);

      Registers.ISR.Define(registers)
        .WithTaggedFlag("TXEIS", 0)
        .WithTaggedFlag("TXOIS", 1)
        .WithTaggedFlag("RXUIS", 2)
        .WithTaggedFlag("RXOIS", 3)
        .WithTaggedFlag("RXFIS", 4)
        .WithTaggedFlag("MSTIS", 5)
        .WithReservedBits(6, 26);

      Registers.RISR.Define(registers)
        .WithTaggedFlag("TXEIR", 0)
        .WithTaggedFlag("TXOIR", 1)
        .WithTaggedFlag("RXUIR", 2)
        .WithTaggedFlag("RXOIR", 3)
        .WithTaggedFlag("RXFIR", 4)
        .WithTaggedFlag("MSTIR", 5)
        .WithReservedBits(6, 26);

      Registers.TXOICR.Define(registers)
        .WithTaggedFlag("TXOICR", 0)
        .WithReservedBits(1, 31);

      Registers.RXOICR.Define(registers)
        .WithTaggedFlag("RXOICR", 0)
        .WithReservedBits(1, 31);

      Registers.RXUICR.Define(registers)
        .WithTaggedFlag("RXUICR", 0)
        .WithReservedBits(1, 31);

      Registers.MSTICR.Define(registers)
        .WithTaggedFlag("MSTICR", 0)
        .WithReservedBits(1, 31);

      Registers.ICR.Define(registers)
        .WithTaggedFlag("ICR", 0)
        .WithReservedBits(1, 31);

      Registers.DMACR.Define(registers)
        .WithFlag(0, valueProviderCallback: _ => rxDmaEnabled,
          writeCallback: (_, value) =>
          {
            rxDmaEnabled = value;
          }, name: "RDMAE")
        .WithFlag(1, valueProviderCallback: _ => txDmaEnabled,
          writeCallback: (_, value) =>
          {
            if (value && !txDmaEnabled)
            {
              txDmaEnabled = false;
              dreqThread.Start();
            }
            if (!value && txDmaEnabled)
            {
              txDmaEnabled = true;
              dreqThread.Stop();
            }
          }, name: "TDMAE")
        .WithReservedBits(2, 30);

      Registers.DMATDLR.Define(registers)
        .WithValueField(0, 8, name: "DMATDLR")
        .WithReservedBits(8, 24);

      Registers.DMARDLR.Define(registers)
        .WithValueField(0, 8, name: "DMARDL")
        .WithReservedBits(8, 24);

      Registers.IDR.Define(registers)
        .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => idcode, name: "IDCODE");

      Registers.SSI_VERSION_ID.Define(registers)
        .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => ssi_comp_version, name: "SSI_COMP_VERSION");

      Registers.DR0.Define(registers)
        .WithValueField(0, 32, valueProviderCallback: _ => PeripheralDataRead(),
            writeCallback: (_, value) => PeripheralDataWrite(value),
            name: "DR0"
        );

      int dr = 1;
      foreach (Registers r in Enum.GetValues(typeof(Registers)))
      {
        if (r >= Registers.DR1 && r <= Registers.DR35)
        {
          int i = dr;
          r.Define(registers)
            .WithValueField(0, 32, name: "DR" + i);
          ++dr;
        }
      }

      Registers.RX_SAMPLE_DLY.Define(registers)
        .WithValueField(0, 8, name: "RSD")
        .WithReservedBits(8, 24);

      Registers.SPI_CTRLR0.Define(registers)
        .WithValueField(0, 2, name: "TRANS_TYPE")
        .WithValueField(2, 4, out addressLength, name: "ADDR_L")
        .WithReservedBits(6, 2)
        .WithValueField(8, 2, out instructionLength, name: "INST_L")
        .WithReservedBits(10, 1)
        .WithValueField(11, 5, out waitCycles, name: "WAIT_CYCLES")
        .WithTaggedFlag("SPI_DDR_EN", 16)
        .WithTaggedFlag("INST_DDR_EN", 17)
        .WithTaggedFlag("SPI_RXDS_EN", 18)
        .WithReservedBits(19, 5)
        .WithValueField(24, 8, out xipCmd, name: "XIP_CMD");
    }
    private DoubleWordRegisterCollection registers;

    private uint idcode = 0x51535049;
    private uint ssi_comp_version = 0x3430412a;

    private CircularBuffer<UInt32> receiveBuffer;
    private CircularBuffer<UInt32> transmitBuffer;

    private bool ssiEnabled;
    private IFlagRegisterField slaveEnabled;

    // I don't really have SSI simulation, so I can stub it with just timer right now
    private IManagedThread dreqThread;
    private IManagedThread clockingThread;
    private bool rxDmaEnabled;
    private bool txDmaEnabled;
    public GPIO DmaTransmitDreq { get; }
    public GPIO DmaStreamDreq { get; }

    private IValueRegisterField numberOfDataFrames;
    private IValueRegisterField dataFrameSize;
    private IValueRegisterField dataFrameSize32;
    private IValueRegisterField controlFrameSize;
    private IValueRegisterField xipCmd;
    private IValueRegisterField instructionLength;
    private IValueRegisterField addressLength;
    private IValueRegisterField tmod;
    private IValueRegisterField waitCycles;
    private IValueRegisterField transmitFifoThreshold;
    private IValueRegisterField receiveFifoThreshold;
    private IGPIOReceiver chipSelect;
    private IFlagRegisterField busy;
    private int bytesToTransfer;
    private int addressBytes;
    private int commandBytesTransferred;
    private int cyclesToWait;
    private int framesToTransfer;
    private enum Registers
    {
      CTRLR0 = 0x0,
      CTRLR1 = 0x4,
      SSIENR = 0x8,
      MWCR = 0xC,
      SER = 0x10,
      BAUDR = 0x14,
      TXFTLR = 0x18,
      RXFTLR = 0x1C,
      TXFLR = 0x20,
      RXFLR = 0x24,
      SR = 0x28,
      IMR = 0x2c,
      ISR = 0x30,
      RISR = 0x34,
      TXOICR = 0x38,
      RXOICR = 0x3c,
      RXUICR = 0x40,
      MSTICR = 0x44,
      ICR = 0x48,
      DMACR = 0x4c,
      DMATDLR = 0x50,
      DMARDLR = 0x54,
      IDR = 0x58,
      SSI_VERSION_ID = 0x5c,
      DR0 = 0x60,
      DR1 = 0x64,
      DR35 = 0xec,
      RX_SAMPLE_DLY = 0xf0,
      SPI_CTRLR0 = 0xf4,
      TXD_DRIVE_EDGE = 0xf8
    }
  }
}
