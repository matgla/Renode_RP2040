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
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Miscellaneous;
using System.Linq;
using Lucene.Net.Search;
using Antmicro.Renode.Peripherals.Timers;
using System.IO;
using Antmicro.Renode.Peripherals.SD;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class PCF8523 : II2CPeripheral
    {
        public PCF8523(IMachine machine)
        {
            timer = new LimitTimer(machine.ClockSource, 1, this, "PCF8523_RTC", direction: Time.Direction.Ascending, eventEnabled: true, autoUpdate: true);
            timer.Enabled = true;
            Reset();
        }

        public void Reset()
        {
            state = State.ReceiveAddress;
            timer.Value = 0;
        }

        public void Write(byte[] data)
        {
            // this.Log(LogLevel.Error, "Write {0}", data.Select(x => x.ToString("X")).Aggregate((x, y) => x + " " + y));
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
            // this.Log(LogLevel.Noisy, "Finished transmission");
            state = State.ReceiveAddress;
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

        private byte[] ProcessReadCommand()
        {
            switch (address)
            {
                case Registers.Control1:
                    return new byte[1] { 0 };
                case Registers.Control2:
                    return new byte[1] { 0 };
                case Registers.Control3:
                    return new byte[1] { 0 };
                case Registers.Seconds:
                    return new byte[1] { (byte)GetCurrentTime().Second };
                case Registers.Minutes:
                    return new byte[1] { (byte)GetCurrentTime().Minute };
                case Registers.Hours:
                    return new byte[1] { (byte)GetCurrentTime().Hour };
                case Registers.Days:
                    return new byte[1] { (byte)GetCurrentTime().Day };
                case Registers.Weekdays:
                    return new byte[1] { (byte)GetCurrentTime().DayOfWeek };
                case Registers.Months:
                    return new byte[1] { (byte)GetCurrentTime().Month };
                case Registers.Years:
                    return new byte[1] { (byte)(GetCurrentTime().Year - 1) };

            }
            return new byte[0];
        }

        private void UpdateSecond(byte second)
        {
            if (second > 59)
            {
                second = 59;
            }
            DateTime dt = GetCurrentTime();
            DateTime newDt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, second);

            // this.Log(LogLevel.Error, "Updating seconds from: " + dt.Ticks + ", to: " + newDt.Ticks);
            timer.Value = (ulong)newDt.Ticks;
        }

        private void UpdateMinute(byte minute)
        {
            if (minute > 59)
            {
                minute = 59;
            }
            DateTime dt = GetCurrentTime();
            DateTime newDt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, minute, dt.Second);
            timer.Value = (ulong)newDt.Ticks;
        }

        private void UpdateHour(byte hour)
        {
            if (hour > 23)
            {
                hour = 23;
            }
            DateTime dt = GetCurrentTime();
            DateTime newDt = new DateTime(dt.Year, dt.Month, dt.Day, hour, dt.Minute, dt.Second);
            timer.Value = (ulong)newDt.Ticks;
        }

        private void UpdateDay(byte day)
        {
            if (day == 0)
            {
                day = 1;
            }
            else if (day > 31)
            {
                day = 31;
            }

            DateTime dt = GetCurrentTime();
            DateTime newDt = new DateTime(dt.Year, dt.Month, day, dt.Hour, dt.Minute, dt.Second);
            timer.Value = (ulong)newDt.Ticks;
        }

        private void UpdateMonth(byte month)
        {
            if (month == 0)
            {
                month = 1;
            }
            else if (month > 12)
            {
                month = 12;
            }
            DateTime dt = GetCurrentTime();
            DateTime newDt = new DateTime(dt.Year, month, dt.Day, dt.Hour, dt.Minute, dt.Second);
            timer.Value = (ulong)newDt.Ticks;
        }

        private void UpdateYear(byte year)
        {
            if (year > 99)
            {
                year = 99;
            }
            DateTime dt = GetCurrentTime();
            DateTime newDt = new DateTime(year + 1, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
            timer.Value = (ulong)newDt.Ticks;
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
            }
        }

        private void ResetRegisters()
        {
            // this.Log(LogLevel.Debug, "Reset command received");
        }

        private DateTime GetCurrentTime()
        {
            // this.Log(LogLevel.Error, "Getting time: " + timer.Value);
            return new DateTime((long)timer.Value * TimeSpan.TicksPerSecond);
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
        private State state;
        private Registers address;

        private LimitTimer timer;
    }
}