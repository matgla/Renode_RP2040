/**
 * rp2040_clocks.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Collections.Generic;
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

        private enum AdcClockAuxSource
        {
            ClkPllUsb = 0,
            ClkPllSys = 1,
            ClkRosc = 2,
            ClkXosc = 3,
            ClkGPin0 = 4,
            CLKGPin1 = 5
        }

        private enum RtcClockAuxSource
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
            usbClockAuxSource = UsbClockAuxSource.ClkPllUsb;
            adcClockAuxSource = AdcClockAuxSource.ClkPllUsb;
            rtcClockAuxSource = RtcClockAuxSource.ClkPllUsb;
            periClockEnabled = false;
            periClockKill = false;
            usbClockEnabled = false;
            usbClockKill = false;
            adcClockEnabled = false;
            adcClockKill = false;
            rtcClockEnabled = false;
            rtcClockKill = false;

            this.xosc = xosc;
            this.rosc = rosc;
            this.pll = pll;
            this.pllusb = pllusb;

            frequencyCounterRefFreq = 0;
            frequencyCounterMinFreq = 0;
            frequencyCounterMaxFreq = 0x1fffffff;
            frequencyCounter = 0;
            frequencyCounterSource = 0;

            this.sysDivInt = 1;
            this.sysDivFrac = 0;
            this.refDiv = 1;
            this.sysDivInt = 1;
            this.sysDivFrac = 0;
            this.usbDiv = 1;
            this.adcDiv = 1;
            this.rtcDivFrac = 0;
            this.rtcDivInt = 1;

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
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ => 1,
                    name: "SELECTED");

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
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ => 1,
                 name: "SELECTED");

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
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ => 1,
                    name: "SELECTED");

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
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ => 1,
                    name: "SELECTED");

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
                .WithValueField(8, 2, valueProviderCallback: _ => refDiv,
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
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 1,
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

            Registers.CLK_USB_DIV.Define(this)
                .WithReservedBits(0, 8)
                .WithValueField(8, 2, valueProviderCallback: _ => usbDiv,
                    writeCallback: (_, value) =>
                    {
                        usbDiv = (byte)value;
                        UpdateUsbClock();
                    }, name: "INT")
                .WithReservedBits(10, 22);

            Registers.CLK_USB_SELECTED.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 1,
                    name: "SELECTED");

            Registers.CLK_ADC_CTRL.Define(this)
                .WithReservedBits(0, 5)
                .WithValueField(5, 3, valueProviderCallback: _ => (ulong)adcClockAuxSource,
                    writeCallback: (_, value) =>
                    {
                        adcClockAuxSource = (AdcClockAuxSource)value;
                        UpdateAdcClock();
                    }, name: "AUXSRC")
                .WithReservedBits(8, 2)
                .WithFlag(10, valueProviderCallback: _ => adcClockKill,
                    writeCallback: (_, value) =>
                    {
                        adcClockKill = value;
                        if (value)
                        {
                            adcClockEnabled = false;
                            UpdateAdcClock();
                        }
                    }, name: "KILL")
                .WithFlag(11, valueProviderCallback: _ => adcClockEnabled,
                    writeCallback: (_, value) =>
                    {
                        adcClockEnabled = value;
                        UpdateAdcClock();
                    }, name: "ENABLE")
                .WithReservedBits(12, 4)
                .WithValueField(16, 2, name: "PHASE")
                .WithReservedBits(18, 2)
                .WithTaggedFlag("NUDGE", 20)
                .WithReservedBits(21, 11);

            Registers.CLK_ADC_DIV.Define(this)
                .WithReservedBits(0, 8)
                .WithValueField(8, 2, valueProviderCallback: _ => adcDiv,
                    writeCallback: (_, value) =>
                    {
                        adcDiv = (byte)value;
                        UpdateAdcClock();
                    }, name: "INT")
                .WithReservedBits(10, 22);

            Registers.CLK_ADC_SELECTED.Define(this)
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ => 1,
                    name: "SELECTED");

            Registers.CLK_RTC_CTRL.Define(this)
                .WithReservedBits(0, 5)
                .WithValueField(5, 3, valueProviderCallback: _ => (ulong)rtcClockAuxSource,
                    writeCallback: (_, value) =>
                    {
                        rtcClockAuxSource = (RtcClockAuxSource)value;
                        UpdateRtcClock();
                    }, name: "AUXSRC")
                .WithReservedBits(8, 2)
                .WithFlag(10, valueProviderCallback: _ => rtcClockKill,
                    writeCallback: (_, value) =>
                    {
                        rtcClockKill = value;
                        if (value)
                        {
                            rtcClockEnabled = false;
                            UpdateRtcClock();
                        }
                    }, name: "KILL")
                .WithFlag(11, valueProviderCallback: _ => rtcClockEnabled,
                    writeCallback: (_, value) =>
                    {
                        rtcClockEnabled = value;
                        UpdateRtcClock();
                    }, name: "ENABLE")
                .WithReservedBits(12, 4)
                .WithValueField(16, 2, name: "PHASE")
                .WithReservedBits(18, 2)
                .WithTaggedFlag("NUDGE", 20)
                .WithReservedBits(21, 11);

            Registers.CLK_RTC_DIV.Define(this)
                .WithValueField(0, 8, valueProviderCallback: _ => rtcDivFrac,
                    writeCallback: (_, value) =>
                    {
                        rtcDivFrac = (byte)value;
                        UpdateRtcClock();
                    })
                .WithValueField(8, 24, valueProviderCallback: _ => rtcDivInt,
                    writeCallback: (_, value) =>
                    {
                        rtcDivInt = (uint)value;
                        UpdateRtcClock();
                    }, name: "INT");

            Registers.CLK_RTC_SELECTED.Define(this)
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ => 1,
                    name: "SELECTED");


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

            Registers.CLK_SYS_RESUS_STATUS.Define(this)
                .WithTaggedFlag("RESUSSED", 0)
                .WithReservedBits(1, 31);

            Registers.FC0_REF_KHZ.Define(this)
                .WithValueField(0, 20, valueProviderCallback: _ => frequencyCounterRefFreq,
                    writeCallback: (_, value) => frequencyCounterRefFreq = (uint)value,
                    name: "REF_KHZ")
                .WithReservedBits(20, 12);

            Registers.FC0_MIN_KHZ.Define(this)
                .WithValueField(0, 25, valueProviderCallback: _ => frequencyCounterMinFreq,
                    writeCallback: (_, value) => frequencyCounterMinFreq = (uint)value,
                    name: "MIN_KHZ")
                .WithReservedBits(25, 7);

            Registers.FC0_MAX_KHZ.Define(this)
                .WithValueField(0, 25, valueProviderCallback: _ => frequencyCounterMaxFreq,
                    writeCallback: (_, value) => frequencyCounterMaxFreq = (uint)value,
                    name: "MAX_KHZ")
                .WithReservedBits(25, 7);

            Registers.FC0_DELAY.Define(this)
                .WithValueField(0, 3, name: "DELAY")
                .WithReservedBits(3, 29);

            Registers.FC0_INTERVAL.Define(this)
                .WithValueField(0, 4, name: "INTERVAL")
                .WithReservedBits(4, 28);

            Registers.FC0_SRC.Define(this)
                .WithValueField(0, 8, valueProviderCallback: _ =>
                {
                    return frequencyCounterSource;
                }, writeCallback: (_, value) =>
                {
                    frequencyCounterSource = (byte)(value);
                    switch (value)
                    {
                        case 0:
                            {
                                frequencyCounter = 0;
                                break;
                            }
                        case 1:
                            {
                                frequencyCounter = ((ulong)pll.CalculateOutputFrequency(xosc.Frequency) << 5) / 1000;
                                break;
                            }
                        case 2:
                            {
                                frequencyCounter = ((ulong)pllusb.CalculateOutputFrequency(xosc.Frequency) << 5) / 1000;
                                break;
                            }
                        case 3:
                            {
                                frequencyCounter = ((ulong)rosc.Frequency << 5) / 1000;
                                break;
                            }
                        case 4:
                            {
                                frequencyCounter = ((ulong)rosc.Frequency << 5) / 1000;
                                break;
                            }
                        case 5:
                            {
                                frequencyCounter = ((ulong)xosc.Frequency << 5) / 1000;
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
                                frequencyCounter = ((ulong)CalculateRefFrequency() << 5) / 1000;
                                break;
                            }
                        case 9:
                            {
                                frequencyCounter = ((ulong)CalculateSysFrequency() << 5) / 1000;
                                break;
                            }
                        case 10:
                            {
                                frequencyCounter = ((ulong)CalculatePeriFrequency() << 5) / 1000;
                                break;
                            }
                        case 11:
                            {
                                frequencyCounter = ((ulong)CalculateUsbFrequency() << 5) / 1000;
                                break;
                            }
                        case 12:
                            {
                                frequencyCounter = ((ulong)CalculateAdcFrequency() << 5) / 1000;
                                break;
                            }
                        case 13:
                            {
                                frequencyCounter = ((ulong)CalculateRtcFrequency() << 5) / 1000;
                                break;
                            }
                    }
                }, name: "KHZ")
                .WithReservedBits(8, 24);

            Registers.FC0_STATUS.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ =>
                    (frequencyCounter >= frequencyCounterMinFreq && frequencyCounter <= frequencyCounterMaxFreq),
                    name: "PASS")
                .WithReservedBits(1, 3)
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => true, name: "DONE")
                .WithReservedBits(5, 3)
                .WithTaggedFlag("RUNNING", 8)
                .WithReservedBits(9, 3)
                .WithTaggedFlag("WAITING", 12)
                .WithReservedBits(13, 3)
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ =>
                    (frequencyCounter <= frequencyCounterMinFreq || frequencyCounter >= frequencyCounterMaxFreq),
                    name: "FAIL")
                .WithReservedBits(17, 3)
                .WithFlag(20, FieldMode.Read, valueProviderCallback: _ => frequencyCounter < frequencyCounterMinFreq,
                    name: "SLOW")
                .WithReservedBits(21, 3)
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => frequencyCounter > frequencyCounterMaxFreq,
                    name: "FAST")
                .WithReservedBits(25, 3)
                .WithTaggedFlag("DIED", 28)
                .WithReservedBits(29, 3);

            Registers.FC0_RESULT.Define(this)
                .WithValueField(0, 5, FieldMode.Read, valueProviderCallback: _ => frequencyCounter & 0x1f, name: "FRAC")
                .WithValueField(5, 25, FieldMode.Read, valueProviderCallback: _ =>
                {
                    return frequencyCounter >> 5;
                }, name: "KHZ")
                .WithReservedBits(30, 2);

            Registers.WAKE_EN0.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => 0xffffffff, name: "WAKE_EN0");

            Registers.WAKE_EN1.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => 0xffffffff, name: "WAKE_EN1");

            Registers.SLEEP_EN0.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => 0xffffffff, name: "SLEEP_EN0");

            Registers.SLEEP_EN1.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => 0xffffffff, name: "SLEEP_EN1");

            // this probably can be implemented, but not needed now
            Registers.ENABLED0.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 0xffffffff,
                name: "ENABLED0");

            // same there
            Registers.ENABLED1.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 0xffffffff,
                name: "ENABLED1");

            Registers.INTR.Define(this)
                .WithTaggedFlag("CLK_SYS_RESUS", 0)
                .WithReservedBits(1, 31);

            Registers.INTE.Define(this)
                .WithTaggedFlag("CLK_SYS_RESUS", 0)
                .WithReservedBits(1, 31);

            Registers.INTF.Define(this)
                .WithTaggedFlag("CLK_SYS_RESUS", 0)
                .WithReservedBits(1, 31);

            Registers.INTS.Define(this)
                .WithTaggedFlag("CLK_SYS_RESUS", 0)
                .WithReservedBits(1, 31);

        }

        public void OnPeripheralChange(Action<long> action)
        {
            onPeriChange.Add(action);
        }

        public void OnAdcChange(Action<long> action)
        {
            onAdcChange.Add(action);
        }

        public void OnRtcChange(Action<long> action)
        {
            onRtcChange.Add(action);
        }

        public void OnUsbChange(Action<long> action)
        {
            onUsbChange.Add(action);
        }

        public long Size { get { return 0x1000; } }

        private RefClockSource refClockSource;
        private RefClockAuxSource refClockAuxSource;
        private byte refDiv;

        private SysClockAuxSource sysClockAuxSource;
        private SysClockSource sysClockSource;
        private byte sysDivFrac;
        private uint sysDivInt;

        private PeriClockAuxSource periClockAuxSource;
        private bool periClockEnabled;
        private bool periClockKill;
        private List<Action<long>> onPeriChange;

        private UsbClockAuxSource usbClockAuxSource;
        private bool usbClockEnabled;
        private bool usbClockKill;
        private byte usbDiv;
        private List<Action<long>> onUsbChange;

        private AdcClockAuxSource adcClockAuxSource;
        private bool adcClockEnabled;
        private bool adcClockKill;
        private byte adcDiv;
        private List<Action<long>> onAdcChange;

        private RtcClockAuxSource rtcClockAuxSource;
        private bool rtcClockEnabled;
        private bool rtcClockKill;
        private byte rtcDivFrac;
        private uint rtcDivInt;
        private List<Action<long>> onRtcChange;


        private uint frequencyCounterRefFreq;
        private uint frequencyCounterMinFreq;
        private uint frequencyCounterMaxFreq;
        private ulong frequencyCounter;
        private byte frequencyCounterSource;

        private long timeout;
        private bool resusEnable;
        private RP2040XOSC xosc;
        private RP2040ROSC rosc;
        private RP2040PLL pll;
        private RP2040PLL pllusb;
    }
}
