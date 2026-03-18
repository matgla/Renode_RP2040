/**
 * ht16k33.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.I2C
{
    /// <summary>
    /// HT16K33 16x8 LED Matrix Driver with I2C interface.
    /// Default I2C address is 0x70 (configurable via A0-A2 pins).
    /// </summary>
    public class HT16K33 : II2CPeripheral
    {
        // Register addresses
        private const byte REG_DISPLAY_RAM_START = 0x00;  // Display RAM: 0x00 - 0x0F (16 bytes)
        private const byte REG_DISPLAY_RAM_END = 0x0F;
        
        // Command codes
        private const byte CMD_SYSTEM_SETUP = 0x20;       // System setup (OSC on/off)
        private const byte CMD_KEYSCAN_START = 0x40;      // Key scan data: 0x40 - 0x45
        private const byte CMD_INT_FLAG = 0x60;           // INT flag: 0x60 - 0x61
        private const byte CMD_DISPLAY_SETUP = 0x80;      // Display setup (on/off, blink)
        private const byte CMD_ROW_INT_SET = 0xA0;        // ROW/INT set
        private const byte CMD_DIMMING_SET = 0xE0;        // Dimming set: 0xE0 - 0xEF

        public HT16K33()
        {
            displayRam = new byte[16];      // 16 bytes of display RAM (16 rows x 8 COMs)
            keyScanData = new byte[6];      // Key scan data (3x13 matrix = 39 keys)
            intFlag = new byte[2];          // INT flag registers
            capturedWrites = new List<byte[]>();
            Reset();
        }

        public void Reset()
        {
            // Clear display RAM
            for (int i = 0; i < displayRam.Length; i++)
            {
                displayRam[i] = 0;
            }

            // Clear key scan data
            for (int i = 0; i < keyScanData.Length; i++)
            {
                keyScanData[i] = 0;
            }

            // Clear INT flag
            intFlag[0] = 0;
            intFlag[1] = 0;

            // Clear captured writes
            capturedWrites.Clear();

            // Default settings
            oscillatorEnabled = false;
            displayEnabled = false;
            blinkRate = BlinkRate.Off;
            dimmingLevel = 15;  // Full brightness
            rowIntMode = false;
            
            currentRegister = 0;
            isCommandPending = false;
            pendingCommand = 0;
        }

        public void Write(byte[] data)
        {
            if (data.Length == 0)
            {
                return;
            }

            // Capture the write for verification
            capturedWrites.Add((byte[])data.Clone());
            
            // Trigger event for testers
            DataWritten?.Invoke(data);

            foreach (byte b in data)
            {
                ProcessByte(b);
            }
        }

        private void ProcessByte(byte data)
        {
            // Check if this is a command byte (MSB is set)
            if ((data & 0x80) != 0 || (data & 0xE0) == 0x20 || (data & 0xF0) == 0xA0)
            {
                // This is a command byte
                ProcessCommand(data);
            }
            else if (currentRegister <= REG_DISPLAY_RAM_END)
            {
                // Write to display RAM
                displayRam[currentRegister] = data;
                currentRegister++;
                if (currentRegister > REG_DISPLAY_RAM_END)
                {
                    currentRegister = 0;  // Wrap around
                }
            }
        }

        private void ProcessCommand(byte cmd)
        {
            // System setup command (0x20 - 0x21)
            if ((cmd & 0xFE) == CMD_SYSTEM_SETUP)
            {
                oscillatorEnabled = (cmd & 0x01) != 0;
                this.Log(LogLevel.Noisy, "HT16K33: Oscillator {0}", oscillatorEnabled ? "ON" : "OFF");
                return;
            }

            // Display setup command (0x80 - 0x87)
            if ((cmd & 0xF8) == CMD_DISPLAY_SETUP)
            {
                displayEnabled = (cmd & 0x01) != 0;
                blinkRate = (BlinkRate)((cmd >> 1) & 0x03);
                this.Log(LogLevel.Noisy, "HT16K33: Display {0}, Blink rate: {1}", 
                    displayEnabled ? "ON" : "OFF", blinkRate);
                return;
            }

            // Dimming set command (0xE0 - 0xEF)
            if ((cmd & 0xF0) == CMD_DIMMING_SET)
            {
                dimmingLevel = (byte)(cmd & 0x0F);
                this.Log(LogLevel.Noisy, "HT16K33: Dimming level set to {0}", dimmingLevel);
                return;
            }

            // Row/INT set command (0xA0 - 0xA3)
            if ((cmd & 0xFC) == CMD_ROW_INT_SET)
            {
                rowIntMode = (cmd & 0x01) != 0;
                bool actInt = (cmd & 0x02) != 0;
                this.Log(LogLevel.Noisy, "HT16K33: ROW/INT mode set");
                return;
            }

            // Display RAM pointer (0x00 - 0x0F)
            if ((cmd & 0xF0) == 0x00)
            {
                currentRegister = (byte)(cmd & 0x0F);
                this.Log(LogLevel.Noisy, "HT16K33: Display RAM pointer set to 0x{0:X2}", currentRegister);
                return;
            }

            // Key scan data read pointer (0x40 - 0x45)
            if ((cmd & 0xF8) == CMD_KEYSCAN_START && (cmd & 0x07) <= 5)
            {
                currentRegister = (byte)(0x40 | (cmd & 0x07));
                this.Log(LogLevel.Noisy, "HT16K33: Key scan pointer set");
                return;
            }

            // INT flag read pointer (0x60 - 0x61)
            if ((cmd & 0xFE) == CMD_INT_FLAG)
            {
                currentRegister = (byte)(0x60 | (cmd & 0x01));
                this.Log(LogLevel.Noisy, "HT16K33: INT flag pointer set");
                return;
            }
        }

        public byte[] Read(int count = 1)
        {
            var result = new List<byte>();

            for (int i = 0; i < count; i++)
            {
                if (currentRegister <= REG_DISPLAY_RAM_END)
                {
                    // Read from display RAM
                    result.Add(displayRam[currentRegister]);
                    currentRegister++;
                    if (currentRegister > REG_DISPLAY_RAM_END)
                    {
                        currentRegister = 0;
                    }
                }
                else if (currentRegister >= 0x40 && currentRegister <= 0x45)
                {
                    // Read key scan data
                    int index = currentRegister - 0x40;
                    result.Add(keyScanData[index]);
                    currentRegister++;
                    if (currentRegister > 0x45)
                    {
                        currentRegister = 0x40;
                    }
                }
                else if (currentRegister >= 0x60 && currentRegister <= 0x61)
                {
                    // Read INT flag
                    int index = currentRegister - 0x60;
                    result.Add(intFlag[index]);
                    currentRegister++;
                    if (currentRegister > 0x61)
                    {
                        currentRegister = 0x60;
                    }
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
            currentRegister = 0;
            isCommandPending = false;
        }

        /// <summary>
        /// Get the current display RAM content for a specific row (0-15).
        /// </summary>
        public byte GetRowData(int row)
        {
            if (row >= 0 && row < 16)
            {
                return displayRam[row];
            }
            return 0;
        }

        /// <summary>
        /// Set key scan data to simulate key presses.
        /// </summary>
        public void SetKeyScanData(int index, byte data)
        {
            if (index >= 0 && index < 6)
            {
                keyScanData[index] = data;
            }
        }

        /// <summary>
        /// Get all captured I2C writes for verification.
        /// </summary>
        public byte[][] GetCapturedWrites()
        {
            return capturedWrites.ToArray();
        }

        /// <summary>
        /// Clear captured writes.
        /// </summary>
        public void ClearCapturedWrites()
        {
            capturedWrites.Clear();
        }

        /// <summary>
        /// Check if the display is enabled.
        /// </summary>
        public bool IsDisplayEnabled => displayEnabled;

        /// <summary>
        /// Get the current blink rate.
        /// </summary>
        public BlinkRate CurrentBlinkRate => blinkRate;

        /// <summary>
        /// Get the current dimming level (0-15).
        /// </summary>
        public byte CurrentDimmingLevel => dimmingLevel;

        /// <summary>
        /// Event fired when data is written to the device.
        /// </summary>
        public event Action<byte[]> DataWritten;

        public enum BlinkRate : byte
        {
            Off = 0,
            Hz2 = 1,      // 2 Hz
            Hz1 = 2,      // 1 Hz
            Hz0_5 = 3     // 0.5 Hz
        }

        private byte[] displayRam;
        private byte[] keyScanData;
        private byte[] intFlag;
        private List<byte[]> capturedWrites;
        private byte currentRegister;
        private bool isCommandPending;
        private byte pendingCommand;
        
        // Settings
        private bool oscillatorEnabled;
        private bool displayEnabled;
        private BlinkRate blinkRate;
        private byte dimmingLevel;
        private bool rowIntMode;
    }
}
