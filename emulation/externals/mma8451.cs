/**
 * mma8451.cs
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
    /// MMA8451 triple-axis accelerometer I2C device simulator.
    /// Default I2C address is 0x1D (can be changed to 0x1C with SA0 pin low).
    /// 
    /// This 14-bit accelerometer provides acceleration data in X, Y, Z axes.
    /// Supports ±2g, ±4g, ±8g ranges with corresponding counts of 4096, 2048, 1024.
    /// </summary>
    public class MMA8451 : II2CPeripheral
    {
        // Register addresses based on MMA8451 datasheet
        private const byte REG_STATUS = 0x00;       // Status register
        private const byte REG_OUT_X_MSB = 0x01;    // X-axis MSB (bits 13-6)
        private const byte REG_OUT_X_LSB = 0x02;    // X-axis LSB (bits 5-0) + 2 unused bits
        private const byte REG_OUT_Y_MSB = 0x03;    // Y-axis MSB
        private const byte REG_OUT_Y_LSB = 0x04;    // Y-axis LSB
        private const byte REG_OUT_Z_MSB = 0x05;    // Z-axis MSB
        private const byte REG_OUT_Z_LSB = 0x06;    // Z-axis LSB
        private const byte REG_SYSMOD = 0x0B;       // System mode
        private const byte REG_INT_SOURCE = 0x0C;   // Interrupt source
        private const byte REG_WHO_AM_I = 0x0D;     // Device ID
        private const byte REG_XYZ_DATA_CFG = 0x0E; // Data configuration (range)
        private const byte REG_CTRL_REG1 = 0x2A;    // Control register 1
        private const byte REG_CTRL_REG2 = 0x2B;    // Control register 2
        private const byte REG_CTRL_REG3 = 0x2C;    // Control register 3
        private const byte REG_CTRL_REG4 = 0x2D;    // Control register 4
        private const byte REG_CTRL_REG5 = 0x2E;    // Control register 5

        // Default values
        private const byte WHO_AM_I_VALUE = 0x1A;   // MMA8451 device ID
        
        // Scale factors for different ranges (LSB per g)
        // ±2g: 4096 counts/g, ±4g: 2048 counts/g, ±8g: 1024 counts/g
        private const float ScaleFactor2G = 4096.0f;
        private const float ScaleFactor4G = 2048.0f;
        private const float ScaleFactor8G = 1024.0f;

        public MMA8451()
        {
            registers = new byte[256]; // Full register space
            Reset();
        }

        public void Reset()
        {
            // Clear all registers
            for (int i = 0; i < registers.Length; i++)
            {
                registers[i] = 0;
            }

            // Set WHO_AM_I register
            registers[REG_WHO_AM_I] = WHO_AM_I_VALUE;

            // Default control register values
            // CTRL_REG1: 0x00 (standby mode, ODR = 800Hz)
            registers[REG_CTRL_REG1] = 0x00;
            
            // CTRL_REG2: 0x00 (normal mode)
            registers[REG_CTRL_REG2] = 0x00;
            
            // CTRL_REG3: 0x00 (push-pull, active low)
            registers[REG_CTRL_REG3] = 0x00;
            
            // CTRL_REG4: 0x00 (no interrupts enabled)
            registers[REG_CTRL_REG4] = 0x00;
            
            // CTRL_REG5: 0x00 (interrupts routed to INT2 pin)
            registers[REG_CTRL_REG5] = 0x00;
            
            // XYZ_DATA_CFG: 0x00 (±2g range)
            registers[REG_XYZ_DATA_CFG] = 0x00;

            // SYSMOD: 0x00 (standby mode)
            registers[REG_SYSMOD] = 0x00;

            // Status: 0x00 (no new data)
            registers[REG_STATUS] = 0x00;

            // Reset state
            currentRegister = 0;
            isFirstWrite = true;

            // Default acceleration values (device at rest on flat surface)
            // Z-axis shows 1g, X and Y show 0g
            xAccelG = 0.0f;
            yAccelG = 0.0f;
            zAccelG = 1.0f;
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
                // CTRL_REG1 controls active/standby mode
                registers[REG_CTRL_REG1] = value;
                UpdateSysmod();
            }
            else if (currentRegister == REG_XYZ_DATA_CFG)
            {
                // XYZ_DATA_CFG: only bits 0-1 are valid (range selection)
                registers[REG_XYZ_DATA_CFG] = (byte)(value & 0x03);
            }
            else if (currentRegister >= REG_OUT_X_MSB && currentRegister <= REG_OUT_Z_LSB)
            {
                // Output data registers are read-only, ignore writes
            }
            else if (currentRegister == REG_WHO_AM_I)
            {
                // WHO_AM_I is read-only, ignore writes
            }
            else if (currentRegister == REG_STATUS)
            {
                // Status is read-only, ignore writes
            }
            else if (currentRegister == REG_SYSMOD)
            {
                // SYSMOD is read-only, ignore writes
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
                // Check if we're reading acceleration data
                if (currentRegister >= REG_OUT_X_MSB && currentRegister <= REG_OUT_Z_LSB)
                {
                    UpdateAccelRegisters();
                }
                // Check if we're reading status
                else if (currentRegister == REG_STATUS)
                {
                    UpdateStatusRegister();
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
        /// Set the X-axis acceleration in g (gravitational units).
        /// Positive values indicate acceleration in the positive X direction.
        /// </summary>
        public void SetAccelX(float g)
        {
            xAccelG = g;
        }

        /// <summary>
        /// Set the Y-axis acceleration in g (gravitational units).
        /// </summary>
        public void SetAccelY(float g)
        {
            yAccelG = g;
        }

        /// <summary>
        /// Set the Z-axis acceleration in g (gravitational units).
        /// At rest on a flat surface, Z should be approximately 1.0g.
        /// </summary>
        public void SetAccelZ(float g)
        {
            zAccelG = g;
        }

        /// <summary>
        /// Set all three acceleration axes at once.
        /// </summary>
        public void SetAcceleration(float x, float y, float z)
        {
            xAccelG = x;
            yAccelG = y;
            zAccelG = z;
        }

        /// <summary>
        /// Get the current X-axis acceleration value.
        /// </summary>
        public float GetAccelX()
        {
            return xAccelG;
        }

        /// <summary>
        /// Get the current Y-axis acceleration value.
        /// </summary>
        public float GetAccelY()
        {
            return yAccelG;
        }

        /// <summary>
        /// Get the current Z-axis acceleration value.
        /// </summary>
        public float GetAccelZ()
        {
            return zAccelG;
        }

        /// <summary>
        /// Set the acceleration range.
        /// 0 = ±2g (4096 counts/g), 1 = ±4g (2048 counts/g), 2 = ±8g (1024 counts/g)
        /// </summary>
        public void SetRange(byte range)
        {
            registers[REG_XYZ_DATA_CFG] = (byte)(range & 0x03);
        }

        /// <summary>
        /// Get the current acceleration range.
        /// </summary>
        public byte GetRange()
        {
            return (byte)(registers[REG_XYZ_DATA_CFG] & 0x03);
        }

        /// <summary>
        /// Set the active mode (true = active, false = standby).
        /// </summary>
        public void SetActive(bool active)
        {
            if (active)
            {
                registers[REG_CTRL_REG1] |= 0x01; // Set active bit
            }
            else
            {
                registers[REG_CTRL_REG1] &= 0xFE; // Clear active bit
            }
            UpdateSysmod();
        }

        /// <summary>
        /// Check if device is in active mode.
        /// </summary>
        public bool IsActive()
        {
            return (registers[REG_CTRL_REG1] & 0x01) != 0;
        }

        /// <summary>
        /// Get the device ID (should be 0x1A for MMA8451).
        /// </summary>
        public byte GetDeviceId()
        {
            return WHO_AM_I_VALUE;
        }

        #endregion

        #region Private Helper Methods

        private void UpdateSysmod()
        {
            // SYSMOD reflects the current mode
            // Bit 0: SYSMOD0 - 0=STANDBY, 1=WAKE (active)
            if ((registers[REG_CTRL_REG1] & 0x01) != 0)
            {
                registers[REG_SYSMOD] = 0x01; // Wake mode
            }
            else
            {
                registers[REG_SYSMOD] = 0x00; // Standby mode
            }
        }

        private void UpdateStatusRegister()
        {
            // Status register indicates data ready for each axis
            // Bit 3: ZYXDR - data ready for all axes
            // Bit 2: ZDR - Z data ready
            // Bit 1: YDR - Y data ready
            // Bit 0: XDR - X data ready
            // In active mode, we always have data ready
            if (IsActive())
            {
                registers[REG_STATUS] = 0x0F; // All data ready
            }
            else
            {
                registers[REG_STATUS] = 0x00; // No data in standby
            }
        }

        private void UpdateAccelRegisters()
        {
            // Get current scale factor based on range setting
            float scaleFactor = GetScaleFactor();

            // Convert g values to raw 14-bit signed values
            // Raw = g * scaleFactor
            // Then left-shift by 2 to align with 14-bit format (bits 15-2)
            short rawX = GToRaw14Bit(xAccelG, scaleFactor);
            short rawY = GToRaw14Bit(yAccelG, scaleFactor);
            short rawZ = GToRaw14Bit(zAccelG, scaleFactor);

            // Store in registers
            // MSB contains bits 13-6 (upper 8 bits of 14-bit value)
            // LSB contains bits 5-0 in upper 6 bits, lower 2 bits unused
            registers[REG_OUT_X_MSB] = (byte)((rawX >> 8) & 0xFF);
            registers[REG_OUT_X_LSB] = (byte)(rawX & 0xFF);
            registers[REG_OUT_Y_MSB] = (byte)((rawY >> 8) & 0xFF);
            registers[REG_OUT_Y_LSB] = (byte)(rawY & 0xFF);
            registers[REG_OUT_Z_MSB] = (byte)((rawZ >> 8) & 0xFF);
            registers[REG_OUT_Z_LSB] = (byte)(rawZ & 0xFF);
        }

        private float GetScaleFactor()
        {
            byte range = (byte)(registers[REG_XYZ_DATA_CFG] & 0x03);
            switch (range)
            {
                case 0: return ScaleFactor2G;
                case 1: return ScaleFactor4G;
                case 2: return ScaleFactor8G;
                default: return ScaleFactor2G;
            }
        }

        /// <summary>
        /// Convert g value to raw 14-bit format.
        /// The raw value is left-aligned to 16 bits (14-bit data in upper bits).
        /// </summary>
        private short GToRaw14Bit(float g, float scaleFactor)
        {
            // Convert g to counts
            float counts = g * scaleFactor;
            
            // Clamp to valid range for 14-bit signed values
            // 14-bit signed: -8192 to +8191
            if (counts > 8191) counts = 8191;
            if (counts < -8192) counts = -8192;
            
            // Convert to short and left-shift by 2 to align with 14-bit format
            // The pico-example reads: (MSB << 6) | (LSB >> 2)
            // This extracts the 14-bit value from the 16-bit register pair
            short raw = (short)counts;
            return (short)(raw << 2);
        }

        #endregion

        // Internal fields
        internal byte[] registers;
        private byte currentRegister;
        private bool isFirstWrite;
        
        // Sensor values
        private float xAccelG;
        private float yAccelG;
        private float zAccelG;
    }
}
