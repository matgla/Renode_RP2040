/**
 * ssd1306.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.I2C
{
    /// <summary>
    /// SSD1306 OLED Display driver (128x32 or 128x64).
    /// Supports I2C interface at address 0x3C.
    /// Provides framebuffer capture as BMP/PNG.
    /// </summary>
    public class SSD1306 : II2CPeripheral
    {
        private const int WIDTH = 128;
        private const int HEIGHT = 32;
        private const int PAGES = HEIGHT / 8;  // 4 pages for 32px height
        private const int BUFFER_SIZE = WIDTH * PAGES; // 512 bytes

        // Commands
        private const byte SET_MEM_MODE = 0x20;
        private const byte SET_COL_ADDR = 0x21;
        private const byte SET_PAGE_ADDR = 0x22;
        private const byte SET_HORIZ_SCROLL = 0x26;
        private const byte SET_SCROLL = 0x2E;
        private const byte SET_DISP_START_LINE = 0x40;
        private const byte SET_CONTRAST = 0x81;
        private const byte SET_CHARGE_PUMP = 0x8D;
        private const byte SET_SEG_REMAP = 0xA0;
        private const byte SET_ENTIRE_ON = 0xA4;
        private const byte SET_ALL_ON = 0xA5;
        private const byte SET_NORM_DISP = 0xA6;
        private const byte SET_INV_DISP = 0xA7;
        private const byte SET_MUX_RATIO = 0xA8;
        private const byte SET_DISP = 0xAE;
        private const byte SET_COM_OUT_DIR = 0xC0;
        private const byte SET_DISP_OFFSET = 0xD3;
        private const byte SET_DISP_CLK_DIV = 0xD5;
        private const byte SET_PRECHARGE = 0xD9;
        private const byte SET_COM_PIN_CFG = 0xDA;
        private const byte SET_VCOM_DESEL = 0xDB;

        // Control bytes
        private const byte CTRL_CMD = 0x80;   // Co=1, D/C=0 (command)
        private const byte CTRL_DATA = 0x40;  // Co=0, D/C=1 (data)
        private const byte CTRL_CMD_SINGLE = 0x00; // Co=0, D/C=0 (single command)

        public SSD1306()
        {
            framebuffer = new byte[BUFFER_SIZE];
            displayOn = false;
            inverted = false;
            memoryMode = 0; // horizontal
            Reset();
        }

        public void Reset()
        {
            Array.Clear(framebuffer, 0, framebuffer.Length);
            displayOn = false;
            inverted = false;
            memoryMode = 0;
            columnStart = 0;
            columnEnd = (byte)(WIDTH - 1);
            pageStart = 0;
            pageEnd = (byte)(PAGES - 1);
            currentColumn = 0;
            currentPage = 0;
            expectingControl = true;
            pendingCommand = 0;
            expectingCommandData = false;
        }

        public void Write(byte[] data)
        {
            foreach (byte b in data)
            {
                ProcessByte(b);
            }
        }

        private void ProcessByte(byte b)
        {
            if (expectingControl)
            {
                // This is a control byte
                if ((b & 0x80) != 0)
                {
                    // Co = 1, next byte is command
                    expectingControl = false;
                    nextByteIsCommand = true;
                }
                else if ((b & 0x40) != 0)
                {
                    // Co = 0, D/C = 1, stream of data follows
                    expectingControl = false;
                    nextByteIsCommand = false;
                }
                else
                {
                    // Co = 0, D/C = 0, single command
                    expectingControl = false;
                    nextByteIsCommand = true;
                    singleCommand = true;
                }
            }
            else if (nextByteIsCommand)
            {
                ProcessCommand(b);
                if (singleCommand)
                {
                    expectingControl = true;
                    singleCommand = false;
                }
            }
            else
            {
                // Data byte
                WriteData(b);
                // In data stream mode, continue accepting data
            }
        }

        private void ProcessCommand(byte cmd)
        {
            if (expectingCommandData)
            {
                // This is data for the previous command
                switch (pendingCommand)
                {
                    case SET_MEM_MODE:
                        memoryMode = cmd;
                        break;
                    case SET_COL_ADDR:
                        if (columnAddrStep == 0)
                        {
                            columnStart = cmd;
                            columnAddrStep = 1;
                            return; // Expect more data
                        }
                        else
                        {
                            columnEnd = cmd;
                            columnAddrStep = 0;
                            currentColumn = columnStart;
                        }
                        break;
                    case SET_PAGE_ADDR:
                        if (pageAddrStep == 0)
                        {
                            pageStart = cmd;
                            pageAddrStep = 1;
                            return; // Expect more data
                        }
                        else
                        {
                            pageEnd = cmd;
                            pageAddrStep = 0;
                            currentPage = pageStart;
                        }
                        break;
                    case SET_DISP_OFFSET:
                        displayOffset = cmd;
                        break;
                    case SET_MUX_RATIO:
                        multiplexRatio = cmd;
                        break;
                    case SET_DISP_CLK_DIV:
                        displayClockDiv = cmd;
                        break;
                    case SET_PRECHARGE:
                        precharge = cmd;
                        break;
                    case SET_COM_PIN_CFG:
                        comPinConfig = cmd;
                        break;
                    case SET_VCOM_DESEL:
                        vcomDeselect = cmd;
                        break;
                    case SET_CONTRAST:
                        contrast = cmd;
                        break;
                    case SET_CHARGE_PUMP:
                        chargePump = cmd;
                        break;
                    case SET_HORIZ_SCROLL:
                        // Horizontal scroll has 6 more bytes
                        if (scrollStep < 6)
                        {
                            scrollStep++;
                            return; // Expect more data
                        }
                        scrollStep = 0;
                        break;
                }
                expectingCommandData = false;
                pendingCommand = 0;
                return;
            }

            // Check for commands that expect data
            switch (cmd)
            {
                case SET_MEM_MODE:
                case SET_COL_ADDR:
                case SET_PAGE_ADDR:
                case SET_DISP_OFFSET:
                case SET_MUX_RATIO:
                case SET_DISP_CLK_DIV:
                case SET_PRECHARGE:
                case SET_COM_PIN_CFG:
                case SET_VCOM_DESEL:
                case SET_CONTRAST:
                case SET_CHARGE_PUMP:
                case SET_HORIZ_SCROLL:
                    pendingCommand = cmd;
                    expectingCommandData = true;
                    if (cmd == SET_COL_ADDR) columnAddrStep = 0;
                    if (cmd == SET_PAGE_ADDR) pageAddrStep = 0;
                    if (cmd == SET_HORIZ_SCROLL) scrollStep = 0;
                    return;
            }

            // Single-byte commands
            if ((cmd & 0xFE) == SET_DISP_START_LINE)
            {
                startLine = (byte)(cmd & 0x3F);
            }
            else if ((cmd & 0xFE) == SET_SEG_REMAP)
            {
                segRemap = (byte)(cmd & 0x01);
            }
            else if ((cmd & 0xFE) == SET_COM_OUT_DIR)
            {
                comOutDir = (byte)(cmd & 0x08);
            }
            else if ((cmd & 0xFE) == SET_DISP)
            {
                displayOn = (cmd & 0x01) != 0;
            }
            else if ((cmd & 0xFE) == SET_SCROLL)
            {
                scrolling = (cmd & 0x01) != 0;
            }
            else
            {
                switch (cmd)
                {
                    case SET_ENTIRE_ON:
                        entireDisplayOn = false;
                        break;
                    case SET_ALL_ON:
                        entireDisplayOn = true;
                        break;
                    case SET_NORM_DISP:
                        inverted = false;
                        break;
                    case SET_INV_DISP:
                        inverted = true;
                        break;
                }
            }
        }

        private void WriteData(byte data)
        {
            if (currentPage < PAGES && currentColumn < WIDTH)
            {
                int idx = currentPage * WIDTH + currentColumn;
                framebuffer[idx] = data;

                // Auto-increment column
                currentColumn++;
                if (currentColumn > columnEnd)
                {
                    currentColumn = columnStart;
                    currentPage++;
                    if (currentPage > pageEnd)
                    {
                        currentPage = pageStart;
                    }
                }
            }
        }

        public byte[] Read(int count = 1)
        {
            // SSD1306 doesn't support read in I2C mode
            return new byte[count];
        }

        public void FinishTransmission()
        {
            expectingControl = true;
            nextByteIsCommand = false;
            singleCommand = false;
        }

        /// <summary>
        /// Save the current framebuffer as a BMP file.
        /// </summary>
        public void SaveAsBmp(string filename)
        {
            using (var fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(fs))
            {
                // BMP Header (14 bytes)
                writer.Write((byte)'B');
                writer.Write((byte)'M');
                
                int rowSize = ((WIDTH + 31) / 32) * 4; // 4-byte aligned
                int imageSize = rowSize * HEIGHT;
                int fileSize = 14 + 40 + 8 + imageSize; // header + DIB + palette + pixels
                
                writer.Write(fileSize);
                writer.Write(0); // Reserved
                writer.Write(14 + 40 + 8); // Offset to pixel data
                
                // DIB Header (BITMAPINFOHEADER, 40 bytes)
                writer.Write(40); // Header size
                writer.Write(WIDTH);
                writer.Write(HEIGHT);
                writer.Write((short)1); // Planes
                writer.Write((short)1); // Bits per pixel (monochrome)
                writer.Write(0); // Compression (none)
                writer.Write(imageSize);
                writer.Write(2835); // X pixels per meter
                writer.Write(2835); // Y pixels per meter
                writer.Write(2); // Colors in palette
                writer.Write(0); // Important colors
                
                // Color palette (2 colors x 4 bytes)
                writer.Write(0x00000000); // Black (BGR)
                writer.Write(0x00FFFFFF); // White (BGR)
                
                // Pixel data (bottom-up, 1bpp)
                for (int y = HEIGHT - 1; y >= 0; y--)
                {
                    byte rowByte = 0;
                    int bitPos = 7;
                    
                    for (int x = 0; x < WIDTH; x++)
                    {
                        // Get pixel from framebuffer (pages are 8 pixels high)
                        int page = y / 8;
                        int bit = y % 8;
                        int idx = page * WIDTH + x;
                        bool pixelOn = (framebuffer[idx] & (1 << bit)) != 0;
                        
                        if (inverted) pixelOn = !pixelOn;
                        
                        if (pixelOn)
                            rowByte |= (byte)(1 << bitPos);
                        
                        bitPos--;
                        if (bitPos < 0)
                        {
                            writer.Write(rowByte);
                            rowByte = 0;
                            bitPos = 7;
                        }
                    }
                    
                    // Write remaining byte and padding
                    if (bitPos != 7)
                        writer.Write(rowByte);
                    
                    // Pad to 4-byte boundary
                    int padding = rowSize - (WIDTH + 7) / 8;
                    for (int p = 0; p < padding; p++)
                        writer.Write((byte)0);
                }
            }
            
            this.Log(LogLevel.Info, "SSD1306: Saved display to {0}", filename);
        }

        /// <summary>
        /// Save the current framebuffer as a PNM (PBM) file.
        /// </summary>
        public void SaveAsPnm(string filename)
        {
            using (var writer = new StreamWriter(filename))
            {
                writer.WriteLine("P1");
                writer.WriteLine($"{WIDTH} {HEIGHT}");
                
                for (int y = 0; y < HEIGHT; y++)
                {
                    for (int x = 0; x < WIDTH; x++)
                    {
                        int page = y / 8;
                        int bit = y % 8;
                        int idx = page * WIDTH + x;
                        bool pixelOn = (framebuffer[idx] & (1 << bit)) != 0;
                        
                        if (inverted) pixelOn = !pixelOn;
                        
                        writer.Write(pixelOn ? "1 " : "0 ");
                    }
                    writer.WriteLine();
                }
            }
            
            this.Log(LogLevel.Info, "SSD1306: Saved display to {0}", filename);
        }

        /// <summary>
        /// Get the current framebuffer content as a byte array.
        /// </summary>
        public byte[] GetFramebuffer()
        {
            byte[] copy = new byte[BUFFER_SIZE];
            Array.Copy(framebuffer, copy, BUFFER_SIZE);
            return copy;
        }

        /// <summary>
        /// Check if display is on.
        /// </summary>
        public bool IsDisplayOn => displayOn;

        private byte[] framebuffer;
        private bool displayOn;
        private bool inverted;
        private bool entireDisplayOn;
        private bool scrolling;
        private byte memoryMode;
        private byte columnStart, columnEnd;
        private byte pageStart, pageEnd;
        private byte currentColumn, currentPage;
        private byte startLine;
        private byte segRemap;
        private byte comOutDir;
        private byte displayOffset;
        private byte multiplexRatio;
        private byte displayClockDiv;
        private byte precharge;
        private byte comPinConfig;
        private byte vcomDeselect;
        private byte contrast;
        private byte chargePump;
        
        private bool expectingControl = true;
        private bool nextByteIsCommand = false;
        private bool singleCommand = false;
        private bool expectingCommandData = false;
        private byte pendingCommand;
        private int columnAddrStep;
        private int pageAddrStep;
        private int scrollStep;
    }
}
