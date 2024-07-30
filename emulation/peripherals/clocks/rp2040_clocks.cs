/**
 * rp2040_clocks.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */


using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{

    public class RP2040Clocks : BasicDoubleWordPeripheral, IKnownSize
    {
        private enum Registers
        {
            CLK_REF_CTRL = 0x30,
            CLK_REF_DIV = 0x34,
            CLK_REF_SELECTED = 0x38,
            CLK_SYS_CTRL = 0x3c,
            CLK_SYS_SELECTED = 0x44,
            CLK_SYS_RESUS_CTRL = 0x78,
            CLK_FC0_STATUS = 0x98,
            CLK_SYS_DIV = 0x40,
            FC0_RESULT = 0x9c,
            FC0_SRC = 0x94
        }

        private enum SysClockAuxSource
        {
            ClkSrcPllSys = 0,
            ClkSrcPllUsb = 1,
            ROSCClkSrc = 2,
            XOSCClkSrc = 3,
            ClkSrcGPin0 = 4,
            ClkSrcGPin1 = 5,
        }

        private enum SysClockSource
        {
            ClkRef = 0,
            ClkSrcClkSysAux = 1
        }
        private enum RefClockSource
        {
            ROSCClkSrcPh = 0,
            ClkSrcClkRefAux = 1,
            XOSCClkSrc = 2
        }

        private enum RefClockAuxSource
        {
            ClkSrcPllUsb = 0,
            ClkSrcGPin0 = 1,
            ClkSrcGPin1 = 2
        }

        public RP2040Clocks(Machine machine, RP2040XOSC xosc, RP2040ROSC rosc) : base(machine)
        {
            refClockSource = RefClockSource.ROSCClkSrcPh;
            refClockAuxSource = RefClockAuxSource.ClkSrcPllUsb;
            sysClockAuxSource = SysClockAuxSource.ClkSrcPllSys;
            sysClockSource = SysClockSource.ClkRef;
            this.xosc = xosc;
            this.rosc = rosc;
            frequencyCounterRunning = false;
            this.sysClockDividerInt = 1;
            this.defaultFrequency = xosc.Frequency;

            DefineRegisters();
            ChangeClock();
        }
        private Machine mach;

        private void ChangeClock()
        {
            foreach (var c in machine.ClockSource.GetAllClockEntries())
            {
                if (c.LocalName == "systick")
                {
                    machine.ClockSource.ExchangeClockEntryWith(c.Handler, oldEntry => oldEntry.With(frequency: (uint)(defaultFrequency / sysClockDividerInt)));
                    this.Log(LogLevel.Debug, "Changing system clock to: " + defaultFrequency / sysClockDividerInt);
                }
                // Recalculate SPI
            }

            foreach (var c in machine.GetSystemBus(this).GetCPUs())
            {
                if (c.GetName().Contains("piocpu"))
                {
                    (c as BaseCPU).PerformanceInMips = (uint)(defaultFrequency / 1000000 / sysClockDividerInt);
                    this.Log(LogLevel.Debug, "Changing performance of: " + c.GetName() + ", to: " + (c as BaseCPU).PerformanceInMips);
                }
            }
        }

        private void DefineRegisters()
        {
            Registers.CLK_REF_CTRL.Define(this)
                .WithValueField(0, 2, FieldMode.Write | FieldMode.Read,
                    writeCallback: (_, value) => refClockSource = (RefClockSource)value,
                    valueProviderCallback: _ => (ulong)refClockSource,
                    name: "CLK_REF_CTRL")
                .WithReservedBits(2, 3)
                .WithValueField(5, 2, FieldMode.Write | FieldMode.Read,
                    writeCallback: (_, value) => refClockAuxSource = (RefClockAuxSource)value,
                    valueProviderCallback: _ => (ulong)refClockAuxSource,
                    name: "CLK_AUX_CTRL")
                .WithReservedBits(7, 25);

            Registers.CLK_REF_SELECTED.Define(this)
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ => (ulong)(1 << (int)refClockSource),
                    name: "CLK_REF_SELECTED");

            Registers.CLK_SYS_RESUS_CTRL.Define(this)
                .WithValueField(0, 8, FieldMode.Write | FieldMode.Read,
                    writeCallback: (_, value) => timeout = (long)value,
                    valueProviderCallback: _ => (ulong)timeout,
                    name: "CLK_SYS_RESUS_CTRL_TIMEOUT")
                .WithFlag(8, FieldMode.Write | FieldMode.Read,
                    writeCallback: (_, value) => resusEnable = value,
                    valueProviderCallback: _ => resusEnable,
                    name: "CLK_SYS_RESUS_CTRL_ENABLE")
                .WithReservedBits(9, 3)
                .WithFlag(12, FieldMode.Write | FieldMode.Read,
                    valueProviderCallback: _ => false,
                    name: "CLK_SYS_RESUS_CTRL_FORCE")
                .WithReservedBits(13, 3)
                .WithFlag(16, FieldMode.Write | FieldMode.Read,
                    valueProviderCallback: _ => false,
                    name: "CLK_SYS_RESUS_CTRL_CLEAR")
                .WithReservedBits(17, 15);

            Registers.CLK_SYS_CTRL.Define(this)
                .WithValueField(0, 1, FieldMode.Write | FieldMode.Read,
                    writeCallback: (_, value) => sysClockSource = (SysClockSource)value,
                    valueProviderCallback: _ => (ulong)sysClockSource,
                    name: "CLK_SYS_CTRL_SRC")
                .WithReservedBits(1, 4)
                .WithValueField(5, 3, FieldMode.Write | FieldMode.Read,
                    writeCallback: (_, value) => sysClockAuxSource = (SysClockAuxSource)value,
                    valueProviderCallback: _ => (ulong)sysClockAuxSource,
                    name: "CLK_SYS_CTRL_AUXSRC")
                .WithReservedBits(8, 24);

            Registers.CLK_SYS_SELECTED.Define(this)
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ => (ulong)(1 << (int)sysClockSource),
                    name: "CLK_SYS_SELECTED");

            Registers.CLK_FC0_STATUS.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "PASS")
                .WithReservedBits(1, 3)
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => !frequencyCounterRunning, name: "DONE")
                .WithReservedBits(5, 3)
                .WithFlag(8, FieldMode.Read, valueProviderCallback: _ =>
                {
                    if (frequencyCounterRunning)
                    {
                        frequencyCounterRunning = false;
                    }
                    return frequencyCounterRunning;
                }, name: "RUNNING")
                .WithReservedBits(9, 3)
                .WithFlag(12, FieldMode.Read, valueProviderCallback: _ => false, name: "WAITING")
                .WithReservedBits(13, 3)
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => false, name: "FAIL")
                .WithReservedBits(17, 3)
                .WithFlag(20, FieldMode.Read, valueProviderCallback: _ => false, name: "SLOW")
                .WithReservedBits(21, 3)
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => false, name: "FAST")
                .WithReservedBits(25, 3)
                .WithFlag(28, FieldMode.Read, valueProviderCallback: _ => false, name: "DIED")
                .WithReservedBits(29, 3);

            Registers.CLK_SYS_DIV.Define(this)
                .WithValueField(0, 8, valueProviderCallback: _ => (ulong)sysClockDividerFrac,
                    writeCallback: (_, value) =>
                    {
                        sysClockDividerFrac = (uint)value;
                        ChangeClock();
                    }, name: "FRAC")
                .WithValueField(8, 24, valueProviderCallback: _ => (ulong)sysClockDividerInt,
                    writeCallback: (_, value) =>
                    {
                        sysClockDividerInt = (uint)value;
                        if (sysClockDividerInt == 0)
                        {
                            sysClockDividerInt = (1 << 16);
                        }
                        ChangeClock();
                    }, name: "INT");

            Registers.FC0_RESULT.Define(this)
                .WithValueField(0, 5, FieldMode.Read, valueProviderCallback: _ => 0, name: "FRAC")
                .WithValueField(5, 25, FieldMode.Read, valueProviderCallback: _ =>
                {
                    return frequencyCounter;
                }, name: "KHZ")
                .WithReservedBits(30, 2);

            Registers.FC0_SRC.Define(this)
                .WithValueField(0, 8, valueProviderCallback: _ =>
                {

                    return frequencyCounter;
                }, writeCallback: (_, value) =>
                {
                    frequencyCounterRunning = true;
                    switch (value)
                    {
                        case 0:
                            {
                                frequencyCounter = 0;
                                break;
                            }
                        case 1:
                            {
                                frequencyCounter = 1;
                                break;
                            }
                        case 2:
                            {
                                frequencyCounter = 2;
                                break;
                            }
                        case 3:
                            {
                                frequencyCounter = 3;
                                break;
                            }
                        case 4:
                            {
                                frequencyCounter = 4;
                                break;
                            }
                        case 5:
                            {
                                frequencyCounter = 5;
                                break;
                            }
                        case 6:
                            {
                                frequencyCounter = 6;
                                break;
                            }
                        case 7:
                            {
                                frequencyCounter = 7;
                                break;
                            }
                        case 8:
                            {
                                frequencyCounter = 8;
                                break;
                            }
                        case 9:
                            {
                                frequencyCounter = (ulong)machine.ClockSource.GetAllClockEntries().First().Frequency / 1000;
                                break;
                            }
                        case 10:
                            {
                                frequencyCounter = (ulong)machine.ClockSource.GetAllClockEntries().First().Frequency / 1000;
                                break;
                            }
                        case 11:
                            {
                                frequencyCounter = 11;
                                break;
                            }
                        case 12:
                            {
                                frequencyCounter = 12;
                                break;
                            }
                        case 13:
                            {
                                frequencyCounter = 13;
                                break;
                            }
                    }

                }, name: "KHZ")
                .WithReservedBits(8, 24);

        }

        public long Size { get { return 0x1000; } }

        private RefClockSource refClockSource;
        private RefClockAuxSource refClockAuxSource;
        private SysClockAuxSource sysClockAuxSource;
        private SysClockSource sysClockSource;
        private long timeout;
        private bool resusEnable;
        private bool frequencyCounterRunning;
        private uint sysClockDividerFrac;
        private uint sysClockDividerInt;
        private ulong defaultFrequency;
        private ulong pllSysFrequency;
        private ulong pllUsbFrequency;
        private ulong roscFrequency;
        private ulong usbFrequency;
        private ulong periFrequency;
        private ulong adcFrequency;
        private ulong rtcFrequency;
        private ulong frequencyCounter;
        private RP2040XOSC xosc;
        private RP2040ROSC rosc;
    }
}
