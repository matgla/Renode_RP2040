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
    public class PCF8523 : II2CPeripheral
    {
        public PCF8523()
        {
            Reset();
        }

        public void Reset()
        {
            registers = new byte[0x20];
            // Initialize with default values per datasheet
            registers[0x00] = 0x58; // Seconds (BCD: 58 seconds)
            registers[0x01] = 0x59; // Minutes (BCD: 59 minutes)
            registers[0x02] = 0x14; // Hours (BCD: 14 hours)
            registers[0x03] = 0x03; // Days (BCD: 3)
            registers[0x04] = 0x21; // Weekdays
            registers[0x05] = 0x06; // Months (BCD: June)
            registers[0x06] = 0x24; // Years (BCD: 24)
            registers[0x07] = 0x00; // Minute alarm
            registers[0x08] = 0x00; // Hour alarm
            registers[0x09] = 0x00; // Day alarm
            registers[0x0A] = 0x00; // Weekday alarm
            registers[0x0B] = 0x00; // Offset register
            registers[0x0C] = 0x00; // Tmr_CLKOUT_ctrl
            registers[0x0D] = 0x00; // Tmr_A_freq_ctrl
            registers[0x0E] = 0x00; // Tmr_A_reg
            registers[0x0F] = 0x00; // Tmr_B_freq_ctrl
            registers[0x10] = 0x00; // Tmr_B_reg
            registers[0x11] = 0x00; // Control_1
            registers[0x12] = 0x00; // Control_2
            registers[0x13] = 0x00; // Control_3

            currentRegister = 0;
            isFirstWrite = true;
        }

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

        private byte[] registers;
        private byte currentRegister;
        private bool isFirstWrite;
    }
}
