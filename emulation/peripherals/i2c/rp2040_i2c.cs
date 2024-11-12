/**
 * rp2040_i2c.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Antmicro.OptionsParser;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.GPIOPort;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Collections;
using Lucene.Net.Search;
using Microsoft.Scripting.Runtime;
using Mono.Cecil.Cil;


namespace Antmicro.Renode.Peripherals.I2C
{

    // This peripheral supports both, fast c# interfaced read/writes from simulated I2C peripherals 
    // And bit banged GPIO for PIO interworking
    class RP2040I2C : SimpleContainer<II2CPeripheral>, II2CPeripheral, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RP2040I2C(IMachine machine, RP2040Clocks clocks, ulong address, int id, RP2040GPIO gpio) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            txFifo = new CircularBuffer<DataEntry>(icTxBufferDepth);
            rxFifo = new CircularBuffer<byte>(icRxBufferDepth);
            sclPins = new List<int>();
            sdaPins = new List<int>();
            this.clocks = clocks;
            this.gpio = gpio;
            this.id = id;

            gpio.SubscribeOnFunctionChange(OnGpioFunctionSelect);
            DefineRegisters();

            machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + xorAliasOffset, aliasSize, "XOR"));
            machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + setAliasOffset, aliasSize, "SET"));
            machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + clearAliasOffset, aliasSize, "CLEAR"));

            executionThread = machine.ObtainManagedThread(Step, 1, "I2C" + id + "_THREAD");

            Reset();
        }

        public override void Reset()
        {
            txFifo.Clear();
            rxFifo.Clear();
            sdaPins.Clear();
            sclPins.Clear();
            currentSlave = null;
            state = State.Idle;

            masterMode.Value = true;
            speed.Value = 0x2;
            address10BitsSlave.Value = false;
            address10BitsMaster.Value = false;
            restartEnabled.Value = true;
            slaveDisable.Value = true;
            stopDetectionIfAddressed.Value = false;
            txEmptyControl.Value = false;
            rxFifoFullHoldControl.Value = false;
            stopDetectionIfMasterActive.Value = false;
            icTar.Value = 0x055;
            gcOrStart.Value = false;
            special.Value = false;
            icSar.Value = 0x055;
            icSsSclHcnt.Value = 0x0028;
            icSsSclLcnt.Value = 0x002f;
            icFsSclHcnt.Value = 0x0006;
            icFsSclLcnt.Value = 0x000d;

            icTxTl.Value = 0;
            rxOver = false;
            rxUnder = false;

            enable.Value = false;

            startDet.Value = false;
            stopDet.Value = false;

            transmissionOngoing = false;

            UpdateFrequency();
        }

        public byte ReadByte(long offset)
        {
            return 0;
        }

        public void WriteByte(long offset, byte value)
        {

        }

        public void Write(byte[] data)
        {

        }

        public byte[] Read(int count = 1)
        {
            return new byte[0];
        }

        public void FinishTransmission()
        {

        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }


        [ConnectionRegion("XOR")]
        public virtual void WriteDoubleWordXor(long offset, uint value)
        {
            RegistersCollection.Write(offset, RegistersCollection.Read(offset) ^ value);
        }

        [ConnectionRegion("SET")]
        public virtual void WriteDoubleWordSet(long offset, uint value)
        {
            RegistersCollection.Write(offset, RegistersCollection.Read(offset) | value);
        }

        [ConnectionRegion("CLEAR")]
        public virtual void WriteDoubleWordClear(long offset, uint value)
        {
            RegistersCollection.Write(offset, RegistersCollection.Read(offset) & (~value));
        }

        [ConnectionRegion("XOR")]
        public virtual uint ReadDoubleWordXor(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        [ConnectionRegion("SET")]
        public virtual uint ReadDoubleWordSet(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        [ConnectionRegion("CLEAR")]
        public virtual uint ReadDoubleWordClear(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public long Size
        {
            get { return 0x1000; }
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.IC_CON.Define(this)
                .WithFlag(0, out masterMode, name: "MASTER_MODE")
                .WithValueField(1, 2, out speed, name: "SPEED")
                .WithFlag(3, out address10BitsSlave, name: "IC_10BITADDR_SLAVE")
                .WithFlag(4, out address10BitsMaster, name: "IC_10BITADDR_MASTER")
                .WithFlag(5, out restartEnabled, name: "IC_RESTART_EN")
                .WithFlag(6, out slaveDisable, name: "IC_SLAVE_DISABLE")
                .WithFlag(7, out stopDetectionIfAddressed, name: "STOP_DET_IFADDRESSED")
                .WithFlag(8, out txEmptyControl, name: "TX_EMPTY_CTRL")
                .WithFlag(9, out rxFifoFullHoldControl, name: "RX_FIFO_FULL_HLD_CTRL")
                .WithFlag(10, out stopDetectionIfMasterActive, FieldMode.Read, name: "STOP_DET_IF_MASTER_ACTIVE")
                .WithReservedBits(11, 21);

            Registers.IC_TAR.Define(this)
                .WithValueField(0, 10, out icTar, name: "IC_TAR")
                .WithFlag(10, out gcOrStart, name: "GC_OR_START")
                .WithFlag(11, out special, name: "SPECIAL")
                .WithReservedBits(12, 20);

            Registers.IC_SAR.Define(this)
                .WithValueField(0, 10, out icSar, name: "IC_SAR")
                .WithReservedBits(10, 22);

            Registers.IC_DATA_CMD.Define(this)
                .WithValueField(0, 12,
                    writeCallback: (_, value) =>
                    {
                        var entry = new DataEntry();
                        entry.Data = (byte)(value & 0xff);
                        entry.Command = (value & (1 << 8)) != 0;
                        entry.Stop = (value & (1 << 9)) != 0;
                        entry.Restart = (value & (1 << 10)) != 0;
                        entry.FirstDataByte = (value & (1 << 11)) != 0;
                        txFifo.Enqueue(entry);
                    },
                    valueProviderCallback: _ =>
                    {
                        if (!rxFifo.TryDequeue(out var result))
                        {
                            rxUnder = true;
                            return 0;
                        }
                        return result;
                    }, name: "DAT")
                .WithReservedBits(12, 20);

            Registers.IC_SS_SCL_HCNT.Define(this)
                .WithValueField(0, 16, out icSsSclHcnt, name: "IC_SS_SCL_HCNT")
                .WithReservedBits(16, 16);

            Registers.IC_SS_SCL_LCNT.Define(this)
                .WithValueField(0, 16, out icSsSclLcnt, name: "IC_SS_SCL_LCNT")
                .WithReservedBits(16, 16);

            Registers.IC_FS_SCL_HCNT.Define(this)
                .WithValueField(0, 16, out icFsSclHcnt, name: "IC_FS_SCL_HCNT")
                .WithReservedBits(16, 16);

            Registers.IC_FS_SCL_LCNT.Define(this)
                .WithValueField(0, 16, out icFsSclLcnt, name: "IC_FS_SCL_LCNT")
                .WithReservedBits(16, 16);

            Registers.IC_RAW_INTR_STAT.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => rxUnder, name: "RX_UNDER")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => rxOver, name: "RX_OVER")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => rxFifo.Count == icRxBufferDepth, name: "RX_FULL")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => false, name: "TX_OVER") // implement 
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => {
                    bool result = txFifo.Count <= (int)icTxTl.Value;
                    if (txEmptyControl.Value)
                    {
                        result &= !transmissionOngoing;
                    } 
                    return result; 
                }, name: "TX_EMPTY")
                .WithFlag(9, out stopDet, FieldMode.Read, name: "STOP_DET")
                .WithFlag(10, out startDet, FieldMode.Read, name: "START_DET");

            Registers.IC_ENABLE.Define(this)
                .WithFlag(0, out enable, writeCallback: (_, value) =>
                {
                    if (value)
                    {
                        executionThread.Start();
                    }
                    else
                    {
                        executionThread.Stop();
                    }
                }, name: "ENABLE");

            Registers.IC_TXFLR.Define(this)
                .WithValueField(0, 5, FieldMode.Read, valueProviderCallback: _ => (uint)txFifo.Count, name: "TXFLR")
                .WithReservedBits(5, 27);
            Registers.IC_RXFLR.Define(this)
                .WithValueField(0, 5, FieldMode.Read, valueProviderCallback: _ => (uint)rxFifo.Count, name: "RXFLR")
                .WithReservedBits(5, 27);

            Registers.IC_CLR_TX_ABRT.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "IC_CLR_TX_ABRT")
                .WithReservedBits(1, 31);

            Registers.IC_CLR_STOP_DET.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ =>
                {
                    stopDet.Value = false;
                    return false;
                }, name: "CLR_STOP_DET")
                .WithReservedBits(1, 31);

            Registers.IC_TX_ABRT_SOURCE.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_7B_ADDR_NOACK")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_10B_ADDR1_NOACK")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_10B_ADDR2_NOACK")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_TXDATA_NOACK")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_GCALL_NOACK")
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_GCALL_READ")
                .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_HS_ACKDET")
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_SBYTE_ACKDET")
                .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_HS_NORSTRT")
                .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_SBYTE_NORSTRT")
                .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_10B_RD_NORSTRT")
                .WithFlag(11, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_MASTER_DIS")
                .WithFlag(12, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_LOST")
                .WithFlag(13, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_SLVFLUSH_TXFIFO")
                .WithFlag(14, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_SLVRD_ARBLOST")
                .WithFlag(15, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_SLVRD_INTX")
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => false, name: "ABRT_USER_ABRT")
                .WithReservedBits(17, 6)
                .WithValueField(23, 9, FieldMode.Read, valueProviderCallback: _ => 0, name: "TX_FLUSH_CNT");

            Registers.IC_TX_TL.Define(this)
                .WithValueField(0, 8, out icTxTl, name: "IC_TX_TL", writeCallback: (_, value) => {
                    if (value > icTxBufferDepth)
                    {
                        value = icTxBufferDepth - 1;
                    }
                })
                .WithReservedBits(8, 24);
        }

        private void UpdateFrequency()
        {
            uint frequency = 100000;
            // this.Log(LogLevel.Debug, "Updating I2C frequency to: {0}", frequency);
            executionThread.Frequency = frequency;
        }

        private void OnGpioFunctionSelect(int pin, RP2040GPIO.GpioFunction function)
        {
            if (id == 0)
            {
                switch (function)
                {
                    case RP2040GPIO.GpioFunction.I2C0_SCL:
                        {
                            sclPins.Add(pin);
                            break;
                        }
                    case RP2040GPIO.GpioFunction.I2C0_SDA:
                        {
                            sdaPins.Add(pin);
                            break;
                        }
                    case RP2040GPIO.GpioFunction.NONE:
                        {
                            sclPins.Remove(pin);
                            sdaPins.Remove(pin);
                            break;
                        }
                }
            }
            else if (id == 1)
            {
                switch (function)
                {
                    case RP2040GPIO.GpioFunction.I2C1_SCL:
                        {
                            sclPins.Add(pin);
                            break;
                        }
                    case RP2040GPIO.GpioFunction.I2C1_SDA:
                        {
                            sdaPins.Add(pin);
                            break;
                        }
                    case RP2040GPIO.GpioFunction.NONE:
                        {
                            sclPins.Remove(pin);
                            sdaPins.Remove(pin);
                            break;
                        }
                }
            }
            else
            {
                // this.Log(LogLevel.Error, "Unsupported I2C id: {0}", id);
            }
        }
        private void StartTransfer()
        {
            // this.Log(LogLevel.Noisy, "I2C starting new transfer for: 0x{0:X}", icTar.Value);
            startDet.Value = true;
            state = State.AddressFrame;
        }

        private void ProcessAddress()
        {
            if (currentSlave == null)
            {
                TryGetByAddress((int)icTar.Value, out currentSlave);
                // this.Log(LogLevel.Noisy, "Got slave at address 0x{0:X} with name: {1}", icTar.Value, currentSlave.GetName());
            }
            state = State.Data;
        }

        // Get first element from fifo, then copy all until next one is stop command or end
        private void ProcessDataReadWrite()
        {
            if (currentSlave != null)
            {
                List<byte> buffer = new List<byte>();
                if (txFifo.Count == 0)
                {
                    // tx under
                    return;
                }

                bool stop = false;
                bool? write = null;
                while (txFifo.TryDequeue(out var e))
                {
                    if (write == null)
                    {
                        write = e.Command == false;
                    }

                    buffer.Add(e.Data);
                    if (e.Stop)
                    {
                        stop = true;
                        stopDet.Value = true;
                        break;
                    }
                }

                if (write != null)
                {
                    if (write.Value)
                    {
                        transmissionOngoing = true;
                        currentSlave.Write(buffer.ToArray());
                        transmissionOngoing = false;
                    }
                    else 
                    {
                        var ret = currentSlave.Read(buffer.Count);
                        foreach (byte b in ret)
                        {
                            rxFifo.Enqueue(b);
                        }
                    }
                }

                if (stop)
                {
                    currentSlave.FinishTransmission();
                }

                if (txFifo.Count == 0)
                {
                    state = State.Idle;
                }
            }
        }

        // RP2040 must simulate I2C together with GPIO toggling, otherwise 
        // I2C<->PIO interworking won't be possible
        private void Step()
        {
            switch (state)
            {
                case State.Idle:
                    {
                        if (txFifo.Count != 0)
                        {
                            state = State.StartCondition;
                        }
                        return;
                    }
                case State.StartCondition:
                    {
                        StartTransfer();
                        return;
                    }
                case State.AddressFrame:
                    {
                        ProcessAddress();
                        return;
                    }
                case State.Data:
                    {
                        ProcessDataReadWrite();
                        return;
                    }
            }
        }

        private enum Registers : long
        {
            IC_CON = 0x00,
            IC_TAR = 0x04,
            IC_SAR = 0x08,
            IC_DATA_CMD = 0x10,
            IC_SS_SCL_HCNT = 0x14,
            IC_SS_SCL_LCNT = 0x18,
            IC_FS_SCL_HCNT = 0x1c,
            IC_FS_SCL_LCNT = 0x20,
            IC_INTR_STAT = 0x2c,
            IC_INTR_MASK = 0x30,
            IC_RAW_INTR_STAT = 0x34,
            IC_RX_TL = 0x38,
            IC_TX_TL = 0x3c,
            IC_CLR_INTR = 0x40,
            IC_CLR_RX_UNDER = 0x44,
            IC_CLR_RX_OVER = 0x48,
            IC_CLR_TX_OVER = 0x4c,
            IC_CLR_RD_REQ = 0x50,
            IC_CLR_TX_ABRT = 0x54,
            IC_CLR_RX_DONE = 0x58,
            IC_CLR_ACTIVITY = 0x5c,
            IC_CLR_STOP_DET = 0x60,
            IC_CLR_START_DET = 0x64,
            IC_CLR_GEN_CALL = 0x68,
            IC_ENABLE = 0x6c,
            IC_STATUS = 0x70,
            IC_TXFLR = 0x74,
            IC_RXFLR = 0x78,
            IC_SDA_HOLD = 0x7c,
            IC_TX_ABRT_SOURCE = 0x80,
            IC_SLV_DATA_NACK_ONLY = 0x84,
            IC_DMA_CR = 0x88,
            IC_DMA_TDLR = 0x8c,
            IC_DMA_RDLR = 0x90,
            IC_SDA_SETUP = 0x94,
            IC_ACK_GENERAL_ACK = 0x98,
            IC_ENABLE_STATUS = 0x9c,
            IC_FS_SPKLEN = 0xa0,
            IC_CLR_RESTART_DET = 0xa8,
            IC_COMP_PARAM_1 = 0xf4,
            IC_COMP_VERSION = 0xf8,
            IC_COMP_TYPE = 0xfc
        }

        private enum State
        {
            Idle,
            StartCondition,
            AddressFrame,
            Ack,
            Data
        }

        private RP2040Clocks clocks;
        private RP2040GPIO gpio;
        private int id;
        private List<int> sclPins;
        private List<int> sdaPins;

        private struct DataEntry
        {
            public byte Data;
            public bool Command;
            public bool Stop;
            public bool Restart;
            public bool FirstDataByte;
        }
        private CircularBuffer<DataEntry> txFifo;
        private CircularBuffer<byte> rxFifo;

        private II2CPeripheral currentSlave;

        private IFlagRegisterField masterMode;
        private IValueRegisterField speed;
        private IFlagRegisterField address10BitsSlave;
        private IFlagRegisterField address10BitsMaster;
        private IFlagRegisterField restartEnabled;
        private IFlagRegisterField slaveDisable;
        private IFlagRegisterField stopDetectionIfAddressed;
        private IFlagRegisterField txEmptyControl;
        private IFlagRegisterField rxFifoFullHoldControl;
        private IFlagRegisterField stopDetectionIfMasterActive;

        private IValueRegisterField icTar;
        private IFlagRegisterField gcOrStart;
        private IFlagRegisterField special;

        private IValueRegisterField icSar;

        private IValueRegisterField icSsSclHcnt;
        private IValueRegisterField icSsSclLcnt;
        private IValueRegisterField icFsSclHcnt;
        private IValueRegisterField icFsSclLcnt;

        private IFlagRegisterField enable;

        private bool rxUnder;
        private bool rxOver;
        private IFlagRegisterField startDet;
        private IFlagRegisterField stopDet;


        private IValueRegisterField icTxTl;

        private bool transmissionOngoing;

        private const ulong aliasSize = 0x1000;
        private const ulong xorAliasOffset = 0x1000;
        private const ulong setAliasOffset = 0x2000;
        private const ulong clearAliasOffset = 0x3000;

        private const int icTxBufferDepth = 16;
        private const int icRxBufferDepth = 16;


        private IManagedThread executionThread;

        private State state;
    };

}