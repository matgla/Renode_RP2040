/**
 * mpl3115a2.cs
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
    /// MPL3115A2 pressure/altitude and temperature sensor I2C device simulator.
    /// Default I2C address is 0x60.
    /// 
    /// This sensor provides altitude (20-bit Q16.4 format) and temperature (12-bit Q8.4 format) data.
    /// Supports FIFO mode for storing up to 32 samples.
    /// </summary>
    public class MPL3115A2 : II2CPeripheral
    {
        // Register addresses
        private const byte REG_F_STATUS = 0x00;      // FIFO status
        private const byte REG_F_DATA = 0x01;        // FIFO data access
        private const byte REG_F_SETUP = 0x0F;       // FIFO setup
        private const byte REG_INT_SOURCE = 0x12;    // Interrupt source
        private const byte REG_PT_DATA_CFG = 0x13;   // Data configuration
        private const byte REG_CTRL_REG1 = 0x26;     // Control register 1
        private const byte REG_CTRL_REG2 = 0x27;     // Control register 2
        private const byte REG_CTRL_REG3 = 0x28;     // Control register 3
        private const byte REG_CTRL_REG4 = 0x29;     // Control register 4
        private const byte REG_CTRL_REG5 = 0x2A;     // Control register 5
        private const byte REG_OFF_P = 0x2B;         // Pressure offset
        private const byte REG_OFF_T = 0x2C;         // Temperature offset
        private const byte REG_OFF_H = 0x2D;         // Altitude offset

        // FIFO constants
        private const int FIFO_SIZE = 32;
        private const int DATA_BATCH_SIZE = 5;  // 3 bytes altitude + 2 bytes temperature

        // Control register bits
        private const byte CTRL_REG1_SBYB = 0x01;  // Standby/Active mode
        private const byte CTRL_REG1_OST = 0x02;   // One-shot trigger
        private const byte CTRL_REG1_RST = 0x04;   // Software reset
        private const byte CTRL_REG1_ALT = 0x80;   // Altimeter/Barometer mode

        // FIFO setup bits
        private const byte F_SETUP_F_MODE1 = 0x40; // FIFO circular buffer mode
        private const byte F_SETUP_F_MODE2 = 0x80; // FIFO stop on overflow
        private const byte F_SETUP_F_MODE_MASK = 0xC0;

        // Default values
        private const byte WHO_AM_I_VALUE = 0xC4;  // MPL3115A2 device ID

        public MPL3115A2()
        {
            registers = new byte[256]; // Full register space
            fifoBuffer = new byte[FIFO_SIZE * DATA_BATCH_SIZE];
            Reset();
        }

        public void Reset()
        {
            // Clear all registers
            for (int i = 0; i < registers.Length; i++)
            {
                registers[i] = 0;
            }

            // Clear FIFO
            for (int i = 0; i < fifoBuffer.Length; i++)
            {
                fifoBuffer[i] = 0;
            }

            // Default control register values
            registers[REG_CTRL_REG1] = 0x00; // Standby mode
            registers[REG_CTRL_REG2] = 0x00;
            registers[REG_CTRL_REG3] = 0x00;
            registers[REG_CTRL_REG4] = 0x00;
            registers[REG_CTRL_REG5] = 0x00;
            registers[REG_F_SETUP] = 0x00;   // FIFO disabled
            registers[REG_PT_DATA_CFG] = 0x00;

            // Reset state
            currentRegister = 0;
            isFirstWrite = true;
            fifoReadIndex = 0;
            fifoWriteIndex = 0;
            fifoCount = 0;

            // Default sensor values
            altitudeM = 0.0f;      // Sea level
            temperatureC = 25.0f;  // Room temperature

            // Pre-fill FIFO with initial data
            FillFifoWithCurrentData();
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
                currentRegister = data[0];
                isFirstWrite = false;

                // Write any additional data bytes (register values)
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
            // Handle special registers
            if (currentRegister == REG_CTRL_REG1)
            {
                // Check for software reset
                if ((value & CTRL_REG1_RST) != 0)
                {
                    Reset();
                    return;
                }
                registers[REG_CTRL_REG1] = value;
            }
            else if (currentRegister == REG_F_SETUP)
            {
                registers[REG_F_SETUP] = value;
                // If FIFO mode changed, update FIFO
                UpdateFifoState();
            }
            else if (currentRegister == REG_F_DATA)
            {
                // F_DATA is read-only, ignore writes
            }
            else if (currentRegister == REG_F_STATUS)
            {
                // F_STATUS is read-only, ignore writes
            }
            else if (currentRegister == REG_INT_SOURCE)
            {
                // INT_SOURCE is read-only, ignore writes
            }
            else
            {
                // All other registers can be written
                registers[currentRegister] = value;
            }

            currentRegister++;
        }

        public byte[] Read(int count = 1)
        {
            var result = new List<byte>();

            for (int i = 0; i < count; i++)
            {
                // Check if we're reading FIFO status
                if (currentRegister == REG_F_STATUS)
                {
                    UpdateFifoStatus();
                }
                // Check if we're reading FIFO data
                // When reading from F_DATA, stay at F_DATA address for burst reads
                // The FIFO itself has internal pointer management via fifoReadIndex
                else if (currentRegister == REG_F_DATA)
                {
                    // Return next byte from FIFO
                    result.Add(ReadFifoByte());
                    // Don't increment currentRegister - stay at F_DATA for FIFO burst reads
                    continue;
                }
                // Check if we're reading interrupt source
                else if (currentRegister == REG_INT_SOURCE)
                {
                    UpdateInterruptSource();
                }

                // Read from current register
                if (currentRegister < registers.Length)
                {
                    result.Add(registers[currentRegister]);
                }
                else
                {
                    result.Add(0);
                }

                // Auto-increment register address
                currentRegister++;
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
        /// Set the altitude in meters.
        /// Range: -698 to 11682 meters
        /// </summary>
        public void SetAltitude(float meters)
        {
            altitudeM = meters;
            FillFifoWithCurrentData();
        }

        /// <summary>
        /// Set the temperature in Celsius.
        /// Range: -40°C to +85°C
        /// </summary>
        public void SetTemperature(float celsius)
        {
            temperatureC = celsius;
            FillFifoWithCurrentData();
        }

        /// <summary>
        /// Set both altitude and temperature at once.
        /// </summary>
        public void SetData(float altitudeMeters, float temperatureCelsius)
        {
            altitudeM = altitudeMeters;
            temperatureC = temperatureCelsius;
            FillFifoWithCurrentData();
        }

        /// <summary>
        /// Get the current altitude in meters.
        /// </summary>
        public float GetAltitude()
        {
            return altitudeM;
        }

        /// <summary>
        /// Get the current temperature in Celsius.
        /// </summary>
        public float GetTemperature()
        {
            return temperatureC;
        }

        /// <summary>
        /// Set the device to active mode.
        /// </summary>
        public void SetActive(bool active)
        {
            if (active)
            {
                registers[REG_CTRL_REG1] |= CTRL_REG1_SBYB;
            }
            else
            {
                registers[REG_CTRL_REG1] &= unchecked((byte)(~CTRL_REG1_SBYB));
            }
        }

        /// <summary>
        /// Check if device is in active mode.
        /// </summary>
        public bool IsActive()
        {
            return (registers[REG_CTRL_REG1] & CTRL_REG1_SBYB) != 0;
        }

        /// <summary>
        /// Set altimeter mode (true) or barometer mode (false).
        /// </summary>
        public void SetAltimeterMode(bool altimeter)
        {
            if (altimeter)
            {
                registers[REG_CTRL_REG1] |= CTRL_REG1_ALT;
            }
            else
            {
                registers[REG_CTRL_REG1] &= unchecked((byte)(~CTRL_REG1_ALT));
            }
        }

        /// <summary>
        /// Check if in altimeter mode (true) or barometer mode (false).
        /// </summary>
        public bool IsAltimeterMode()
        {
            return (registers[REG_CTRL_REG1] & CTRL_REG1_ALT) != 0;
        }

        /// <summary>
        /// Enable or disable FIFO.
        /// </summary>
        public void SetFifoEnabled(bool enabled)
        {
            if (enabled)
            {
                registers[REG_F_SETUP] |= F_SETUP_F_MODE2; // Stop on overflow mode
            }
            else
            {
                registers[REG_F_SETUP] &= unchecked((byte)(~F_SETUP_F_MODE_MASK));
            }
            UpdateFifoState();
        }

        /// <summary>
        /// Check if FIFO is enabled.
        /// </summary>
        public bool IsFifoEnabled()
        {
            return (registers[REG_F_SETUP] & F_SETUP_F_MODE_MASK) != 0;
        }

        /// <summary>
        /// Trigger a FIFO overflow (for testing interrupt handling).
        /// </summary>
        public void TriggerFifoOverflow()
        {
            // Fill FIFO to capacity
            FillFifoWithCurrentData();
            // Set overflow flag
            registers[REG_F_STATUS] = 0xC0; // F_OVF = 1, F_CNT = 32 (saturated)
            UpdateInterruptSource();
        }

        #endregion

        #region Private Helper Methods

        private void UpdateFifoState()
        {
            byte fifoMode = (byte)(registers[REG_F_SETUP] & F_SETUP_F_MODE_MASK);
            
            if (fifoMode == 0)
            {
                // FIFO disabled - clear it
                fifoCount = 0;
                fifoReadIndex = 0;
                fifoWriteIndex = 0;
            }
            else
            {
                // FIFO enabled - fill with data
                FillFifoWithCurrentData();
            }
        }

        private void UpdateFifoStatus()
        {
            // F_STATUS register:
            // Bit 7: F_OVF - FIFO overflow flag
            // Bit 6: F_WMRK_FLAG - FIFO watermark flag
            // Bits 5-0: F_CNT - FIFO sample count
            
            if (IsFifoEnabled())
            {
                // Report full FIFO (32 samples)
                registers[REG_F_STATUS] = (byte)(0xC0 | Math.Min(fifoCount, 32)); // F_OVF=1, F_WMRK_FLAG=1
            }
            else
            {
                registers[REG_F_STATUS] = 0x00;
            }
        }

        private void UpdateInterruptSource()
        {
            // INT_SOURCE register shows which interrupt triggered
            // Bit 6: FIFO interrupt
            // Bit 5: Temperature change
            // Bit 4: Pressure/altitude change
            
            byte intSource = 0;
            
            if (IsFifoEnabled() && fifoCount >= FIFO_SIZE)
            {
                intSource |= 0x40; // FIFO interrupt
            }
            
            registers[REG_INT_SOURCE] = intSource;
        }

        private byte ReadFifoByte()
        {
            if (!IsFifoEnabled())
            {
                return 0;
            }
            
            // Auto-refill FIFO if empty (for continuous operation)
            if (fifoCount == 0)
            {
                FillFifoWithCurrentData();
            }

            byte value = fifoBuffer[fifoReadIndex];
            
            fifoReadIndex = (fifoReadIndex + 1) % fifoBuffer.Length;
            
            // When we read the last byte of a sample, decrement count
            if (fifoReadIndex % DATA_BATCH_SIZE == 0)
            {
                fifoCount = Math.Max(0, fifoCount - 1);
            }
            
            return value;
        }

        private void FillFifoWithCurrentData()
        {
            // Convert current altitude and temperature to raw format
            byte[] sample = ConvertToRawData(altitudeM, temperatureC);
            
            

            // Fill entire FIFO with the same sample (simulating constant conditions)
            for (int i = 0; i < FIFO_SIZE; i++)
            {
                int offset = i * DATA_BATCH_SIZE;
                Array.Copy(sample, 0, fifoBuffer, offset, DATA_BATCH_SIZE);
            }

            fifoCount = FIFO_SIZE;
            fifoReadIndex = 0;
            fifoWriteIndex = 0;
        }

        /// <summary>
        /// Convert altitude and temperature to raw MPL3115A2 format.
        /// 
        /// Altitude: 20-bit signed Q16.4 format (3 bytes: MSB, CSB, LSB)
        /// The firmware reconstructs: h = (MSB<<24 | CSB<<16 | LSB<<8), then altitude = h / 65536.0
        /// 
        /// Temperature: 12-bit signed Q8.4 format (2 bytes: MSB, LSB)
        /// The firmware reconstructs: t = (MSB<<8 | LSB), then temperature = t / 256.0
        /// </summary>
        private byte[] ConvertToRawData(float altitude, float temperature)
        {
            var result = new byte[5];

            // For altitude, the firmware divides by 65536 (= 2^16).
            // The Q16.4 format has 16 integer bits and 4 fractional bits.
            // So: raw_altitude = altitude * 65536 / 16 = altitude * 4096
            // But we need to distribute across 3 bytes for the firmware's bit reconstruction.
            // 
            // Firmware: h = MSB<<24 | CSB<<16 | LSB<<8
            //           altitude = h / 65536.0
            // 
            // So: h = altitude * 65536
            //     MSB = (h >> 24) & 0xFF - but this is wrong for negative values
            //     
            // Instead, we store as 20-bit Q16.4 in the 3 bytes:
            // - altFixed = altitude * 16 (convert to Q16.4)
            // - Shift left by 8 so that when firmware does LSB<<8, it gets the right value
            // - Actually: firmware does MSB<<24 | CSB<<16 | LSB<<8 = combined << 8
            // - So combined = altitude * 65536 / 256 = altitude * 256
            // - altFixed = altitude * 16 (Q16.4)
            // - To get combined: altFixed * 16 = altitude * 256
            
            int altFixed = (int)Math.Round(altitude * 16.0f); // Q16.4 format
            
            // Clamp to 20-bit signed range (-524288 to 524287)
            if (altFixed > 524287) altFixed = 524287;
            if (altFixed < -524288) altFixed = -524288;

            // Sign-extend to 24 bits and align for the firmware's bit extraction
            // The firmware expects: (MSB<<24 | CSB<<16 | LSB<<8) / 65536
            // We have: altFixed = altitude * 16
            // We need: h = altitude * 65536 = altFixed * 4096
            // So: shift altFixed left by 12 bits
            int altShifted = altFixed << 12;
            
            // For negative values, we need proper sign extension
            // altShifted is a 32-bit value, extract the bytes
            result[0] = (byte)((altShifted >> 24) & 0xFF); // MSB
            result[1] = (byte)((altShifted >> 16) & 0xFF); // CSB  
            result[2] = (byte)((altShifted >> 8) & 0xFF);  // LSB

            // For temperature, the firmware divides by 256 (= 2^8).
            // The Q8.4 format has 8 integer bits and 4 fractional bits.
            // Firmware: t = MSB<<8 | LSB
            //           temperature = t / 256.0
            // So: t = temperature * 256
            // tempFixed = temperature * 16 (Q8.4)
            // We need: t = tempFixed * 16 = temperature * 256
            
            int tempFixed = (int)Math.Round(temperature * 16.0f); // Q8.4 format
            
            // Clamp to 12-bit signed range (-2048 to 2047)
            if (tempFixed > 2047) tempFixed = 2047;
            if (tempFixed < -2048) tempFixed = -2048;

            // Shift left by 4 bits: tempFixed * 16 = temperature * 256
            int tempShifted = tempFixed << 4;
            
            result[3] = (byte)((tempShifted >> 8) & 0xFF); // MSB
            result[4] = (byte)(tempShifted & 0xFF);        // LSB

            return result;
        }

        #endregion

        // Internal fields
        internal byte[] registers;
        private byte[] fifoBuffer;
        private byte currentRegister;
        private bool isFirstWrite;
        private int fifoReadIndex;
        private int fifoWriteIndex;
        private int fifoCount;
        
        // Sensor values
        private float altitudeM;
        private float temperatureC;
    }
}
