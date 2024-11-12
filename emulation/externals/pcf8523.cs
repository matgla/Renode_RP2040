/**
 * pcf8523.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Numerics;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Miscellaneous;
using System.Linq;
using Lucene.Net.Search;
using Antmicro.Renode.Peripherals.Timers;
using System.IO;
using Antmicro.Renode.Peripherals.SD;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.OptionsParser;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class PCF8523 : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public PCF8523(IMachine machine)
        {
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
            timer = machine.ObtainManagedThread(Tick, 1, "PCF8523_RTC");// new LimitTimer(machine.ClockSource, 1, this, "PCF8523_RTC", direction: Time.Direction.Ascending, eventEnabled: true, autoUpdate: true);
            timer.Start();
            Reset();
        }

        public void Reset()
        {
            state = State.ReceiveAddress;
            ticks = 0;
            RegistersCollection.Reset(); 
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
            var result = ProcessReadCommand();
            address += 1;
            if (address > Registers.TimerBRegister)
            {
                address = Registers.Control1;
            }
            return result;
        }

        public void FinishTransmission()
        {
            state = State.ReceiveAddress;
        }

        public ByteRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.Control1.Define(this)
                .WithTaggedFlag("CIE", 0)
                .WithFlag(1, out alarmInterruptEnable, name: "AIE")
                .WithFlag(2, out secondInterruptEnable, name: "SIE")
                .WithFlag(3, out hourMode12, name: "12_24")
                .WithFlag(4, valueProviderCallback: _ => false, writeCallback: (_, value) => {
                    if (value) 
                    {
                        ResetRegisters();
                    }
                }, name: "SR")
                .WithFlag(5, valueProviderCallback: _ => false, writeCallback: (_, value)  => {
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
                    writeCallback: (_, value) => UpdateSecond(BcdToByte(value)),
                    name: "SECONDS")
                .WithTaggedFlag("OS", 7);

            Registers.Minutes.Define(this)
                .WithValueField(0, 7, 
                    valueProviderCallback: _ => ByteToBcd(GetCurrentTime().Minute),
                    writeCallback: (_, value) => UpdateMinute(BcdToByte(value)),
                    name: "MINUTES")
                .WithTaggedFlag("RES", 7);

            Registers.Hours.Define(this)
                .WithValueField(0, 6, 
                    valueProviderCallback: _ => ByteToBcd(GetCurrentTime().Hour),
                    writeCallback: (_, value) => UpdateHour((byte)value),
                    name: "HOURS")
                .WithTaggedFlags("RES", 6, 2);

        }

        private void HandleByteWrite(byte data)
        {
            switch (state)
            {
                case State.ReceiveAddress:
                    {
                        state = State.HandleCommand;
                        address = (Registers)data;
                        break;
                    }
                case State.HandleCommand:
                    {
                        HandleCommand(data);
                        address += 1;
                        if (address > Registers.TimerBRegister)
                        {
                            address = Registers.Control1;
                        }
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

        private byte ConvertBcdHourByMode(byte hour)
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

        private byte GetHoursInBcd()
        {
            if (hourMode12)
            {
                byte hour = (byte)(GetCurrentTime().Hour % 12);
                byte amOrPm = GetCurrentTime().Hour > 12 ? (byte)0x10 : (byte)0x00;
                if (hour == 0) 
                {
                    hour = 12;
                } 
                return (byte)(hour | amOrPm);
            }
            return ByteToBcd((byte)GetCurrentTime().Minute);
        }

        private byte BcdToByte(ulong data)
        {
            return (byte)((data >> 4) * 10 | data & 0xf);
        }

        private byte BcdToByte(int data)
        {
            return BcdToByte((ulong)data);
        }

        private byte[] ProcessReadCommand()
        {
            switch (address)
            {
                case Registers.Control1:
                    return new byte[1] { control1 };
                case Registers.Control2:
                    return new byte[1] { control2 };
                case Registers.Control3:
                    return new byte[1] { control3 };
                case Registers.Seconds:
                    return new byte[1] { ByteToBcd((byte)GetCurrentTime().Second) };
                case Registers.Minutes:
                    return new byte[1] { ByteToBcd((byte)GetCurrentTime().Minute) };
                case Registers.Hours:
                    return new byte[1] { GetHoursInBcd() };
                case Registers.Days:
                    return new byte[1] { ByteToBcd((byte)GetCurrentTime().Day) };
                case Registers.Weekdays:
                    return new byte[1] { ByteToBcd((byte)GetCurrentTime().DayOfWeek) };
                case Registers.Months:
                    return new byte[1] { ByteToBcd((byte)GetCurrentTime().Month) };
                case Registers.Years:
                    return new byte[1] { ByteToBcd((byte)(GetCurrentTime().Year - 1)) };
            }
            return new byte[0];
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

        private void UpdateYear(byte year)
        {
            ticks = (ulong)GetCurrentTime().With(year: year).Ticks;
        }

        private void SetMinuteAlarm(byte minute)
        {
            minuteAlarm = null;
            if ((minute & (1 << 7)) == 0)
            {
                // minute alarm is enabled
                minuteAlarm = BcdToByte((byte)(minute & 0xef)); 
                this.Log(LogLevel.Noisy, "Set alarm minute to: " + minuteAlarm.GetValueOrDefault());
            }
            else 
            {
                this.Log(LogLevel.Noisy, "Disabled alarm minute condition");
            }
        }

        private void SetHourAlarm(byte hour)
        {
            hourAlarm = null;
            if ((hour & (1 << 7)) == 0)
            {
                hour = GetHoursInBcd()
                this.Log(LogLevel.Noisy, "Set alarm hour to: " + hourAlarm.GetValueOrDefault());
            }
            else 
            {
                this.Log(LogLevel.Noisy, "Disabled alarm hour condition");
            }
        }

        private void SetDayAlarm(byte day)
        {
            dayAlarm = null;
            if ((day & (1 << 7)) == 0)
            {
                dayAlarm = BcdToByte((byte)(day & 0x3f));
                this.Log(LogLevel.Noisy, "Set alarm day to: " + dayAlarm.GetValueOrDefault());
            }
            else 
            {
                this.Log(LogLevel.Noisy, "Disabled alarm day condition");
            }
        }

        private void SetWeekDayAlarm(byte day)
        {
            weekdayAlarm = null;
            if ((day & (1 << 7)) == 0)
            {
                weekdayAlarm = BcdToByte((byte)(day & 0x7));
                this.Log(LogLevel.Noisy, "Set alarm weekday to: " + weekdayAlarm.GetValueOrDefault());
            }
            else 
            {
                this.Log(LogLevel.Noisy, "Disabled alarm weekday condition");
            }
        }

        private void HandleCommand(byte data)
        {
            // this.Log(LogLevel.Error, "Handling command: {0:X}", data);
            switch (address)
            {
                case Registers.Control1:
                    {
                        if (data == 0x58)
                        {
                            ResetRegisters();
                        }
                        return;
                    }
                case Registers.Seconds:
                    {
                        UpdateSecond((byte)(data & 0x7f));
                        break;
                    }
                case Registers.Minutes:
                    {
                        UpdateMinute((byte)(data & 0x7f));
                        break;
                    }
                case Registers.Hours:
                    {
                        UpdateHour((byte)(data & 0x3f));
                        break;
                    }
                case Registers.Weekdays:
                    {
                        break;
                    }
                case Registers.Days:
                    {
                        UpdateDay((byte)(data & 0x7));
                        break;
                    }
                case Registers.Months:
                    {
                        UpdateMonth((byte)(data & 0x1f));
                        break;
                    }
                case Registers.Years:
                    {
                        UpdateYear(data);
                        break;
                    }
                case Registers.MinuteAlarm:
                    {
                        SetMinuteAlarm(data);
                        break;
                    }
                case Registers.HourAlarm:
                    {
                        SetHourAlarm(data);
                        break;
                    }
                case Registers.DayAlarm:
                    {
                        SetDayAlarm(data);
                        break;
                    }
                case Registers.WeekdayAlarm:
                    {
                        SetWeekDayAlarm(data);
                        break;
                    }
            }
        }

        private void ResetRegisters()
        {
            control1 = 0;
            control2 = 0;
            control3 = 0xe0;
            minuteAlarm = null;
            weekdayAlarm = null;
            dayAlarm = null;
            hourAlarm = null;
            // this.Log(LogLevel.Debug, "Reset command received");
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
            if (minuteAlarm != null)
            {
                triggerAlarm = now.Minute == minuteAlarm.Value;
            }
            if (hourAlarm != null && triggerAlarm.GetValueOrDefault(false))
            {
                triggerAlarm = now.Hour == hourAlarm.Value;
            }
            if (dayAlarm != null && triggerAlarm.GetValueOrDefault(false))
            {
                triggerAlarm = now.Day == dayAlarm.Value;
            }
            if (weekdayAlarm != null && triggerAlarm.GetValueOrDefault(false))
            {
                triggerAlarm = (byte)now.DayOfWeek == weekdayAlarm.Value;
            }

            if (triggerAlarm.GetValueOrDefault(false))
            {
                control2 |= (1 << 3); 
            }
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
        private Registers address;
        private byte control1;
        private byte control2;
        private byte control3;

        private byte? minuteAlarm;
        private byte? hourAlarm;
        private byte? dayAlarm;
        private byte? weekdayAlarm;

        private IManagedThread timer;
        private ulong ticks;
    }
}