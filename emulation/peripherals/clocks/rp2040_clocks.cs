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
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Peripherals.GPIOPort;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class RP2040Clocks : RP2040PeripheralBase, IKnownSize
    {
        public RP2040Clocks(Machine machine, RP2040XOSC xosc, RP2040ROSC rosc, RP2040PLL pll, RP2040PLL pllusb, NVIC nvic0, NVIC nvic1, ulong address, RP2040GPIO gpio) : base(machine, address)
        {
            IRQ = new GPIO();
            gpio.SubscribeOnFunctionChange(UpdateGpioMapping);
            refClockSource = RefClockSource.ROSCClkSrcPh;
            refClockAuxSource = RefClockAuxSource.ClkSrcPllUsb;
            sysClockAuxSource = SysClockAuxSource.ClkSrcPllSys;
            sysClockSource = SysClockSource.ClkRef;
            periClockAuxSource = PeriClockAuxSource.ClkSys;
            usbClockAuxSource = UsbClockAuxSource.ClkPllUsb;
            adcClockAuxSource = AdcClockAuxSource.ClkPllUsb;
            rtcClockAuxSource = RtcClockAuxSource.ClkPllUsb;

            onSysClockChange = new List<Action<long>>();
            onRefClockChange = new List<Action<long>>();
            onPeriChange = new List<Action<long>>();
            onAdcChange = new List<Action<long>>();
            onUsbChange = new List<Action<long>>();
            onRtcChange = new List<Action<long>>();
            this.xosc = xosc;
            this.rosc = rosc;
            this.pll = pll;
            this.pllusb = pllusb;
            nvic = new NVIC[2];
            nvic[0] = nvic0;
            nvic[1] = nvic1;
            this.gpio = gpio;

            this.pll.RegisterClient(OnPllChanged);
            this.pllusb.RegisterClient(OnPllChanged);


            DefineRegisters();
            gpout = new GPOUTControl[numberOfGPOUTs];
            gpoutPins = new int[numberOfGPOUTs];
            for (int i = 0; i < numberOfGPOUTs; ++i)
            {
                gpout[i] = new GPOUTControl();
            }

            Reset();
        }

        public override void Reset()
        {
            periClockEnabled = false;
            periClockKill = false;
            usbClockEnabled = false;
            usbClockKill = false;
            adcClockEnabled = false;
            adcClockKill = false;
            rtcClockEnabled = false;
            rtcClockKill = false;
            frequencyCounterRefFreq = 0;
            frequencyCounterMinFreq = 0;
            frequencyCounterMaxFreq = 0x1fffffff;
            frequencyCounter = 0;
            frequencyCounterSource = 0;

            sysDivInt = 1;
            sysDivFrac = 0;
            refDiv = 1;
            usbDiv = 1;
            adcDiv = 1;
            rtcDivFrac = 0;
            rtcDivInt = 1;
            resused = false;
            resusIrqEnabled = false;
            resusIrqForced = false;
            resusForced = false;

            SystemClockFrequency = 0;
            PeripheralClockFrequency = 0;
            ReferenceClockFrequency = 0;
            UsbClockFrequency = 0;
            AdcClockFrequency = 0;
            RtcClockFrequency = 0;
            timeout = 0;
            resusEnable = false;

            for (int i = 0; i < numberOfGPOUTs; ++i)
            {
                gpoutPins[i] = -1;
                gpout[i].dividerIntegral = 1;
            }
            UpdateAllClocks();
        }

        private void UpdateGpioMapping(int id, RP2040GPIO.GpioFunction function)
        {
            this.Log(LogLevel.Info, "Update of GPIO mapping for pin: " + id + " function: " + function);
            switch (function)
            {
                case RP2040GPIO.GpioFunction.CLOCK_GPOUT0:
                    {
                        gpoutPins[0] = id;
                        break;
                    }
                case RP2040GPIO.GpioFunction.CLOCK_GPOUT1:
                    {
                        gpoutPins[1] = id;
                        break;
                    }
                case RP2040GPIO.GpioFunction.CLOCK_GPOUT2:
                    {
                        gpoutPins[2] = id;
                        break;
                    }
                case RP2040GPIO.GpioFunction.CLOCK_GPOUT3:
                    {
                        gpoutPins[3] = id;
                        break;
                    }
            }
        }

        public void OnSystemClockChange(Action<long> action)
        {
            onSysClockChange.Add(action);
        }

        public void OnRefClockChange(Action<long> action)
        {
            onRefClockChange.Add(action);
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

        public void UpdateAllClocks()
        {
            UpdateRefClock();
            UpdateSysClock();
            ReconfigureAllGpout();
        }

        private void ReconfigureAllGpout()
        {
            for (int i = 0; i < numberOfGPOUTs; ++i)
            {
                ReconfigureGpout(i);
            }
        }

        private void OnPllChanged()
        {
            UpdateAllClocks();
            ReconfigureAllGpout();
        }

        private ulong GetReferenceClockSourceFrequency()
        {
            switch (refClockSource)
            {
                case RefClockSource.ROSCClkSrcPh:
                    return rosc.Frequency;
                case RefClockSource.XOSCClkSrc:
                    return xosc.Frequency;
                case RefClockSource.ClkSrcClkRefAux:
                    {
                        switch (refClockAuxSource)
                        {
                            case RefClockAuxSource.ClkSrcPllUsb:
                                return pllusb.CalculateOutputFrequency(xosc.Frequency);
                            case RefClockAuxSource.ClkSrcGPin0:
                            case RefClockAuxSource.ClkSrcGPin1:
                                {
                                    this.Log(LogLevel.Error, "GPin is not supported");
                                    return 1;
                                }
                        }
                        this.Log(LogLevel.Error, "Unknown aux reference clock");
                        return 1;
                    }

            }

            this.Log(LogLevel.Error, "Unknown configuration for reference clock source");
            return 1;
        }



        private void UpdateRefClock()
        {
            ulong sourceFrequency = GetReferenceClockSourceFrequency();
            ulong integral = refDiv == 0 ? 1ul << 16 : refDiv;
            ReferenceClockFrequency = (uint)(sourceFrequency / integral);

            if (sysClockSource == SysClockSource.ClkRef)
            {
                UpdateSysClock();
            }
            ReconfigureAllGpout();
        }

        private ulong GetSystemClockSourceFrequency()
        {
            if (sysClockSource == SysClockSource.ClkSrcClkSysAux)
            {
                switch (sysClockAuxSource)
                {
                    case SysClockAuxSource.ROSCClkSrc:
                        {
                            return rosc.Frequency;
                        }
                    case SysClockAuxSource.XOSCClkSrc:
                        {
                            return xosc.Frequency;
                        }
                    case SysClockAuxSource.ClkSrcPllSys:
                        {
                            return pll.CalculateOutputFrequency(xosc.Frequency);
                        }
                    case SysClockAuxSource.ClkSrcPllUsb:
                        {
                            return pllusb.CalculateOutputFrequency(xosc.Frequency);
                        }
                    case SysClockAuxSource.ClkSrcGPin0:
                    case SysClockAuxSource.ClkSrcGPin1:
                        {
                            this.Log(LogLevel.Warning, "GPIN clock source is not supported yet");
                            return 1;
                        }
                }
            }
            else
            {
                return ReferenceClockFrequency;
            }
            this.Log(LogLevel.Error, "Unknown system clock source configuraton");
            return 1;
        }

        private void UpdateSysClock()
        {
            ulong sourceFrequency = GetSystemClockSourceFrequency();
            ulong integral = sysDivInt == 0 ? 1 << 16 : sysDivInt;
            uint newFrequency = (uint)((decimal)sourceFrequency / (integral + (decimal)sysDivFrac / 256));
            if (newFrequency == 0)
            {
                newFrequency = 1;
            }
            if (newFrequency != SystemClockFrequency)
            {
                SystemClockFrequency = newFrequency;

                foreach (var n in nvic)
                {
                    n.Frequency = SystemClockFrequency;
                }

                foreach (var c in machine.ClockSource.GetAllClockEntries())
                {
                    if (c.LocalName == "systick")
                    {
                        machine.ClockSource.ExchangeClockEntryWith(c.Handler, oldEntry => oldEntry.With(frequency: SystemClockFrequency));
                    }
                }

                foreach (var a in onSysClockChange)
                {
                    a(SystemClockFrequency);
                }

                if (periClockAuxSource == PeriClockAuxSource.ClkSys ||
                    periClockAuxSource == PeriClockAuxSource.ClkPllSys)
                {
                    UpdatePeripheralsClock();
                }
                if (usbClockAuxSource == UsbClockAuxSource.ClkPllSys)
                {
                    UpdateUsbClock();
                }
                if (adcClockAuxSource == AdcClockAuxSource.ClkPllSys)
                {
                    UpdateAdcClock();
                }
                if (rtcClockAuxSource == RtcClockAuxSource.ClkPllSys)
                {
                    UpdateRtcClock();
                }
            }

            // if clock is destroyed call resus action
            if (sysClockSource == SysClockSource.ClkSrcClkSysAux)
            {
                bool newResus = false;
                switch (sysClockAuxSource)
                {
                    case SysClockAuxSource.ClkSrcGPin1:
                    case SysClockAuxSource.ClkSrcGPin0:
                        {
                            return;
                        }
                    case SysClockAuxSource.ClkSrcPllSys:
                        {
                            newResus = !pll.PllEnabled();
                            break;
                        }
                    case SysClockAuxSource.ClkSrcPllUsb:
                        {
                            newResus = !pllusb.PllEnabled();
                            break;
                        }

                    case SysClockAuxSource.XOSCClkSrc:
                        {
                            newResus = !xosc.Enabled;
                            break;
                        }
                    case SysClockAuxSource.ROSCClkSrc:
                        {
                            newResus = !rosc.Enabled;
                            break;
                        }
                }

                if (resusEnable)
                {
                    if (newResus && !resused)
                    {
                        sysClockSource = SysClockSource.ClkRef;
                        resused = newResus;
                        UpdateSysClock();
                        if (resusIrqEnabled)
                        {
                            IRQ.Set(true);
                        }
                    }
                }
            }
            ReconfigureAllGpout();
        }

        private ulong GetPeripheralSourceFrequency()
        {
            switch (periClockAuxSource)
            {
                case PeriClockAuxSource.ClkSys:
                    return SystemClockFrequency;
                case PeriClockAuxSource.ClkPllSys:
                    return pll.CalculateOutputFrequency(xosc.Frequency);
                case PeriClockAuxSource.ClkPllUsb:
                    return pllusb.CalculateOutputFrequency(xosc.Frequency);
                case PeriClockAuxSource.ClkXosc:
                    return xosc.Frequency;
                case PeriClockAuxSource.ClkRosc:
                    return rosc.Frequency;
                case PeriClockAuxSource.ClkGPin0:
                case PeriClockAuxSource.ClkGPin1:
                    {
                        this.Log(LogLevel.Error, "GPin is not supported");
                        return 1;
                    }
            }
            this.Log(LogLevel.Error, "Unknown configuration for peripheral aux source");
            return 1;
        }

        private void UpdatePeripheralsClock()
        {
            uint newFrequency = (uint)GetPeripheralSourceFrequency();
            if (newFrequency != PeripheralClockFrequency)
            {
                PeripheralClockFrequency = newFrequency;
                foreach (var a in onPeriChange)
                {
                    a(PeripheralClockFrequency);
                }
            }
            ReconfigureAllGpout();
        }

        private ulong GetUsbClockSourceFrequency()
        {
            switch (usbClockAuxSource)
            {
                case UsbClockAuxSource.ClkPllUsb:
                    return pllusb.CalculateOutputFrequency(xosc.Frequency);
                case UsbClockAuxSource.ClkPllSys:
                    return pll.CalculateOutputFrequency(xosc.Frequency);
                case UsbClockAuxSource.ClkRosc:
                    return rosc.Frequency;
                case UsbClockAuxSource.ClkXosc:
                    return xosc.Frequency;
                case UsbClockAuxSource.ClkGPin0:
                case UsbClockAuxSource.ClkGPin1:
                    {
                        this.Log(LogLevel.Error, "GPin clock source not supported");
                        return 1;
                    }
            }
            this.Log(LogLevel.Error, "Unknown configuration for USB aux source");
            return 1;
        }

        private void UpdateUsbClock()
        {
            ulong integral = usbDiv == 0 ? 1ul << 16 : usbDiv;
            UsbClockFrequency = (uint)(GetUsbClockSourceFrequency() / integral);

            foreach (var a in onUsbChange)
            {
                a(UsbClockFrequency);
            }
            ReconfigureAllGpout();
        }

        private ulong GetRtcClockSourceFrequency()
        {
            switch (rtcClockAuxSource)
            {
                case RtcClockAuxSource.ClkPllUsb:
                    return pllusb.CalculateOutputFrequency(xosc.Frequency);
                case RtcClockAuxSource.ClkPllSys:
                    return pll.CalculateOutputFrequency(xosc.Frequency);
                case RtcClockAuxSource.ClkRosc:
                    return rosc.Frequency;
                case RtcClockAuxSource.ClkXosc:
                    return xosc.Frequency;
                case RtcClockAuxSource.ClkGPin0:
                case RtcClockAuxSource.ClkGPin1:
                    {
                        this.Log(LogLevel.Error, "GPin clock source not supported");
                        return 1;
                    }
            }
            this.Log(LogLevel.Error, "Unknown configuration for RTC aux source");
            return 1;
        }

        private void UpdateRtcClock()
        {
            ulong integral = rtcDivInt == 0 ? 1ul << 16 : rtcDivInt;
            RtcClockFrequency = (uint)(GetRtcClockSourceFrequency() / ((decimal)integral + (decimal)rtcDivFrac / 256));

            foreach (var a in onRtcChange)
            {
                a(RtcClockFrequency);
            }
            ReconfigureAllGpout();
        }

        private ulong GetAdcClockSourceFrequency()
        {
            switch (adcClockAuxSource)
            {
                case AdcClockAuxSource.ClkPllUsb:
                    return pllusb.CalculateOutputFrequency(xosc.Frequency);
                case AdcClockAuxSource.ClkPllSys:
                    return pll.CalculateOutputFrequency(xosc.Frequency);
                case AdcClockAuxSource.ClkRosc:
                    return rosc.Frequency;
                case AdcClockAuxSource.ClkXosc:
                    return xosc.Frequency;
                case AdcClockAuxSource.ClkGPin0:
                case AdcClockAuxSource.ClkGPin1:
                    {
                        this.Log(LogLevel.Error, "GPin clock source not supported");
                        return 1;
                    }
            }
            this.Log(LogLevel.Error, "Unknown configuration for ADC aux source");
            return 1;
        }

        private void UpdateAdcClock()
        {
            ulong integral = adcDiv == 0 ? 1ul << 16 : adcDiv;
            AdcClockFrequency = (uint)(GetRtcClockSourceFrequency() / integral);

            foreach (var a in onAdcChange)
            {
                a(AdcClockFrequency);
            }
            ReconfigureAllGpout();
        }

        private ulong GetFrequencyForAuxSource(GPOUTControl.AuxSource source)
        {
            switch (source)
            {
                case GPOUTControl.AuxSource.ClkSrcPllSys: return pll.CalculateOutputFrequency(xosc.Frequency);
                case GPOUTControl.AuxSource.ClkSrcGPin0:
                case GPOUTControl.AuxSource.ClkSrcGPin1:
                    {
                        this.Log(LogLevel.Error, "GPIN input is not supported");
                        return 0;
                    }
                case GPOUTControl.AuxSource.ClkSrcPllUsb: return pllusb.CalculateOutputFrequency(xosc.Frequency);
                case GPOUTControl.AuxSource.RoscClkSrc: return rosc.Frequency;
                case GPOUTControl.AuxSource.XoscClkSrc: return xosc.Frequency;
                case GPOUTControl.AuxSource.ClkSys: return SystemClockFrequency;
                case GPOUTControl.AuxSource.ClkUsb: return UsbClockFrequency;
                case GPOUTControl.AuxSource.ClkAdc: return AdcClockFrequency;
                case GPOUTControl.AuxSource.ClkRtc: return RtcClockFrequency;
                case GPOUTControl.AuxSource.ClkRef: return ReferenceClockFrequency;
            }
            return 0;
        }
        private void ReconfigureGpout(int id)
        {
            if (!gpout[id].kill)
            {
                // if enabled then kill 
                if (gpout[id].thread != null && gpout[id].enable == false)
                    return;
            }
            if (!gpout[id].enable)
            {
                return;
            }
            this.Log(LogLevel.Info, "Configuring GPOUT" + id + " for source " + gpout[id].auxSource);
            if (gpout[id].thread == null)
            {
                gpout[id].thread = machine.ObtainManagedThread(() => GpoutStep(id), 1, name: "GPOUT" + id);
            }
            gpout[id].thread.Stop();
            if (gpout[id].auxSource == GPOUTControl.AuxSource.ClkSrcGPin0 || gpout[id].auxSource == GPOUTControl.AuxSource.ClkSrcGPin1)
            {
                this.Log(LogLevel.Info, "Setting GPOUT" + id + " to input pin propagation");
                // register for gpio callback
                return;
            }
            ulong divider = (ulong)gpout[id].dividerIntegral;
            if (divider == 0)
            {
                divider = 1 << 16;
            }
            ulong frequency = GetFrequencyForAuxSource(gpout[id].auxSource) / divider;
            this.Log(LogLevel.Info, "Setting GPOUT" + id + " frequency: " + frequency);
            gpout[id].thread.Frequency = (uint)frequency << 1;
            gpout[id].thread.Start();
        }

        private void GpoutStep(int id)
        {
            if (gpoutPins[id] != -1)
            {
                this.gpio.TogglePin(gpoutPins[id]);
            }
        }
        private void DefineRegisters()
        {
            int registerCounter = 0;
            Registers[] controlRegs = new Registers[4];
            controlRegs[0] = Registers.CLK_GPOUT0_CTRL; 
            controlRegs[1] = Registers.CLK_GPOUT1_CTRL; 
            controlRegs[2] = Registers.CLK_GPOUT2_CTRL; 
            controlRegs[3] = Registers.CLK_GPOUT3_CTRL;
            foreach (var r in controlRegs)
            {
                int id = registerCounter;
                r.Define(this)
                    .WithReservedBits(0, 5)
                    .WithValueField(5, 4, valueProviderCallback: (_) =>
                    {
                        return (ulong)gpout[id].auxSource;
                    }, writeCallback: (_, value) =>
                    {
                        gpout[id].auxSource = (GPOUTControl.AuxSource)value;
                        ReconfigureGpout(id);
                    }, name: "AUXSRC")
                    .WithReservedBits(9, 1)
                    .WithFlag(10, valueProviderCallback: _ => gpout[id].kill,
                        writeCallback: (_, value) =>
                        {
                            gpout[id].kill = value;
                            ReconfigureGpout(id);
                        }, name: "KILL")
                    .WithFlag(11, valueProviderCallback: _ => gpout[id].enable,
                        writeCallback: (_, value) =>
                        {
                            gpout[id].enable = value;
                            ReconfigureGpout(id);
                        }, name: "ENABLE")
                    .WithFlag(12, valueProviderCallback: _ => gpout[id].dc50,
                        writeCallback: (_, value) => gpout[id].dc50 = value,
                        name: "DC50")
                    .WithReservedBits(13, 3)
                    .WithValueField(16, 2, valueProviderCallback: _ => gpout[id].phase,
                        writeCallback: (_, value) => gpout[id].phase = (byte)value,
                        name: "PHASE")
                    .WithReservedBits(18, 2)
                    .WithFlag(20, valueProviderCallback: _ => gpout[id].nudge,
                        writeCallback: (_, value) => gpout[id].nudge = value,
                        name: "NUDGE");

                registerCounter++;
            }

            registerCounter = 0;
            Registers[] dividerRegs = new Registers[4];
            dividerRegs[0] = Registers.CLK_GPOUT0_DIV; 
            dividerRegs[1] = Registers.CLK_GPOUT1_DIV; 
            dividerRegs[2] = Registers.CLK_GPOUT2_DIV; 
            dividerRegs[3] = Registers.CLK_GPOUT3_DIV;
            foreach (var r in dividerRegs)
            {
                int id = registerCounter;
                r.Define(this)
                    .WithValueField(0, 8,
                        valueProviderCallback: (_) => (ulong)gpout[id].dividerFrac,
                        writeCallback: (_, value) =>
                        {
                            gpout[id].dividerFrac = (int)value;
                            ReconfigureGpout(id);
                        }, name: "DIV_FRAC")
                    .WithValueField(8, 24,
                        valueProviderCallback: (_) => (ulong)gpout[id].dividerIntegral,
                        writeCallback: (_, value) =>
                        {
                            gpout[id].dividerIntegral = (int)value;
                            ReconfigureGpout(id);
                        }, name: "DIV_INT");

                registerCounter++;
            }

            Registers.CLK_GPOUT0_SELECTED.Define(this)
                .WithValueField(0, 32, FieldMode.Read,
                     valueProviderCallback: _ => 1,
                     name: "SELECTED");

            Registers.CLK_GPOUT1_SELECTED.Define(this)
                .WithValueField(0, 32, FieldMode.Read,
                     valueProviderCallback: _ => 1,
                     name: "SELECTED");

            Registers.CLK_GPOUT2_SELECTED.Define(this)
                .WithValueField(0, 32, FieldMode.Read,
                     valueProviderCallback: _ => 1,
                     name: "SELECTED");

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
                .WithFlag(12, valueProviderCallback: _ => resusForced,
                    writeCallback: (_, value) =>
                    {
                        resusForced = value;
                        resused = value;
                        if (resusIrqEnabled || value)
                        {
                            IRQ.Set(resused);
                        }
                    }, name: "CLK_SYS_RESUS_CTRL_FORCE")
                .WithReservedBits(13, 3)
                .WithFlag(16, valueProviderCallback: _ => false,
                        writeCallback: (_, value) =>
                        {
                            if (value)
                            {
                                resused = false;
                            }
                        },
                    name: "CLK_SYS_RESUS_CTRL_CLEAR")
                .WithReservedBits(17, 15);

            Registers.CLK_SYS_RESUS_STATUS.Define(this)
                .WithFlag(0, FieldMode.Read,
                    valueProviderCallback: _ => resused,
                    name: "RESUSSED")
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
                                frequencyCounter = ((ulong)ReferenceClockFrequency << 5) / 1000;
                                break;
                            }
                        case 9:
                            {
                                frequencyCounter = ((ulong)SystemClockFrequency << 5) / 1000;
                                break;
                            }
                        case 10:
                            {
                                frequencyCounter = ((ulong)PeripheralClockFrequency << 5) / 1000;
                                break;
                            }
                        case 11:
                            {
                                frequencyCounter = ((ulong)UsbClockFrequency << 5) / 1000;
                                break;
                            }
                        case 12:
                            {
                                frequencyCounter = ((ulong)AdcClockFrequency << 5) / 1000;
                                break;
                            }
                        case 13:
                            {
                                frequencyCounter = ((ulong)RtcClockFrequency << 5) / 1000;
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
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => resused,
                    name: "CLK_SYS_RESUS")
                .WithReservedBits(1, 31);

            Registers.INTE.Define(this)
                .WithFlag(0, valueProviderCallback: _ => resusIrqEnabled,
                    writeCallback: (_, value) => resusIrqEnabled = value,
                    name: "CLK_SYS_RESUS")
                .WithReservedBits(1, 31);

            Registers.INTF.Define(this)
                .WithFlag(0, valueProviderCallback: _ => resusIrqForced,
                    writeCallback: (_, value) =>
                    {
                        resusIrqForced = value;
                        RaiseInterrupt();
                    },
                    name: "CLK_SYS_RESUS")
                .WithReservedBits(1, 31);

            Registers.INTS.Define(this)
                .WithFlag(0, FieldMode.Read,
                    valueProviderCallback: _ => resused || resusIrqForced,
                    name: "CLK_SYS_RESUS")
                .WithReservedBits(1, 31);
        }

        private void RaiseInterrupt()
        {
            IRQ.Set(true);
        }

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
            ClkGPin1 = 5
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

        public uint SystemClockFrequency { get; private set; }
        public uint PeripheralClockFrequency { get; private set; }
        public uint ReferenceClockFrequency { get; private set; }
        public uint UsbClockFrequency { get; private set; }
        public uint AdcClockFrequency { get; private set; }
        public uint RtcClockFrequency { get; private set; }
        public GPIO IRQ { get; private set; }

        private RefClockSource refClockSource;
        private RefClockAuxSource refClockAuxSource;
        private byte refDiv;
        private List<Action<long>> onRefClockChange;

        private SysClockAuxSource sysClockAuxSource;
        private SysClockSource sysClockSource;
        private byte sysDivFrac;
        private uint sysDivInt;
        private List<Action<long>> onSysClockChange;

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

        private bool resused;
        private bool resusIrqEnabled;
        private bool resusIrqForced;
        private bool resusForced;

        private NVIC[] nvic;
        private const int numberOfGPOUTs = 4;
        private struct GPOUTControl
        {
            public bool nudge;
            public byte phase;
            public bool dc50;
            public bool enable;
            public bool kill;
            public enum AuxSource
            {
                ClkSrcPllSys = 0,
                ClkSrcGPin0 = 1,
                ClkSrcGPin1 = 2,
                ClkSrcPllUsb = 3,
                RoscClkSrc = 4,
                XoscClkSrc = 5,
                ClkSys = 6,
                ClkUsb = 7,
                ClkAdc = 8,
                ClkRtc = 9,
                ClkRef = 10
            };
            public AuxSource auxSource;
            public int dividerIntegral;
            public int dividerFrac;
            public IManagedThread thread;
        }
        GPOUTControl[] gpout;
        private RP2040GPIO gpio;
        private int[] gpoutPins;
    }
}
