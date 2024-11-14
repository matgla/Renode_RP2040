/**
 * pcf8523.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Timers;
using IronPython.Modules;
using System.Threading;

namespace Antmicro.Renode.Peripherals.I2C
{


    // TODO: cover with tests
    //       add weekday setting
    public class PCF8523 : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public PCF8523(IMachine machine, bool gpoutEnabled)
        {
            timer = machine.ObtainManagedThread(Tick, 1, "PCF8523_RTC");
            timer.Start();
            timerA = new LimitTimer(machine.ClockSource, 1, this, "PCF8523_TIMERA", direction: Time.Direction.Descending, enabled: false, workMode: Time.WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timerB = new LimitTimer(machine.ClockSource, 1, this, "PCF8523_TIMERB", direction: Time.Direction.Descending, enabled: false, workMode: Time.WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timerAIrqDisable = new LimitTimer(machine.ClockSource, 10000, this, "PCF8523_TIMERA_IRQ_DISABLE", direction: Time.Direction.Ascending, enabled: false, workMode: Time.WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timerBIrqDisable = new LimitTimer(machine.ClockSource, 10000, this, "PCF8523_TIMERB_IRQ_DISABLE", direction: Time.Direction.Ascending, enabled: false, workMode: Time.WorkMode.OneShot, eventEnabled: true, autoUpdate: true);

            ClockOutPin = new GPIO();
            IRQ2 = new GPIO();

            timerA.LimitReached += OnTimerAFired;
            timerB.LimitReached += OnTimerBFired;
            timerAIrqDisable.LimitReached += OnTimerAIrqEnd;
            timerBIrqDisable.LimitReached += OnTimerBIrqEnd;

            this.gpoutEnabled = gpoutEnabled;
            if (gpoutEnabled)
            {
                gpoutClock = machine.ObtainManagedThread(GpoutStep, 1, "PCF8523_GPOUT");
            }

            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public void Reset()
        {
            state = State.ReceiveAddress;
            ticks = 0;
            ResetRegisters();
        }

        public void Write(byte[] data)
        {
            foreach (byte b in data)
            {
                HandleByteWrite(b);
            }
        }

        public byte[] Read(int count = 0)
        {
            var result = RegistersCollection.Read(address);
            IncrementAddress();
            return new byte[1] { result };
        }

        public void FinishTransmission()
        {
            state = State.ReceiveAddress;
        }

        public ByteRegisterCollection RegistersCollection { get; }

        public GPIO ClockOutPin { get; private set; }
        public GPIO IRQ1 => ClockOutPin;
        public GPIO IRQ2 { get; private set; }

        private void DefineRegisters()
        {
            Registers.Control1.Define(this)
                .WithTaggedFlag("CIE", 0)
                .WithFlag(1, out alarmInterruptEnable, writeCallback: (_, value) => {
                    this.Log(LogLevel.Error, "SETTING IRQ ALARM: " + value);
                    }, name: "AIE")
                .WithFlag(2, out secondInterruptEnable, name: "SIE")
                .WithFlag(3, out hourMode12, name: "12_24")
                .WithFlag(4, valueProviderCallback: _ => false, writeCallback: (_, value) =>
                {
                    if (value)
                    {
                        ResetRegisters();
                    }
                }, name: "SR")
                .WithFlag(5, valueProviderCallback: _ => false, writeCallback: (_, value) =>
                {
                    if (value)
                    {
                        timer.Stop();
                        return;
                    }
                    timer.Start();
                }, name: "STOP")
                .WithTaggedFlag("T", 6)
                .WithTaggedFlag("CAP_SEL", 7);

            Registers.Control2.Define(this)
                .WithFlag(0, out countdownTimerBInterruptEnabled, name: "CTBIE")
                .WithFlag(1, out countdownTimerAInterruptEnabled, name: "CTAIE")
                .WithFlag(2, out watchdogTimerEnabled, name: "WTAIE")
                .WithFlag(3, out alarmInterrupt, FieldMode.Read | FieldMode.WriteZeroToClear, writeCallback: (_, value) =>
                {
                    if (!value)
                    {
                        if (alarmInterruptEnable.Value)
                        {
                            IRQ1.Set(true);
                        }
                    }
                }, name: "AF")
                .WithFlag(4, out secondInterrupt, FieldMode.Read | FieldMode.WriteZeroToClear, writeCallback: (_, value) =>
                {

                }, name: "SF")
                .WithFlag(5, out countdownTimerBInterrupt, FieldMode.Read | FieldMode.WriteZeroToClear, writeCallback: (_, value) =>
                {
                    if (!value)
                    {
                        if (countdownTimerBInterruptEnabled.Value)
                        {
                            IRQ2.Set(true);
                            IRQ1.Set(true);
                            timerBIrqDisable.Enabled = false;
                        }
                    }
                }, name: "CTBF")
                .WithFlag(6, out countdownTimerAInterrupt, FieldMode.Read | FieldMode.WriteZeroToClear, writeCallback: (_, value) =>
                {
                    if (!value)
                    {
                        if (countdownTimerAInterruptEnabled.Value)
                        {
                            IRQ1.Set(true);
                            timerAIrqDisable.Enabled = false;
                        }
                    }
                }, name: "CTAF")
                .WithFlag(7, out watchdogTimerInterrupt, FieldMode.Read | FieldMode.WriteZeroToClear, writeCallback: (_, value) =>
                {
                    if (!value)
                    {
                        if (watchdogTimerEnabled.Value)
                        {
                            IRQ1.Set(true);
                            timerAIrqDisable.Enabled = false;
                        }
                    }
                }, name: "WTAF");

            Registers.Control3.Define(this)
                .WithTaggedFlag("BLIE", 0)
                .WithTaggedFlag("BSIE", 1)
                .WithTaggedFlag("BLF", 2)
                .WithTaggedFlag("BSF", 3)
                .WithTaggedFlag("RES", 4)
                .WithTag("PM", 5, 3);

            Registers.Seconds.Define(this)
                .WithValueField(0, 7,
                    valueProviderCallback: _ => ByteToBcd(GetCurrentTime().Second),
                    writeCallback: (_, value) => UpdateSecond(BcdToByte(value & 0x7f)),
                    name: "SECONDS")
                .WithTaggedFlag("OS", 7);

            Registers.Minutes.Define(this)
                .WithValueField(0, 7,
                    valueProviderCallback: _ => ByteToBcd(GetCurrentTime().Minute),
                    writeCallback: (_, value) => UpdateMinute(BcdToByte(value & 0x7f)),
                    name: "MINUTES")
                .WithIgnoredBits(7, 1);

            Registers.Hours.Define(this)
                .WithValueField(0, 6,
                    valueProviderCallback: _ => GetHoursInBcd(GetCurrentTime().Hour),
                    writeCallback: (_, value) => UpdateHour(ConvertBcdHour((byte)value)),
                    name: "HOURS")
                .WithIgnoredBits(7, 1);

            Registers.Days.Define(this)
                .WithValueField(0, 6,
                    valueProviderCallback: _ => ByteToBcd(GetCurrentTime().Day),
                    writeCallback: (_, value) => UpdateDay(BcdToByte(value & 0x3f)),
                    name: "DAYS")
                 .WithIgnoredBits(6, 2);

            Registers.Weekdays.Define(this)
                .WithValueField(0, 3,
                    valueProviderCallback: _ => ByteToBcd((byte)GetCurrentTime().DayOfWeek), // C# day of week is compilant with pcf8523
                    name: "WEEKDAYS")
                .WithTaggedFlags("RES", 3, 5);

            Registers.Months.Define(this)
                .WithValueField(0, 5,
                    valueProviderCallback: _ => ByteToBcd(GetCurrentTime().Month),
                    writeCallback: (_, value) => UpdateMonth(BcdToByte(value & 0x1f)),
                    name: "MONTHS")
                .WithIgnoredBits(5, 3);

            Registers.Years.Define(this)
                .WithValueField(0, 8,
                    valueProviderCallback: _ => ByteToBcd(GetCurrentTime().Year - 1),
                    writeCallback: (_, value) => UpdateYear(BcdToByte(value & 0xff)),
                    name: "YEARS");

            Registers.MinuteAlarm.Define(this)
                .WithValueField(0, 7,
                    valueProviderCallback: _ => ByteToBcd(minuteAlarm),
                    writeCallback: (_, value) => minuteAlarm = BcdToByte(value & 0x7f),
                    name: "MINUTE_ALARM")
                .WithFlag(7, out minuteAlarmEnabled, name: "AEN_M");

            Registers.HourAlarm.Define(this)
                .WithValueField(0, 6,
                    valueProviderCallback: _ => GetHoursInBcd(hourAlarm),
                    writeCallback: (_, value) => hourAlarm = ConvertBcdHour((byte)value),
                    name: "HOUR_ALARM")
                .WithIgnoredBits(6, 1)
                .WithFlag(7, out hourAlarmEnabled, name: "AEN_H");

            Registers.DayAlarm.Define(this)
                .WithValueField(0, 6,
                    valueProviderCallback: _ => ByteToBcd(dayAlarm),
                    writeCallback: (_, value) => dayAlarm = BcdToByte(value & 0x3f),
                    name: "DAY_ALARM")
                .WithIgnoredBits(6, 1)
                .WithFlag(7, out dayAlarmEnabled, name: "AEN_D");

            Registers.WeekdayAlarm.Define(this)
                .WithValueField(0, 3,
                    valueProviderCallback: _ => ByteToBcd(weekdayAlarm),
                    writeCallback: (_, value) => weekdayAlarm = BcdToByte(value & 0x07),
                    name: "WEEKDAY_ALARM")
                .WithIgnoredBits(3, 4)
                .WithFlag(7, out weekdayAlarmEnabled, name: "AEN_W");

            Registers.Offset.Define(this)
                .WithIgnoredBits(0, 8);

            Registers.TimerClockOutControl.Define(this)
                .WithFlag(0,
                    valueProviderCallback: _ => timerB.Enabled,
                    writeCallback: (_, value) => timerB.Enabled = value,
                    name: "TBC")
                .WithValueField(1, 2, out timerAControl,
                    writeCallback: (_, value) =>
                    {
                        timerA.Enabled = false;
                        timerA.Mode = Time.WorkMode.Periodic;
                        if (value == 1 || value == 2)
                        {
                            timerA.Enabled = true;
                        }
                        if (value == 2)
                        {
                            timerA.Mode = Time.WorkMode.OneShot;
                        }
                    },
                    name: "TAC")
                .WithValueField(3, 3, out clockOutputFrequency,
                    writeCallback: (_, value) =>
                    {
                        if (!gpoutEnabled)
                        {
                            return;
                        }
                        gpoutClock.Stop();
                        switch (value)
                        {
                            case 0: gpoutClock.Frequency = 32768 * 2; break;
                            case 1: gpoutClock.Frequency = 16384 * 2; break;
                            case 2: gpoutClock.Frequency = 8192 * 2; break;
                            case 3: gpoutClock.Frequency = 4096 * 2; break;
                            case 4: gpoutClock.Frequency = 1024 * 2; break;
                            case 5: gpoutClock.Frequency = 32 * 2; break;
                            case 6: gpoutClock.Frequency = 1 * 2; break;
                            case 7: return;
                        }
                        gpoutClock.Start();
                    },
                    name: "COF")
                .WithFlag(6, out timerBMode, name: "TBM")
                .WithFlag(7, out timerAMode, name: "TAM");

            Registers.TimerAFrequencyControl.Define(this)
                .WithValueField(0, 3, out timerAFrequency, writeCallback: (_, value) =>
                {
                    if (value == 0)
                    {
                        timerA.Frequency = 4096;
                    }
                    else if (value == 1)
                    {
                        timerA.Frequency = 64;
                    }
                    else
                    {
                        timerA.Frequency = 1;
                    }
                }, name: "TAQ")
                .WithIgnoredBits(3, 5);

            Registers.TimerARegister.Define(this)
                .WithValueField(0, 8,
                    valueProviderCallback: _ => timerA.Value,
                    writeCallback: (_, value) =>
                    {
                        if (watchdogTimerEnabled.Value)
                        {
                            watchdogTimerInterrupt.Value = false;
                            timerAIrqDisable.Enabled = false;
                            IRQ1.Set(true);
                        }
                        if (value == 0)
                        {
                            timerA.Enabled = false;
                            return;
                        }

                        // 1/60
                        if (timerAFrequency.Value == 3)
                        {
                            timerA.Value = value * 60;
                        }
                        else if (timerAFrequency.Value > 3)
                        {
                            timerA.Value = value * 3600;
                        }
                        else
                        {
                            timerA.Value = value;
                        }
                        timerA.Enabled = true;
                    }, name: "T_A"
                );

            Registers.TimerBFrequencyControl.Define(this)
                .WithValueField(0, 3, out timerAFrequency, writeCallback: (_, value) =>
                {
                    if (value == 0)
                    {
                        timerB.Frequency = 4096;
                    }
                    else if (value == 1)
                    {
                        timerB.Frequency = 64;
                    }
                    else
                    {
                        timerB.Frequency = 1;
                    }
                }, name: "TBQ")
                .WithIgnoredBits(3, 1)
                .WithValueField(4, 3, out timerBPulseWidth, name: "TBW")
                .WithIgnoredBits(7, 1);

            Registers.TimerBRegister.Define(this)
                .WithValueField(0, 8,
                    valueProviderCallback: _ => timerB.Value,
                    writeCallback: (_, value) =>
                    {
                        if (value == 0)
                        {
                            timerB.Enabled = false;
                            return;
                        }
                        timerB.Mode = Time.WorkMode.Periodic;
                        // 1/60
                        if (timerBFrequency.Value == 3)
                        {
                            timerB.Value = value * 60;
                        }
                        else if (timerBFrequency.Value > 3)
                        {
                            timerB.Value = value * 3600;
                        }
                        else
                        {
                            timerB.Value = value;
                        }
                    }, name: "T_B"
                );
        }

        private void IncrementAddress()
        {
            address += 1;
            if (address > (long)Registers.TimerBRegister)
            {
                address = 0;
            }
        }

        private void HandleByteWrite(byte data)
        {
            switch (state)
            {
                case State.ReceiveAddress:
                    {
                        state = State.HandleCommand;
                        address = data;
                        break;
                    }
                case State.HandleCommand:
                    {
                        RegistersCollection.Write(address, data);
                        IncrementAddress();
                        break;
                    }
            }
        }

        private byte ByteToBcd(long data)
        {
            byte ld = (byte)(data % 10);
            byte ud = (byte)(data / 10);
            return (byte)((ud << 4) | ld);
        }

        private byte ConvertBcdHour(byte hour)
        {
            if (hourMode12.Value == true)
            {
                // 12 - hour mode 
                bool pm = (hour & (1 << 5)) != 0;
                hour = BcdToByte(hour & 0x1f);
                if (pm)
                {
                    hour += 12;
                    hour %= 24;
                }
            }
            else
            {
                hour = BcdToByte(hour & 0x3f);
            }
            return hour;
        }

        private byte GetHoursInBcd(long time)
        {
            if (hourMode12.Value)
            {
                byte hour = (byte)(time % 12);
                byte amOrPm = time > 12 ? (byte)0x10 : (byte)0x00;
                if (hour == 0)
                {
                    hour = 12;
                }
                return (byte)(hour | amOrPm);
            }
            return ByteToBcd(time);
        }

        private byte BcdToByte(ulong data)
        {
            return (byte)((data >> 4) * 10 + (data & 0xf));
        }

        private byte BcdToByte(int data)
        {
            return BcdToByte((ulong)data);
        }

        private byte Clamp(byte value, byte min, byte max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }
        private void UpdateSecond(byte second)
        {
            this.Log(LogLevel.Debug, "Setting seconds: " + second);
            ticks = GetCurrentTime().With(second: Clamp(second, 0, 59)).Ticks;
        }

        private void UpdateMinute(byte minute)
        {
            this.Log(LogLevel.Debug, "Setting minute: " + minute);
            ticks = GetCurrentTime().With(minute: Clamp(minute, 0, 59)).Ticks;
        }

        private void UpdateHour(byte hour)
        {
            this.Log(LogLevel.Debug, "Setting hour to: " + hour);
            ticks = GetCurrentTime().With(hour: Clamp(hour, 0, 23)).Ticks;
        }

        private void UpdateDay(byte day)
        {
            this.Log(LogLevel.Debug, "Setting day to: " + day);
            ticks = GetCurrentTime().With(day: Clamp(day, 1, 31)).Ticks;
        }

        private void UpdateMonth(byte month)
        {
            this.Log(LogLevel.Debug, "Setting month to: " + month);
            ticks = GetCurrentTime().With(month: Clamp(month, 1, 12)).Ticks;
        }

        private void UpdateYear(int year)
        {
            this.Log(LogLevel.Debug, "Setting year to: " + year);
            ticks = GetCurrentTime().With(year: Clamp((byte)year, 1, 100)).Ticks;
        }

        private void ResetRegisters()
        {
            RegistersCollection.Reset();
            ticks = DateTime.MinValue.Ticks;
            minuteAlarm = 0;
            weekdayAlarm = 0;
            dayAlarm = 0;
            hourAlarm = 0;
            minuteAlarmEnabled.Value = true;
            hourAlarmEnabled.Value = true;
            dayAlarmEnabled.Value = true;
            weekdayAlarmEnabled.Value = true;
            timerA.Enabled = false;
            timerB.Enabled = false;
            timerAIrqDisable.Enabled = false;
            timerBIrqDisable.Enabled = false;
            IRQ1.Set(true);
            IRQ2.Set(true);
        }

        private DateTime GetCurrentTime()
        {
            return new DateTime(ticks);
        }

        private void Tick()
        {
            ticks += TimeSpan.TicksPerSecond;
            var now = GetCurrentTime();
            bool? triggerAlarm = null;

            if (now.Second == 0)
            {
                if (!minuteAlarmEnabled.Value)
                {
                    triggerAlarm = now.Minute == minuteAlarm;
                }
                if (!hourAlarmEnabled.Value && triggerAlarm.GetValueOrDefault(false))
                {
                    triggerAlarm = now.Hour == hourAlarm;
                }
                if (!dayAlarmEnabled.Value && triggerAlarm.GetValueOrDefault(false))
                {
                    triggerAlarm = now.Day == dayAlarm;
                }
                if (!weekdayAlarmEnabled.Value && triggerAlarm.GetValueOrDefault(false))
                {
                    triggerAlarm = (byte)now.DayOfWeek == weekdayAlarm;
                }

                if (triggerAlarm.GetValueOrDefault(false))
                {
                    alarmInterrupt.Value = true;
                    if (alarmInterruptEnable.Value)
                    {
                        this.Log(LogLevel.Debug, "Triggering alarm IRQ1");
                        IRQ1.Set(true);
                        IRQ1.Set(false);
                    }
                }
            }

            secondInterrupt.Value = true;
            if (secondInterruptEnable.Value)
            {
                IRQ1.Set(false);
                if (timerAMode.Value)
                {
                    SetTimerAPulse();
                }
            }
        }

        private void GpoutStep()
        {
            ClockOutPin.Toggle();
        }



        private ulong GetTimerBPulseWidth()
        {
            if (timerBFrequency.Value == 0)
            {
                return 1;
            }
            if (timerBFrequency.Value == 1)
            {
                return (int)(7.812 * 10);
            }

            switch (timerBPulseWidth.Value)
            {
                case 0: return (int)(46.875 * 10);
                case 1: return (int)(62.500 * 10);
                case 2: return (int)(78.125 * 10);
                case 3: return (int)(93.750 * 10);
                case 4: return (int)(125 * 10);
                case 5: return (int)(156.250 * 10);
                case 6: return (int)(187.500 * 10);
                case 7: return (int)(218.750 * 10);
            }
            return 1;
        }

        private ulong GetTimerAPulseWidth()
        {
            if (timerAFrequency.Value == 0)
            {
                return 1;
            }
            if (timerAFrequency.Value == 1)
            {
                return (int)(7.812 * 10);
            }
            return (int)(15.625 * 10);
        }

        private void SetTimerAPulse()
        {
            timerAIrqDisable.Value = 0;
            timerAIrqDisable.Limit = GetTimerAPulseWidth();
            timerAIrqDisable.Enabled = true;

        }
        private void OnTimerAFired()
        {
            if (timerAControl.Value == 1) // countdown timer 
            {
                if (countdownTimerAInterruptEnabled.Value)
                {
                    countdownTimerAInterrupt.Value = true;
                    if (timerAMode.Value == true)
                    {
                        SetTimerAPulse();
                    }
                    IRQ1.Set(false);
                }
            }
            else if (timerAControl.Value == 2)
            {
                if (watchdogTimerEnabled.Value)
                {
                    if (timerAMode.Value == true)
                    {
                        SetTimerAPulse();
                    }
                    watchdogTimerInterrupt.Value = true;
                    IRQ1.Set(false);
                }
            }
        }
        private void OnTimerBFired()
        {
            if (countdownTimerBInterruptEnabled.Value)
            {
                countdownTimerBInterrupt.Value = true;
                if (timerBMode.Value == true)
                {
                    timerBIrqDisable.Value = 0;
                    timerBIrqDisable.Limit = GetTimerBPulseWidth();
                    timerBIrqDisable.Enabled = true;
                }
                IRQ1.Set(false);
                IRQ2.Set(false);
            }
        }

        private void OnTimerAIrqEnd()
        {
            IRQ1.Set(true);
        }

        private void OnTimerBIrqEnd()
        {
            IRQ1.Set(true);
            IRQ2.Set(true);
        }

        private enum State
        {
            ReceiveAddress,
            HandleCommand
        };

        private enum Registers : byte
        {
            Control1 = 0,
            Control2,
            Control3,
            Seconds,
            Minutes,
            Hours,
            Days,
            Weekdays,
            Months,
            Years,
            MinuteAlarm,
            HourAlarm,
            DayAlarm,
            WeekdayAlarm,
            Offset,
            TimerClockOutControl,
            TimerAFrequencyControl,
            TimerARegister,
            TimerBFrequencyControl,
            TimerBRegister
        };

        IFlagRegisterField alarmInterruptEnable;
        IFlagRegisterField secondInterruptEnable;
        IFlagRegisterField hourMode12;

        IFlagRegisterField countdownTimerBInterruptEnabled;
        IFlagRegisterField countdownTimerAInterruptEnabled;
        IFlagRegisterField watchdogTimerEnabled;
        IFlagRegisterField alarmInterrupt;
        IFlagRegisterField secondInterrupt;
        IFlagRegisterField countdownTimerBInterrupt;
        IFlagRegisterField countdownTimerAInterrupt;
        IFlagRegisterField watchdogTimerInterrupt;

        private State state;
        private long address;

        private IFlagRegisterField minuteAlarmEnabled;
        private IFlagRegisterField hourAlarmEnabled;
        private IFlagRegisterField dayAlarmEnabled;
        private IFlagRegisterField weekdayAlarmEnabled;

        private IValueRegisterField timerAControl;
        private bool timerAIsWatchdog => timerAControl.Value == 2;

        private IValueRegisterField clockOutputFrequency;
        private IFlagRegisterField timerBMode;
        private IFlagRegisterField timerAMode;

        private IValueRegisterField timerAFrequency;
        private IValueRegisterField timerBFrequency;
        private IValueRegisterField timerBPulseWidth;
        private byte minuteAlarm;
        private byte hourAlarm;
        private byte dayAlarm;
        private byte weekdayAlarm;

        private IManagedThread timer;
        private readonly bool gpoutEnabled;
        private IManagedThread gpoutClock;
        private long ticks;

        private LimitTimer timerA;
        private LimitTimer timerB;
        private LimitTimer timerAIrqDisable;
        private LimitTimer timerBIrqDisable;
    }
}