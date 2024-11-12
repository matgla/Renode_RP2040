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
using Dynamitey.DynamicObjects;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class PCF8523 : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public PCF8523(IMachine machine, bool gpoutEnabled)
        {
            timer = machine.ObtainManagedThread(Tick, 1, "PCF8523_RTC");
            timer.Start();
            timerA = new LimitTimer(machine.ClockSource, 1, this, "PCF8523_TIMERA", direction: Time.Direction.Descending, enabled: false, workMode: Time.WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timerB = new LimitTimer(machine.ClockSource, 1, this, "PCF8523_TIMERB", direction: Time.Direction.Descending, enabled: false, workMode: Time.WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timerA.LimitReached += () => OnTimerFired(0);
            timerB.LimitReached += () => OnTimerFired(1);

            // this.gpoutEnabled = gpoutEnabled;
            // if (gpoutEnabled)
            // {
            //     gpoutClock = machine.ObtainManagedThread(GpoutStep, 1, "PCF8523_GPOUT");
            // }

            // RegistersCollection = new ByteRegisterCollection(this);
            // DefineRegisters();
            // Reset();
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

        private void DefineRegisters()
        {
            Registers.Control1.Define(this)
                .WithTaggedFlag("CIE", 0)
                .WithFlag(1, out alarmInterruptEnable, name: "AIE")
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
                .WithFlag(3, out alarmInterrupt, name: "AF")
                .WithFlag(4, out secondInterrupt, name: "SF")
                .WithFlag(5, out countdownTimerBInterrupt, name: "CTBF")
                .WithFlag(6, out countdownTimerAInterrupt, name: "CTAF")
                .WithFlag(7, out watchdogTimerInterrupt, name: "WTAF");

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
                    writeCallback: (_, value) => UpdateYear(BcdToByte(value & 0xff) + 1),
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
                .WithFlag(7, out minuteAlarmEnabled, name: "AEN_H");

            Registers.DayAlarm.Define(this)
                .WithValueField(0, 6,
                    valueProviderCallback: _ => ByteToBcd(dayAlarm),
                    writeCallback: (_, value) => dayAlarm = BcdToByte(value & 0x3f),
                    name: "DAY_ALARM")
                .WithIgnoredBits(6, 1)
                .WithFlag(7, out minuteAlarmEnabled, name: "AEN_D");

            Registers.WeekdayAlarm.Define(this)
                .WithValueField(0, 3,
                    valueProviderCallback: _ => ByteToBcd(weekdayAlarm),
                    writeCallback: (_, value) => weekdayAlarm = BcdToByte(value & 0x07),
                    name: "WEEKDAY_ALARM")
                .WithIgnoredBits(3, 4)
                .WithFlag(7, out minuteAlarmEnabled, name: "AEN_W");

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
                        if (value == 1 || value == 2)
                        {
                            timerA.Enabled = true;
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
                .WithIgnoredBits(3, 5);

            Registers.TimerBRegister.Define(this)
                .WithValueField(0, 8,
                    valueProviderCallback: _ => timerB.Value,
                    writeCallback: (_, value) =>
                    {
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
            return (byte)((data >> 4) * 10 | data & 0xf);
        }

        private byte BcdToByte(int data)
        {
            return BcdToByte((ulong)data);
        }

        private void UpdateSecond(byte second)
        {
            ticks = (ulong)GetCurrentTime().With(second: second).Ticks;
        }

        private void UpdateMinute(byte minute)
        {
            ticks = (ulong)GetCurrentTime().With(minute: minute).Ticks;
        }

        private void UpdateHour(byte hour)
        {
            ticks = (ulong)GetCurrentTime().With(hour: hour).Ticks;
        }

        private void UpdateDay(byte day)
        {
            ticks = (ulong)GetCurrentTime().With(day: day).Ticks;
        }

        private void UpdateMonth(byte month)
        {
            ticks = (ulong)GetCurrentTime().With(month: month).Ticks;
        }

        private void UpdateYear(int year)
        {
            ticks = (ulong)GetCurrentTime().With(year: year).Ticks;
        }

        private void ResetRegisters()
        {
            RegistersCollection.Reset();
            minuteAlarm = 0;
            weekdayAlarm = 0;
            dayAlarm = 0;
            hourAlarm = 0;
            minuteAlarmEnabled.Value = true;
            hourAlarmEnabled.Value = true;
            dayAlarmEnabled.Value = true;
            weekDayAlarmEnabled.Value = true;
            timerA.Enabled = false;
            timerB.Enabled = false;
        }

        private DateTime GetCurrentTime()
        {
            // this.Log(LogLevel.Error, "Getting time: " + timer.Value);
            return new DateTime((long)ticks * TimeSpan.TicksPerSecond);
        }

        private void Tick()
        {
            ticks++;
            var now = GetCurrentTime();
            bool? triggerAlarm = null;

            if (minuteAlarmEnabled.Value)
            {
                triggerAlarm = now.Minute == minuteAlarm;
            }
            if (hourAlarmEnabled.Value && triggerAlarm.GetValueOrDefault(false))
            {
                triggerAlarm = now.Hour == hourAlarm;
            }
            if (dayAlarmEnabled.Value && triggerAlarm.GetValueOrDefault(false))
            {
                triggerAlarm = now.Day == dayAlarm;
            }
            if (weekDayAlarmEnabled.Value && triggerAlarm.GetValueOrDefault(false))
            {
                triggerAlarm = (byte)now.DayOfWeek == weekdayAlarm;
            }

            if (triggerAlarm.GetValueOrDefault(false))
            {
                alarmInterrupt.Value = true;
            }
        }

        private void GpoutStep()
        {
            ClockOutPin.Toggle();
        }

        private void OnTimerFired(int id)
        {

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
        private IFlagRegisterField weekDayAlarmEnabled;

        private IValueRegisterField timerAControl;
        private bool timerAIsWatchdog => timerAControl.Value == 2;

        private IValueRegisterField clockOutputFrequency;
        private IFlagRegisterField timerBMode;
        private IFlagRegisterField timerAMode;

        private IValueRegisterField timerAFrequency;
        private IValueRegisterField timerBFrequency;

        private byte minuteAlarm;
        private byte hourAlarm;
        private byte dayAlarm;
        private byte weekdayAlarm;

        private IManagedThread timer;
        private readonly bool gpoutEnabled;
        private IManagedThread gpoutClock;
        private ulong ticks;

        private LimitTimer timerA;
        private LimitTimer timerB;
    }
}