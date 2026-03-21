/**
 * pa1010d.cs
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
    /// PA1010D Mini GPS Module I2C device simulator.
    /// Default I2C address is 0x10.
    /// 
    /// This GPS module outputs NMEA 0183 sentences containing location and time data.
    /// Supports GNRMC (Recommended Minimum Specific GNSS Sentence) protocol.
    /// </summary>
    public class PA1010D : II2CPeripheral
    {
        // Default I2C address
        private const byte DEFAULT_ADDRESS = 0x10;
        
        // Maximum read buffer size
        private const int MAX_READ = 250;

        public PA1010D()
        {
            Reset();
        }

        public void Reset()
        {
            // Initialize with default GPS data
            utcTime = "123519.00";
            status = 'A';  // A = Data valid, V = Data invalid
            latitude = "4807.038";
            nsIndicator = "N";
            longitude = "01131.000";
            ewIndicator = "E";
            speedOverGround = "022.4";
            courseOverGround = "084.4";
            date = "230394";
            magneticVariation = "003.1";
            magVarEwIndicator = "W";
            mode = "A";
            
            // Build initial NMEA buffer
            BuildNmeaBuffer();
            
            readIndex = 0;
        }

        #region II2CPeripheral Implementation

        public void Write(byte[] data)
        {
            if (data.Length == 0)
            {
                return;
            }

            // Convert bytes to ASCII and accumulate command
            foreach (byte b in data)
            {
                if (b != 0)
                {
                    commandBuffer.Append((char)b);
                }
            }

            // Check if we have a complete command (ends with \r\n)
            string cmd = commandBuffer.ToString();
            if (cmd.Contains("\r\n"))
            {
                this.Log(LogLevel.Debug, $"PA1010D received command: {cmd.Trim()}");
                
                // Process the command (in real device this would configure output)
                // For simulation, we just acknowledge by rebuilding the buffer
                BuildNmeaBuffer();
                
                // Clear command buffer
                commandBuffer.Clear();
            }
        }

        public byte[] Read(int count = 1)
        {
            var result = new List<byte>();

            for (int i = 0; i < count; i++)
            {
                if (readIndex < nmeaBuffer.Length)
                {
                    result.Add(nmeaBuffer[readIndex]);
                    readIndex++;
                }
                else
                {
                    // Return null bytes when buffer exhausted
                    result.Add(0);
                }
            }

            return result.ToArray();
        }

        public void FinishTransmission()
        {
            // Reset read index for next transaction
            readIndex = 0;
        }

        #endregion

        #region Public API for Test Configuration

        /// <summary>
        /// Set the UTC time string (format: HHMMSS.SS).
        /// </summary>
        public void SetUtcTime(string time)
        {
            utcTime = time;
            BuildNmeaBuffer();
        }

        /// <summary>
        /// Set the data status ('A' = valid, 'V' = invalid).
        /// </summary>
        public void SetStatus(char statusChar)
        {
            status = statusChar;
            BuildNmeaBuffer();
        }
        
        /// <summary>
        /// Set data as valid (status = 'A').
        /// </summary>
        public void SetDataValid()
        {
            SetStatus('A');
        }
        
        /// <summary>
        /// Set data as invalid (status = 'V').
        /// </summary>
        public void SetDataInvalid()
        {
            SetStatus('V');
        }

        /// <summary>
        /// Set the latitude (format: DDMM.MMMM).
        /// </summary>
        public void SetLatitude(string lat)
        {
            latitude = lat;
            BuildNmeaBuffer();
        }

        /// <summary>
        /// Set the N/S indicator ("N" or "S").
        /// </summary>
        public void SetNsIndicator(string ns)
        {
            nsIndicator = ns;
            BuildNmeaBuffer();
        }

        /// <summary>
        /// Set the longitude (format: DDDMM.MMMM).
        /// </summary>
        public void SetLongitude(string lon)
        {
            longitude = lon;
            BuildNmeaBuffer();
        }

        /// <summary>
        /// Set the E/W indicator ("E" or "W").
        /// </summary>
        public void SetEwIndicator(string ew)
        {
            ewIndicator = ew;
            BuildNmeaBuffer();
        }

        /// <summary>
        /// Set the speed over ground in knots.
        /// </summary>
        public void SetSpeedOverGround(string speed)
        {
            speedOverGround = speed;
            BuildNmeaBuffer();
        }

        /// <summary>
        /// Set the course over ground in degrees.
        /// </summary>
        public void SetCourseOverGround(string course)
        {
            courseOverGround = course;
            BuildNmeaBuffer();
        }

        /// <summary>
        /// Set the date (format: DDMMYY).
        /// </summary>
        public void SetDate(string dateStr)
        {
            date = dateStr;
            BuildNmeaBuffer();
        }

        /// <summary>
        /// Set the magnetic variation in degrees.
        /// </summary>
        public void SetMagneticVariation(string magVar)
        {
            magneticVariation = magVar;
            BuildNmeaBuffer();
        }

        /// <summary>
        /// Set the magnetic variation E/W indicator.
        /// </summary>
        public void SetMagVarEwIndicator(string magEw)
        {
            magVarEwIndicator = magEw;
            BuildNmeaBuffer();
        }

        /// <summary>
        /// Set the mode indicator ('A', 'D', 'E', 'N', 'S').
        /// </summary>
        public void SetMode(string modeStr)
        {
            mode = modeStr;
            BuildNmeaBuffer();
        }

        /// <summary>
        /// Set all GPS data at once.
        /// </summary>
        public void SetGpsData(string time, char statusChar, string lat, string ns, 
                               string lon, string ew, string speed, string course,
                               string dateStr, string magVar, string magEw, string modeStr)
        {
            utcTime = time;
            status = statusChar;
            latitude = lat;
            nsIndicator = ns;
            longitude = lon;
            ewIndicator = ew;
            speedOverGround = speed;
            courseOverGround = course;
            date = dateStr;
            magneticVariation = magVar;
            magVarEwIndicator = magEw;
            mode = modeStr;
            BuildNmeaBuffer();
        }

        /// <summary>
        /// Get the current UTC time.
        /// </summary>
        public string GetUtcTime()
        {
            return utcTime;
        }

        /// <summary>
        /// Get the current status character.
        /// </summary>
        public char GetStatus()
        {
            return status;
        }

        /// <summary>
        /// Get the current latitude.
        /// </summary>
        public string GetLatitude()
        {
            return latitude;
        }

        /// <summary>
        /// Get the current longitude.
        /// </summary>
        public string GetLongitude()
        {
            return longitude;
        }

        /// <summary>
        /// Check if GPS data is valid (status = 'A').
        /// </summary>
        public bool IsDataValid()
        {
            return status == 'A';
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Build the NMEA GNRMC sentence from current data.
        /// Format: $GNRMC,time,status,lat,N/S,lon,E/W,speed,course,date,magVar,magEw,mode*checksum
        /// </summary>
        private void BuildNmeaBuffer()
        {
            // Build the NMEA sentence (without checksum)
            string sentence = $"GNRMC,{utcTime},{status},{latitude},{nsIndicator},{longitude},{ewIndicator},{speedOverGround},{courseOverGround},{date},{magneticVariation},{magVarEwIndicator},{mode}";
            
            // Calculate checksum (XOR of all bytes between $ and *)
            byte checksum = 0;
            foreach (char c in sentence)
            {
                checksum ^= (byte)c;
            }
            
            // Build complete NMEA sentence with $ prefix and checksum
            string completeSentence = $"${sentence}*{checksum:X2}\r\n";
            
            // Convert to byte array
            nmeaBuffer = Encoding.ASCII.GetBytes(completeSentence);
            
            // Pad to MAX_READ with null bytes followed by double line feed
            // (firmware stops reading when it sees 10, 10 sequence)
            var paddedBuffer = new List<byte>(nmeaBuffer);
            
            // Add null bytes to fill up to near MAX_READ, then add the terminator sequence
            while (paddedBuffer.Count < MAX_READ - 2)
            {
                paddedBuffer.Add(0);
            }
            
            // Add double line feed terminator (firmware checks for buffer[i] == 10 && buffer[i+1] == 10)
            paddedBuffer.Add(10);
            paddedBuffer.Add(10);
            
            nmeaBuffer = paddedBuffer.ToArray();
            readIndex = 0;
            
            this.Log(LogLevel.Debug, $"PA1010D built NMEA: {completeSentence.Trim()}");
        }

        #endregion

        // Internal fields
        private byte[] nmeaBuffer;
        private int readIndex;
        private StringBuilder commandBuffer = new StringBuilder();
        
        // GPS data fields
        private string utcTime;
        private char status;
        private string latitude;
        private string nsIndicator;
        private string longitude;
        private string ewIndicator;
        private string speedOverGround;
        private string courseOverGround;
        private string date;
        private string magneticVariation;
        private string magVarEwIndicator;
        private string mode;
    }
}
