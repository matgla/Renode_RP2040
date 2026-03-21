/**
 * pcf8523.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.I2C
{
    /// <summary>
    /// PCF8523 Real Time Clock I2C device simulator.
    /// Default I2C address is 0x68.
    /// 
    /// Supports reading/writing time and date in BCD format.
    /// Compatible with PCF8520 example from pico-examples.
    /// </summary>
    public class PCF8523 : II2CPeripheral
    {
        // Register addresses (0x00-0x12 for PCF8523)
        // Control registers
        private const byte REG_CONTROL_1 = 0x00;
        private const byte REG_CONTROL_2 = 0x01;
        private const byte REG_CONTROL_3 = 0x02;
        
        // Time and date registers (in BCD format)
        private const byte REG_SECONDS = 0x03;   // bits 6-4: tens, bits 3-0: units
        private const byte REG_MINUTES = 0x04;   // bits 6-4: tens, bits 3-0: units
        private const byte REG_HOURS = 0x05;     // bits 5-4: tens, bits 3-0: units
        private const byte REG_DAYS = 0x06;      // bits 5-4: tens, bits 3-0: units
        private const byte REG_WEEKDAYS = 0x07;  // bits 2-0: day of week (0=Sunday)
        private const byte REG_MONTHS = 0x08;    // bit 4: tens, bits 3-0: units
        private const byte REG_YEARS = 0x09;     // bits 7-4: tens, bits 3-0: units
        
        // Alarm registers
        private const byte REG_MINUTE_ALARM = 0x0A;
        private const byte REG_HOUR_ALARM = 0x0B;
        private const byte REG_DAY_ALARM = 0x0C;
        private const byte REG_WEEKDAY_ALARM = 0x0D;
        
        // Offset and timer registers
        private const byte REG_OFFSET = 0x0E;
        private const byte REG_TMR_CLKOUT_CTRL = 0x0F;
        private const byte REG_TMR_A_FREQ_CTRL = 0x10;
        private const byte REG_TMR_A_REG = 0x11;
        private const byte REG_TMR_B_FREQ_CTRL = 0x12;
        private const byte REG_TMR_B_REG = 0x13;

        public PCF8523()
        {
            Reset();
        }

        public void Reset()
        {
            registers = new byte[0x20];
            
            // Initialize time/date to zeros (as firmware sets on init)
            // This allows tests to verify the initial state before setting custom values
            SetTime(0, 0, 0);
            SetDate(0, 0, 0, 0); // All zeros for day/month/year/weekday
            
            // Initialize alarm registers (default 0x80 = alarm disabled)
            registers[REG_MINUTE_ALARM] = 0x80;
            registers[REG_HOUR_ALARM] = 0x80;
            registers[REG_DAY_ALARM] = 0x80;
            registers[REG_WEEKDAY_ALARM] = 0x80;
            
            // Initialize control registers
            // Control_1: 0x58 = software reset performed (as per PCF8523 datasheet)
            registers[REG_CONTROL_1] = 0x58;
            registers[REG_CONTROL_2] = 0x00;
            registers[REG_CONTROL_3] = 0x00;
            
            // Initialize offset and timer registers
            registers[REG_OFFSET] = 0x00;
            registers[REG_TMR_CLKOUT_CTRL] = 0x00;
            registers[REG_TMR_A_FREQ_CTRL] = 0x00;
            registers[REG_TMR_A_REG] = 0x00;
            registers[REG_TMR_B_FREQ_CTRL] = 0x00;
            registers[REG_TMR_B_REG] = 0x00;

            currentRegister = 0;
            isFirstWrite = true;
        }

        #region II2CPeripheral Implementation

        public void Write(byte[] data)
        {
            if (data.Length == 0)
            {
                return;
            }

            if (isFirstWrite)
            {
                // First byte is the register address
                currentRegister = (byte)(data[0] & 0x1F);
                isFirstWrite = false;

                // Write any additional data bytes
                for (int i = 1; i < data.Length; i++)
                {
                    WriteRegister(data[i]);
                }
            }
            else
            {
                // Continuation of write, auto-increment registers
                foreach (byte b in data)
                {
                    WriteRegister(b);
                }
            }
        }

        private void WriteRegister(byte value)
        {
            if (currentRegister < registers.Length)
            {
                registers[currentRegister] = value;
                currentRegister++;
                currentRegister &= 0x1F; // Wrap around at 0x20
            }
        }

        public byte[] Read(int count = 1)
        {
            var result = new List<byte>();

            for (int i = 0; i < count; i++)
            {
                if (currentRegister < registers.Length)
                {
                    result.Add(registers[currentRegister]);
                    currentRegister++;
                    currentRegister &= 0x1F; // Wrap around at 0x20
                }
                else
                {
                    result.Add(0x00);
                }
            }

            return result.ToArray();
        }

        public void FinishTransmission()
        {
            // Reset state for next transaction
            isFirstWrite = true;
        }

        #endregion

        #region Public API for Test Configuration

        /// <summary>
        /// Set the time (hours, minutes, seconds) in BCD format.
        /// </summary>
        public void SetTime(int hours, int minutes, int seconds)
        {
            registers[REG_SECONDS] = DecimalToBcd(seconds);
            registers[REG_MINUTES] = DecimalToBcd(minutes);
            registers[REG_HOURS] = DecimalToBcd(hours);
        }

        /// <summary>
        /// Set the date (day, month, year, weekday) in BCD format.
        /// Year is 2-digit (0-99 for 2000-2099).
        /// Weekday: 0=Sunday, 1=Monday, ..., 6=Saturday
        /// </summary>
        public void SetDate(int day, int month, int year, int weekday)
        {
            registers[REG_DAYS] = DecimalToBcd(day);
            registers[REG_WEEKDAYS] = (byte)(weekday & 0x07);
            registers[REG_MONTHS] = DecimalToBcd(month);
            registers[REG_YEARS] = DecimalToBcd(year % 100);
        }

        /// <summary>
        /// Set the seconds (0-59).
        /// </summary>
        public void SetSeconds(int seconds)
        {
            registers[REG_SECONDS] = DecimalToBcd(seconds);
        }

        /// <summary>
        /// Set the minutes (0-59).
        /// </summary>
        public void SetMinutes(int minutes)
        {
            registers[REG_MINUTES] = DecimalToBcd(minutes);
        }

        /// <summary>
        /// Set the hours (0-23).
        /// </summary>
        public void SetHours(int hours)
        {
            registers[REG_HOURS] = DecimalToBcd(hours);
        }

        /// <summary>
        /// Set the day of month (1-31).
        /// </summary>
        public void SetDay(int day)
        {
            registers[REG_DAYS] = DecimalToBcd(day);
        }

        /// <summary>
        /// Set the weekday (0=Sunday, 1=Monday, ..., 6=Saturday).
        /// </summary>
        public void SetWeekday(int weekday)
        {
            registers[REG_WEEKDAYS] = (byte)(weekday & 0x07);
        }

        /// <summary>
        /// Set the month (1-12).
        /// </summary>
        public void SetMonth(int month)
        {
            registers[REG_MONTHS] = DecimalToBcd(month);
        }

        /// <summary>
        /// Set the year (0-99 for 2000-2099).
        /// </summary>
        public void SetYear(int year)
        {
            registers[REG_YEARS] = DecimalToBcd(year % 100);
        }

        /// <summary>
        /// Get the current seconds.
        /// </summary>
        public int GetSeconds()
        {
            return BcdToDecimal(registers[REG_SECONDS]);
        }

        /// <summary>
        /// Get the current minutes.
        /// </summary>
        public int GetMinutes()
        {
            return BcdToDecimal(registers[REG_MINUTES]);
        }

        /// <summary>
        /// Get the current hours.
        /// </summary>
        public int GetHours()
        {
            return BcdToDecimal(registers[REG_HOURS]);
        }

        /// <summary>
        /// Get the current day of month.
        /// </summary>
        public int GetDay()
        {
            return BcdToDecimal(registers[REG_DAYS]);
        }

        /// <summary>
        /// Get the current weekday (0=Sunday, 1=Monday, ..., 6=Saturday).
        /// </summary>
        public int GetWeekday()
        {
            return registers[REG_WEEKDAYS] & 0x07;
        }

        /// <summary>
        /// Get the current month (1-12).
        /// </summary>
        public int GetMonth()
        {
            return BcdToDecimal(registers[REG_MONTHS]);
        }

        /// <summary>
        /// Get the current year (0-99 for 2000-2099).
        /// </summary>
        public int GetYear()
        {
            return BcdToDecimal(registers[REG_YEARS]);
        }

        /// <summary>
        /// Check if alarm is triggered (bit 3 of Control_2).
        /// </summary>
        public bool IsAlarmTriggered()
        {
            return (registers[REG_CONTROL_2] & 0x08) != 0;
        }

        /// <summary>
        /// Set alarm triggered flag.
        /// </summary>
        public void SetAlarmTriggered(bool triggered)
        {
            if (triggered)
            {
                registers[REG_CONTROL_2] |= 0x08;
            }
            else
            {
                registers[REG_CONTROL_2] &= unchecked((byte)(~0x08));
            }
        }

        /// <summary>
        /// Get the time as a formatted string (HH:MM:SS).
        /// </summary>
        public string GetTimeString()
        {
            return $"{GetHours():D2}:{GetMinutes():D2}:{GetSeconds():D2}";
        }

        /// <summary>
        /// Get the date as a formatted string (DD/MM/YY).
        /// </summary>
        public string GetDateString()
        {
            return $"{GetDay():D2}/{GetMonth():D2}/{GetYear():D2}";
        }

        /// <summary>
        /// Write a value directly to a register (for testing/debugging).
        /// </summary>
        public void WriteDirect(byte register, byte value)
        {
            if (register < registers.Length)
            {
                registers[register] = value;
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Convert decimal number to BCD (Binary Coded Decimal).
        /// </summary>
        private byte DecimalToBcd(int value)
        {
            int tens = value / 10;
            int units = value % 10;
            return (byte)((tens << 4) | units);
        }

        /// <summary>
        /// Convert BCD (Binary Coded Decimal) to decimal number.
        /// </summary>
        private int BcdToDecimal(byte bcd)
        {
            int tens = (bcd >> 4) & 0x0F;
            int units = bcd & 0x0F;
            return tens * 10 + units;
        }

        #endregion

        private byte[] registers;
        private byte currentRegister;
        private bool isFirstWrite;
    }
}
