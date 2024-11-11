/**
 * rp2040_i2c.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Collections.Generic;
using Antmicro.OptionsParser;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.GPIOPort;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Utilities.Collections;
using Lucene.Net.Search;


namespace Antmicro.Renode.Peripherals.I2C
{

    class RP2040I2C : SimpleContainer<II2CPeripheral>, II2CPeripheral, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RP2040I2C(IMachine machine, RP2040Clocks clocks, ulong address, int id, RP2040GPIO gpio) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            txFifo = new CircularBuffer<UInt16>(16);
            rxFifo = new CircularBuffer<UInt16>(16);
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
 
            Reset();
        }

        public override void Reset()
        {
            txFifo.Clear();
            rxFifo.Clear();
            sdaPins.Clear();
            sclPins.Clear();
            currentSlave = null;

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
            dat.Value = 0x0;
            cmd.Value = false;
            stop.Value = false;
            restart.Value = false;
            firstDataByte.Value = false;
            icSsSclHcnt.Value = 0x0028;
            icSsSclLcnt.Value = 0x002f;
            icFsSclHcnt.Value = 0x0006;
            icFsSclLcnt.Value = 0x000d;
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
                .WithValueField(0, 8, out dat, 
                    writeCallback: (_, value) => txFifo.Enqueue((UInt16)dat.Value),
                    valueProviderCallback: _ => {
                        if (!rxFifo.TryDequeue(out var result))
                        {
                            return 0;
                        }
                        return result;
                    }, name: "DAT")
                .WithFlag(8, out cmd, name: "CMD")
                .WithFlag(9, out stop, name: "STOP")
                .WithFlag(10, out restart, name: "RESTART")
                .WithFlag(11, out firstDataByte, FieldMode.Read, name: "FIRST_DATA_BYTE")
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
                this.Log(LogLevel.Error, "Unsupported I2C id: {0}", id);
            }
        }
        private void StartTransfer()
        {
            this.Log(LogLevel.Noisy, "I2C starting new transfer for: 0x{0:X}", icTar.Value);
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

        private RP2040Clocks clocks;
        private RP2040GPIO gpio;
        private int id;
        private List<int> sclPins;
        private List<int> sdaPins;

        private CircularBuffer<UInt16> txFifo;
        private CircularBuffer<UInt16> rxFifo; 

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

        private IValueRegisterField dat;
        private IFlagRegisterField cmd;
        private IFlagRegisterField stop;
        private IFlagRegisterField restart;
        private IFlagRegisterField firstDataByte;

        private IValueRegisterField icSsSclHcnt;
        private IValueRegisterField icSsSclLcnt;
        private IValueRegisterField icFsSclHcnt;
        private IValueRegisterField icFsSclLcnt;

        private const ulong aliasSize = 0x1000;
        private const ulong xorAliasOffset = 0x1000;
        private const ulong setAliasOffset = 0x2000;
        private const ulong clearAliasOffset = 0x3000;
 
    };

}