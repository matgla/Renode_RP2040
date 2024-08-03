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
            CLK_GPOUT0_CTRL = 0x00,
            CLK_GPOUT0_DIV = 0x04,
            CLK_GPOUT0_SELECTED = 0x08,
            CLK_GPOUT1_CTRL = 0x0c,
            CLK_GPOUT1_DIV = 0x10,
            CLK_GPOUT1_SELECTED = 0x14,
            CLK_GPOUT2_CTRL = 0x18,
            CLK_GPOUT2_DIV = 0x1c,
            CLK_GPOUT2_SELECTED = 0x20,
            CLK_GPOUT3_CTRL = 0x24,
            CLK_GPOUT3_DIV = 0x28,
            CLK_GPOUT3_SELECTED = 0x2c,
            CLK_REF_CTRL = 0x30,
            CLK_REF_DIV = 0x34,
            CLK_REF_SELECTED = 0x38,
            CLK_SYS_CTRL = 0x3c,
            CLK_SYS_DIV = 0x40,
            CLK_SYS_SELECTED = 0x44,
            CLK_PERI_CTRL = 0x48,
            CLK_PERI_SELECTED = 0x50,
            CLK_USB_CTRL = 0x54,
            CLK_USB_DIV = 0x58,
            CLK_USB_SELECTED = 0x5c,
            CLK_ADC_CTRL = 0x60,
            CLK_ADC_DIV = 0x64,
            CLK_ADC_SELECTED = 0x68,
            CLK_RTC_CTRL = 0x6c,
            CLK_RTC_DIV = 0x70,
            CLK_RTC_SELECTED = 0x74,
            CLK_SYS_RESUS_CTRL = 0x78,
            CLK_SYS_RESUS_STATUS = 0x7c,
            FC0_REF_KHZ = 0x80,
            FC0_MIN_KHZ = 0x84,
            FC0_MAX_KHZ = 0x88,
            FC0_DELAY = 0x8c,
            FC0_INTERVAL = 0x90,
            FC0_SRC = 0x94,
            FC0_STATUS = 0x98,
            FC0_RESULT = 0x9c,
            WAKE_EN0 = 0xa0,
            WAKE_EN1 = 0xa4,
            SLEEP_EN0 = 0xa8,
            SLEEP_EN1 = 0xac,
            ENABLED0 = 0xb0,
            ENABLED1 = 0xb4,
            INTR = 0xb8,
            INTE = 0xbc,
            INTF = 0xc0,
            INTS = 0xc4
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

        private enum PeriClockAuxSource
        {
            ClkSys = 0,
            ClkPllSys = 1,
            ClkPllUsb = 2,
            ClkXosc = 3,
            ClkRosc = 4,
            ClkGPin0 = 5,
            ClkGPin1 = 6
        }

        private enum UsbClockAuxSource
        {
            ClkPllUsb = 0,
            ClkPllSys = 1,
            ClkRosc = 2,
            ClkXosc = 3,
            ClkGPin0 = 4,
            ClkGPin1 = 5
        }

        public RP2040Clocks(Machine machine, RP2040XOSC xosc, RP2040ROSC rosc, RP2040PLL pll, RP2040PLL pllusb) : base(machine)
        {
            refClockSource = RefClockSource.ROSCClkSrcPh;
            refClockAuxSource = RefClockAuxSource.ClkSrcPllUsb;
            sysClockAuxSource = SysClockAuxSource.ClkSrcPllSys;
            sysClockSource = SysClockSource.ClkRef;
            periClockAuxSource = PeriClockAuxSource.ClkSys;
            periClockEnabled = false;
            periClockKill = false;
            this.xosc = xosc;
            this.rosc = rosc;
            this.pll = pll;
            this.pllusb = pllusb;
            frequencyCounterRunning = false;
            this.sysClockDividerInt = 1;

            this.refDiv = 1;
            this.sysDivInt = 1;
            this.sysDivFrac = 0;

            DefineRegisters();
            ChangeClock();
        }
        private Machine mach;

        private void ChangeClock()
        {
            ulong sourceFrequency = 0;
            this.Log(LogLevel.Noisy, "Reconfiguration of clocks with system clock: " + sysClockSource + ", system clock auxilary: " + sysClockAuxSource);

            if (sysClockSource == SysClockSource.ClkSrcClkSysAux)
            {
                switch (sysClockAuxSource)
                {
                    case SysClockAuxSource.ROSCClkSrc:
                        {
                            sourceFrequency = rosc.Frequency;
                            break;
                        }
                    case SysClockAuxSource.XOSCClkSrc:
                        {
                            sourceFrequency = xosc.Frequency;
                            break;
                        }
                    case SysClockAuxSource.ClkSrcPllSys:
                        {
                            sourceFrequency = pll.CalculateOutputFrequency(xosc.Frequency);
                            break;
                        }
                    case SysClockAuxSource.ClkSrcPllUsb:
                        {
                            sourceFrequency = pllusb.CalculateOutputFrequency(xosc.Frequency);
                            break;
                        }
                    case SysClockAuxSource.ClkSrcGPin0:
                    case SysClockAuxSource.ClkSrcGPin1:
                        {
                            this.Log(LogLevel.Warning, "GPIN clock source is not supported yet");
                            break;
                        }
                }
            }
            else
            {
                switch (refClockSource)
                {
                    case RefClockSource.ClkSrcClkRefAux:
                        switch (refClockAuxSource)
                        {
                            case RefClockAuxSource.ClkSrcGPin0:
                            case RefClockAuxSource.ClkSrcGPin1:
                                {
                                    this.Log(LogLevel.Warning, "GPIN clock source is not supported yet");
                                    break;
                                }
                            case RefClockAuxSource.ClkSrcPllUsb:
                                {
                                    sourceFrequency = pllusb.CalculateOutputFrequency(xosc.Frequency);
                                    break;
                                }
                        }
                        break;
                    case RefClockSource.XOSCClkSrc:
                        {
                            sourceFrequency = xosc.Frequency;
                            break;
                        }
                    case RefClockSource.ROSCClkSrcPh:
                        {
                            sourceFrequency = rosc.Frequency;
                            break;
                        }
                }
            }

            foreach (var c in machine.ClockSource.GetAllClockEntries())
            {
                if (c.LocalName == "systick")
                {
                    machine.ClockSource.ExchangeClockEntryWith(c.Handler, oldEntry => oldEntry.With(frequency: (uint)(sourceFrequency / sysClockDividerInt)));
                    this.Log(LogLevel.Debug, "Changing system clock with base: " + sourceFrequency + " / " + sysClockDividerInt);
                }
                // Recalculate SPI
            }

            foreach (var c in machine.GetSystemBus(this).GetCPUs())
            {
                if (c.GetName().Contains("piocpu"))
                {
                    (c as BaseCPU).PerformanceInMips = (uint)(sourceFrequency / 1000000 / sysClockDividerInt);
                    this.Log(LogLevel.Debug, "Changing performance of: " + c.GetName() + ", to: " + (c as BaseCPU).PerformanceInMips);
                }
            }
        }

        private void UpdatePeripheralsClock()
        {
        }

        private void UpdateRefClock()
        {
        }

        private void UpdateSysClock()
        {
        }

        private void UpdateUsbClock()
        {
        }

        private void UpdateRtcClock()
        {
        }

        private void UpdateAdcClock()
        {
        }

        private void DefineRegisters()
        {
            Registers.CLK_GPOUT0_CTRL.Define(this)
                .WithReservedBits(0, 5)
                .WithValueField(5, 4, name: "AUXSRC")
                .WithReservedBits(9, 1)
                .WithTaggedFlag("KILL", 10)
                .WithTaggedFlag("ENABLE", 11)
                .WithTaggedFlag("DC50", 12)
                .WithReservedBits(13, 3)
                .WithValueField(16, 2, name: "PHASE")
                .WithReservedBits(18, 2)
                .WithTaggedFlag("NUDGE", 20)
                .WithReservedBits(21, 11);

            Registers.CLK_GPOUT0_DIV.Define(this)
                .WithValueField(0, 8, name: "FRAC")
                .WithValueField(8, 24, name: "INT");

            Registers.CLK_GPOUT0_SELECTED.Define(this)
                .WithValueField(0, 32, name: "SELECTED");

            Registers.CLK_GPOUT1_CTRL.Define(this)
                .WithReservedBits(0, 5)
                .WithValueField(5, 4, name: "AUXSRC")
                .WithReservedBits(9, 1)
                .WithTaggedFlag("KILL", 10)
                .WithTaggedFlag("ENABLE", 11)
                .WithTaggedFlag("DC50", 12)
                .WithReservedBits(13, 3)
                .WithValueField(16, 2, name: "PHASE")
                .WithReservedBits(18, 2)
                .WithTaggedFlag("NUDGE", 20)
                .WithReservedBits(21, 11);

            Registers.CLK_GPOUT1_DIV.Define(this)
                .WithValueField(0, 8, name: "FRAC")
                .WithValueField(8, 24, name: "INT");

            Registers.CLK_GPOUT1_SELECTED.Define(this)
                .WithValueField(0, 32, name: "SELECTED");

            Registers.CLK_GPOUT2_CTRL.Define(this)
                .WithReservedBits(0, 5)
                .WithValueField(5, 4, name: "AUXSRC")
                .WithReservedBits(9, 1)
                .WithTaggedFlag("KILL", 10)
                .WithTaggedFlag("ENABLE", 11)
                .WithTaggedFlag("DC50", 12)
                .WithReservedBits(13, 3)
                .WithValueField(16, 2, name: "PHASE")
                .WithReservedBits(18, 2)
                .WithTaggedFlag("NUDGE", 20)
                .WithReservedBits(21, 11);

            Registers.CLK_GPOUT2_DIV.Define(this)
                .WithValueField(0, 8, name: "FRAC")
                .WithValueField(8, 24, name: "INT");

            Registers.CLK_GPOUT2_SELECTED.Define(this)
                .WithValueField(0, 32, name: "SELECTED");

            Registers.CLK_GPOUT3_CTRL.Define(this)
                .WithReservedBits(0, 5)
                .WithValueField(5, 4, name: "AUXSRC")
                .WithReservedBits(9, 1)
                .WithTaggedFlag("KILL", 10)
                .WithTaggedFlag("ENABLE", 11)
                .WithTaggedFlag("DC50", 12)
                .WithReservedBits(13, 3)
                .WithValueField(16, 2, name: "PHASE")
                .WithReservedBits(18, 2)
                .WithTaggedFlag("NUDGE", 20)
                .WithReservedBits(21, 11);

            Registers.CLK_GPOUT3_DIV.Define(this)
                .WithValueField(0, 8, name: "FRAC")
                .WithValueField(8, 24, name: "INT");

            Registers.CLK_GPOUT3_SELECTED.Define(this)
                .WithValueField(0, 32, name: "SELECTED");

            Registers.CLK_REF_CTRL.Define(this)
                .WithValueField(0, 2, valueProviderCallback: _ => (ulong)refClockSource,
                    writeCallback: (_, value) =>
                    {
                        refClockSource = (RefClockSource)value;
                        UpdateRefClock();
                    },
                    name: "CLK_REF_CTRL")
                .WithReservedBits(2, 3)
                .WithValueField(5, 2, valueProviderCallback: _ => (ulong)refClockAuxSource,
                    writeCallback: (_, value) =>
                    {
                        refClockAuxSource = (RefClockAuxSource)value;
                        UpdateRefClock();
                    },
                    name: "CLK_AUX_CTRL")
                .WithReservedBits(7, 25);

            Registers.CLK_REF_DIV.Define(this)
                .WithReservedBits(0, 8)
                .WithValueField(8, 2, valueProviderCallback: _ => refDiv == 0 ? 1ul << 16 : refDiv,
                    writeCallback: (_, value) =>
                    {
                        refDiv = (byte)value;
                        UpdateRefClock();
                    },
                    name: "INT")
                .WithReservedBits(10, 22);

            Registers.CLK_REF_SELECTED.Define(this)
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ => (ulong)(1 << (int)refClockSource),
                    name: "CLK_REF_SELECTED");

            Registers.CLK_SYS_CTRL.Define(this)
                .WithValueField(0, 1, valueProviderCallback: _ => (ulong)sysClockSource,
                    writeCallback: (_, value) =>
                    {
                        sysClockSource = (SysClockSource)value;
                        UpdateSysClock();
                    },
                    name: "CLK_SYS_CTRL_SRC")
                .WithReservedBits(1, 4)
                .WithValueField(5, 3, valueProviderCallback: _ => (ulong)sysClockAuxSource,
                    writeCallback: (_, value) =>
                    {
                        sysClockAuxSource = (SysClockAuxSource)value;
                        UpdateSysClock();
                    },
                    name: "CLK_SYS_CTRL_AUXSRC")
                .WithReservedBits(8, 24);

            Registers.CLK_SYS_SELECTED.Define(this)
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ => (ulong)(1 << (int)sysClockSource),
                    name: "CLK_SYS_SELECTED");

            Registers.CLK_SYS_DIV.Define(this)
                .WithValueField(0, 8, valueProviderCallback: _ => sysDivFrac,
                    writeCallback: (_, value) =>
                    {
                        sysDivFrac = (byte)value;
                        UpdateSysClock();
                    },
                    name: "FRAC")
                .WithValueField(8, 24, valueProviderCallback: _ => sysDivInt,
                    writeCallback: (_, value) =>
                    {
                        sysDivInt = (uint)value;
                        UpdateSysClock();
                    },
                    name: "INT");

            Registers.CLK_SYS_RESUS_CTRL.Define(this)
                .WithValueField(0, 8, valueProviderCallback: _ => (ulong)timeout,
                    writeCallback: (_, value) => timeout = (long)value,
                    name: "CLK_SYS_RESUS_CTRL_TIMEOUT")
                .WithFlag(8, valueProviderCallback: _ => resusEnable,
                    writeCallback: (_, value) => resusEnable = value,
                    name: "CLK_SYS_RESUS_CTRL_ENABLE")
                .WithReservedBits(9, 3)
                .WithTaggedFlag("CLK_SYS_RESUS_CTRL_FORCE", 12)
                .WithReservedBits(13, 3)
                .WithTaggedFlag("CLK_SYS_RESUS_CTRL_CLEAR", 16)
                .WithReservedBits(17, 15);

            Registers.CLK_PERI_CTRL.Define(this)
                .WithReservedBits(0, 5)
                .WithValueField(5, 3, valueProviderCallback: _ => (ulong)periClockAuxSource,
                    writeCallback: (_, value) =>
                    {
                        periClockAuxSource = (PeriClockAuxSource)value;
                        UpdatePeripheralsClock();
                    },
                    name: "AUXSRC")
                .WithReservedBits(8, 2)
                .WithFlag(10, valueProviderCallback: _ => periClockKill,
                    writeCallback: (_, value) =>
                    {
                        periClockKill = value;
                        if (value)
                        {
                            periClockEnabled = false;
                            UpdatePeripheralsClock();
                        }
                    })
                .WithFlag(11, valueProviderCallback: _ => periClockEnabled,
                    writeCallback: (_, value) =>
                    {
                        periClockEnabled = value;
                        UpdatePeripheralsClock();
                    }, name: "ENABLE")
                .WithReservedBits(12, 20);

            Registers.CLK_PERI_SELECTED.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (ulong)(1 << (int)periClockAuxSource),
                    name: "SELECTED");

            Registers.CLK_USB_CTRL.Define(this)
                .WithReservedBits(0, 5)
                .WithValueField(5, 3, valueProviderCallback: _ => (ulong)usbClockAuxSource,
                    writeCallback: (_, value) =>
                    {
                        usbClockAuxSource = (UsbClockAuxSource)value;
                        UpdateUsbClock();
                    }, name: "AUXSRC")
                .WithReservedBits(8, 2)
                .WithFlag(10, valueProviderCallback: _ => usbClockKill,
                    writeCallback: (_, value) =>
                    {
                        usbClockKill = value;
                        if (value)
                        {
                            usbClockEnabled = false;
                        }
                        UpdateUsbClock();
                    }, name: "KILL")
                .WithFlag(11, valueProviderCallback: _ => usbClockEnabled,
                    writeCallback: (_, value) =>
                    {
                        usbClockEnabled = value;
                        UpdateUsbClock();
                    }, name: "ENABLE")
                .WithReservedBits(12, 4)
                .WithValueField(16, 2, name: "PHASE")
                .WithReservedBits(18, 2)
                .WithTaggedFlag("NUDGE", 20)
                .WithReservedBits(21, 11);



            Registers.FC0_STATUS.Define(this)
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
                                frequencyCounter = pll.CalculateOutputFrequency(xosc.Frequency) / 1000;
                                break;
                            }
                        case 2:
                            {
                                frequencyCounter = pllusb.CalculateOutputFrequency(xosc.Frequency) / 1000;
                                break;
                            }
                        case 3:
                            {
                                frequencyCounter = rosc.Frequency / 1000;
                                break;
                            }
                        case 4:
                            {
                                frequencyCounter = rosc.Frequency / 1000;
                                break;
                            }
                        case 5:
                            {
                                frequencyCounter = xosc.Frequency / 1000;
                                break;
                            }
                        case 6:
                            {
                                // GPIN not supported
                                frequencyCounter = 0;
                                break;
                            }
                        case 7:
                            {
                                // GPIN not supported
                                frequencyCounter = 0;
                                break;
                            }
                        case 8:
                            {
                                frequencyCounter = 8;
                                break;
                            }
                        case 9:
                            {
                                frequencyCounter = (ulong)(machine.ClockSource.GetAllClockEntries().First().Frequency / 1000);
                                break;
                            }
                        case 10:
                            {
                                frequencyCounter = (ulong)(machine.ClockSource.GetAllClockEntries().First().Frequency / 1000);
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
        private PeriClockAuxSource periClockAuxSource;
        private UsbClockAuxSource usbClockAuxSource;

        private bool periClockEnabled;
        private bool periClockKill;
        private bool usbClockEnabled;
        private bool usbClockKill;


        private long timeout;
        private bool resusEnable;
        private bool frequencyCounterRunning;
        private uint sysClockDividerFrac;
        private uint sysClockDividerInt;
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
        private RP2040PLL pll;
        private RP2040PLL pllusb;
        private byte refDiv;
        private byte sysDivFrac;
        private uint sysDivInt;
    }
}
