/**
 * lcd1602_i2c.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Collections.Generic;
using System.Text;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.I2C
{
    /// <summary>
    /// LCD 1602 (16x2 character LCD) with I2C backpack (PCF8574-based).
    /// Default I2C address is 0x27 (configurable via A0-A2 pins: 0x20-0x27).
    /// 
    /// The PCF8574 I/O expander connects to the LCD:
    /// - P0-P3: Data bits D4-D7 (upper nibble for HD44780)
    /// - P4: RS (Register Select) - 0=Command, 1=Data
    /// - P5: RW (Read/Write) - typically grounded for write-only
    /// - P6: E (Enable) - toggled to latch data
    /// - P7: Backlight control - 0=off, 1=on
    /// </summary>
    public class LCD1602_I2C : II2CPeripheral
    {
        // PCF8574 pin mapping to LCD
        private const byte PIN_D4 = 0x01;  // P0 - Data bit 4
        private const byte PIN_D5 = 0x02;  // P1 - Data bit 5
        private const byte PIN_D6 = 0x04;  // P2 - Data bit 6
        private const byte PIN_D7 = 0x08;  // P3 - Data bit 7
        private const byte PIN_RS = 0x10;  // P4 - Register Select
        private const byte PIN_RW = 0x20;  // P5 - Read/Write (usually 0)
        private const byte PIN_E  = 0x40;  // P6 - Enable
        private const byte PIN_BL = 0x80;  // P7 - Backlight

        // HD44780 commands
        private const byte CMD_CLEAR_DISPLAY = 0x01;
        private const byte CMD_RETURN_HOME = 0x02;
        private const byte CMD_ENTRY_MODE_SET = 0x04;
        private const byte CMD_DISPLAY_CONTROL = 0x08;
        private const byte CMD_CURSOR_SHIFT = 0x10;
        private const byte CMD_FUNCTION_SET = 0x20;
        private const byte CMD_SET_CGRAM_ADDR = 0x40;
        private const byte CMD_SET_DDRAM_ADDR = 0x80;

        // Display control flags
        private const byte DISPLAY_ON = 0x04;
        private const byte CURSOR_ON = 0x02;
        private const byte BLINK_ON = 0x01;

        public LCD1602_I2C()
        {
            displayBuffer = new char[2, 16];  // 2 lines, 16 characters
            capturedMessages = new List<string>();
            capturedBytes = new List<byte>();
            Reset();
        }

        public void Reset()
        {
            // Clear display buffer
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 16; col++)
                {
                    displayBuffer[row, col] = ' ';
                }
            }

            // Clear captured data
            capturedMessages.Clear();
            capturedBytes.Clear();
            currentLine.Clear();

            // Reset state
            pcf8574Output = 0;
            backlightOn = false;
            displayEnabled = false;
            cursorEnabled = false;
            blinkEnabled = false;
            currentRow = 0;
            currentCol = 0;
            lastEnableState = false;
            highNibble = 0;
            awaitingLowNibble = false;
            
            this.Log(LogLevel.Debug, "LCD1602_I2C: Reset");
        }

        public void Write(byte[] data)
        {
            if (data.Length == 0)
            {
                return;
            }

            this.Log(LogLevel.Noisy, "LCD1602_I2C: Received {0} bytes", data.Length);

            foreach (byte b in data)
            {
                capturedBytes.Add(b);
                ProcessByte(b);
            }
        }

        private void ProcessByte(byte data)
        {
            // Store the PCF8574 output state
            pcf8574Output = data;

            // Check backlight state
            bool bl = (data & PIN_BL) != 0;
            if (bl != backlightOn)
            {
                backlightOn = bl;
                this.Log(LogLevel.Noisy, "LCD1602_I2C: Backlight {0}", backlightOn ? "ON" : "OFF");
            }

            // Check for E (Enable) rising edge
            bool enable = (data & PIN_E) != 0;
            if (enable && !lastEnableState)
            {
                // Rising edge - capture the nibble
                byte nibble = (byte)(data & 0x0F);
                bool rs = (data & PIN_RS) != 0;

                if (!awaitingLowNibble)
                {
                    // This is the high nibble
                    highNibble = nibble;
                    awaitingLowNibble = true;
                }
                else
                {
                    // This is the low nibble - combine to form the byte
                    byte fullByte = (byte)((highNibble << 4) | nibble);
                    awaitingLowNibble = false;
                    ProcessLCDByte(fullByte, rs);
                }
            }
            lastEnableState = enable;
        }

        private void ProcessLCDByte(byte data, bool isData)
        {
            if (isData)
            {
                // Character data
                char c = (char)data;
                this.Log(LogLevel.Noisy, "LCD1602_I2C: Write char '{0}' (0x{1:X2}) at ({2}, {3})", 
                    c, data, currentRow, currentCol);

                if (currentRow < 2 && currentCol < 16)
                {
                    displayBuffer[currentRow, currentCol] = c;
                    currentLine.Append(c);
                    currentCol++;
                    
                    // Trigger event
                    CharacterWritten?.Invoke(c, currentRow, currentCol - 1);
                }
            }
            else
            {
                // Command
                ProcessCommand(data);
            }
        }

        private void ProcessCommand(byte cmd)
        {
            this.Log(LogLevel.Noisy, "LCD1602_I2C: Command 0x{0:X2}", cmd);

            if ((cmd & CMD_CLEAR_DISPLAY) == CMD_CLEAR_DISPLAY && cmd != 0)
            {
                // Clear display
                for (int row = 0; row < 2; row++)
                {
                    for (int col = 0; col < 16; col++)
                    {
                        displayBuffer[row, col] = ' ';
                    }
                }
                currentRow = 0;
                currentCol = 0;
                
                // Save current line if not empty
                if (currentLine.Length > 0)
                {
                    string msg = currentLine.ToString().Trim();
                    if (!string.IsNullOrEmpty(msg) && !capturedMessages.Contains(msg))
                    {
                        capturedMessages.Add(msg);
                        MessageChanged?.Invoke(msg);
                    }
                    currentLine.Clear();
                }
                
                this.Log(LogLevel.Debug, "LCD1602_I2C: Clear Display");
            }
            else if ((cmd & CMD_RETURN_HOME) == CMD_RETURN_HOME)
            {
                currentRow = 0;
                currentCol = 0;
                this.Log(LogLevel.Debug, "LCD1602_I2C: Return Home");
            }
            else if ((cmd & CMD_ENTRY_MODE_SET) == CMD_ENTRY_MODE_SET)
            {
                bool increment = (cmd & 0x02) != 0;
                bool shift = (cmd & 0x01) != 0;
                this.Log(LogLevel.Debug, "LCD1602_I2C: Entry Mode Set - Increment: {0}, Shift: {1}", 
                    increment, shift);
            }
            else if ((cmd & CMD_DISPLAY_CONTROL) == CMD_DISPLAY_CONTROL)
            {
                displayEnabled = (cmd & DISPLAY_ON) != 0;
                cursorEnabled = (cmd & CURSOR_ON) != 0;
                blinkEnabled = (cmd & BLINK_ON) != 0;
                this.Log(LogLevel.Debug, "LCD1602_I2C: Display Control - Display: {0}, Cursor: {1}, Blink: {2}",
                    displayEnabled, cursorEnabled, blinkEnabled);
            }
            else if ((cmd & CMD_CURSOR_SHIFT) == CMD_CURSOR_SHIFT)
            {
                bool right = (cmd & 0x04) != 0;
                bool displayShift = (cmd & 0x08) != 0;
                this.Log(LogLevel.Debug, "LCD1602_I2C: Cursor Shift - Right: {0}, Display: {1}",
                    right, displayShift);
            }
            else if ((cmd & CMD_FUNCTION_SET) == CMD_FUNCTION_SET)
            {
                bool eightBit = (cmd & 0x10) != 0;
                bool twoLines = (cmd & 0x08) != 0;
                bool fiveByTen = (cmd & 0x04) != 0;
                this.Log(LogLevel.Debug, "LCD1602_I2C: Function Set - 8-bit: {0}, 2 Lines: {1}, 5x10: {2}",
                    eightBit, twoLines, fiveByTen);
            }
            else if ((cmd & CMD_SET_CGRAM_ADDR) == CMD_SET_CGRAM_ADDR)
            {
                byte addr = (byte)(cmd & 0x3F);
                this.Log(LogLevel.Debug, "LCD1602_I2C: Set CGRAM Address 0x{0:X2}", addr);
            }
            else if ((cmd & CMD_SET_DDRAM_ADDR) == CMD_SET_DDRAM_ADDR)
            {
                byte addr = (byte)(cmd & 0x7F);
                // Map DDRAM address to row/col
                if (addr < 0x40)
                {
                    currentRow = 0;
                    currentCol = addr;
                }
                else
                {
                    currentRow = 1;
                    currentCol = (byte)(addr - 0x40);
                }
                this.Log(LogLevel.Debug, "LCD1602_I2C: Set DDRAM Address 0x{0:X2} -> ({1}, {2})", 
                    addr, currentRow, currentCol);
            }
        }

        public byte[] Read(int count = 1)
        {
            // PCF8574 is write-only in this context
            return new byte[count];
        }

        public void FinishTransmission()
        {
            // Transmission complete - save any pending message
            if (currentLine.Length > 0)
            {
                string msg = currentLine.ToString().Trim();
                if (!string.IsNullOrEmpty(msg) && !capturedMessages.Contains(msg))
                {
                    capturedMessages.Add(msg);
                    MessageChanged?.Invoke(msg);
                }
            }
        }

        /// <summary>
        /// Get the current display content as a 2-line string array.
        /// </summary>
        public string[] GetDisplayContent()
        {
            string[] lines = new string[2];
            for (int row = 0; row < 2; row++)
            {
                StringBuilder sb = new StringBuilder();
                for (int col = 0; col < 16; col++)
                {
                    sb.Append(displayBuffer[row, col]);
                }
                lines[row] = sb.ToString();
            }
            return lines;
        }

        /// <summary>
        /// Get the display content of a specific line (0 or 1).
        /// </summary>
        public string GetLine(int row)
        {
            if (row < 0 || row > 1) return string.Empty;
            StringBuilder sb = new StringBuilder();
            for (int col = 0; col < 16; col++)
            {
                sb.Append(displayBuffer[row, col]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Get all captured messages (strings that were displayed before clear).
        /// </summary>
        public string[] GetCapturedMessages()
        {
            return capturedMessages.ToArray();
        }

        /// <summary>
        /// Get the last captured message.
        /// </summary>
        public string GetLastMessage()
        {
            if (capturedMessages.Count > 0)
            {
                return capturedMessages[capturedMessages.Count - 1];
            }
            return string.Empty;
        }

        /// <summary>
        /// Get all raw captured bytes for debugging.
        /// </summary>
        public byte[] GetCapturedBytes()
        {
            return capturedBytes.ToArray();
        }

        /// <summary>
        /// Check if the display is enabled.
        /// </summary>
        public bool IsDisplayEnabled => displayEnabled;

        /// <summary>
        /// Check if backlight is on.
        /// </summary>
        public bool IsBacklightOn => backlightOn;

        /// <summary>
        /// Event fired when a message is captured (display cleared after text).
        /// </summary>
        public event Action<string> MessageChanged;

        /// <summary>
        /// Event fired when a character is written to the display.
        /// </summary>
        public event Action<char, int, int> CharacterWritten;

        private char[,] displayBuffer;
        private List<string> capturedMessages;
        private List<byte> capturedBytes;
        private StringBuilder currentLine = new StringBuilder();

        private byte pcf8574Output;
        private bool backlightOn;
        private bool displayEnabled;
        private bool cursorEnabled;
        private bool blinkEnabled;
        private int currentRow;
        private int currentCol;
        private bool lastEnableState;
        private byte highNibble;
        private bool awaitingLowNibble;
    }
}
