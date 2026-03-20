/**
 * lis3dh.cs
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
    /// LIS3DH accelerometer and temperature sensor I2C device simulator.
    /// Default I2C address is 0x18.
    /// 
    /// The device can also be configured at address 0x19 when SA0 pin is high.
    /// </summary>
    public class LIS3DH : II2CPeripheral
    {
        // Register addresses based on LIS3DH datasheet
        private const byte REG_WHO_AM_I = 0x0F;
        
        // Temperature and auxiliary ADC registers
        private const byte REG_OUT_ADC3_L = 0x0C;
        private const byte REG_OUT_ADC3_H = 0x0D;
        
        // Control registers
        private const byte REG_TEMP_CFG_REG = 0x1F;
        private const byte REG_CTRL_REG1 = 0x20;
        private const byte REG_CTRL_REG2 = 0x21;
        private const byte REG_CTRL_REG3 = 0x22;
        private const byte REG_CTRL_REG4 = 0x23;
        private const byte REG_CTRL_REG5 = 0x24;
        private const byte REG_CTRL_REG6 = 0x25;
        
        // Status register
        private const byte REG_STATUS_REG = 0x27;
        
        // Output registers (acceleration data)
        private const byte REG_OUT_X_L = 0x28;
        private const byte REG_OUT_X_H = 0x29;
        private const byte REG_OUT_Y_L = 0x2A;
        private const byte REG_OUT_Y_H = 0x2B;
        private const byte REG_OUT_Z_L = 0x2C;
        private const byte REG_OUT_Z_H = 0x2D;
        
        // FIFO control
        private const byte REG_FIFO_CTRL_REG = 0x2E;
        
        // Default values
        private const byte WHO_AM_I_VALUE = 0x33;
        
        // Sensitivity for normal mode, ±2g range: 4mg/LSB = 0.004g
        // To convert g to raw: raw = g / 0.004 = g * 250
        // But pico-example uses: scaling = 64 / 0.004 = 16000
        private const float AccelScaleFactor = 16000.0f;
        
        // Temperature scale factor: raw = temp_C * 64
        private const float TempScaleFactor = 64.0f;

        public LIS3DH()
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
            registers[REG_CTRL_REG1] = 0x07; // X, Y, Z axes enabled
            registers[REG_CTRL_REG4] = 0x00;
            registers[REG_TEMP_CFG_REG] = 0x00;
            registers[REG_FIFO_CTRL_REG] = 0x00;

            // Reset state
            currentRegister = 0;
            isFirstWrite = true;

            // Default sensor values
            xAccelG = 0.0f;
            yAccelG = 0.0f;
            zAccelG = 1.0f; // 1g on Z axis (device at rest)
            temperatureC = 25.0f;
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
            // Write to register
            registers[currentRegister] = value;
            currentRegister++;
        }

        public byte[] Read(int count = 1)
        {
            var result = new List<byte>();

            for (int i = 0; i < count; i++)
            {
                // Check if we're reading acceleration data
                if (currentRegister >= REG_OUT_X_L && currentRegister <= REG_OUT_Z_H)
                {
                    UpdateAccelRegisters();
                }
                // Check if we're reading temperature data
                else if (currentRegister == REG_OUT_ADC3_L || currentRegister == REG_OUT_ADC3_H)
                {
                    UpdateTempRegisters();
                }

                // Read from current register
                result.Add(registers[currentRegister]);

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
        /// Set the temperature in Celsius.
        /// This is read through the auxiliary ADC (ADC3).
        /// </summary>
        public void SetTemperatureCelsius(float celsius)
        {
            temperatureC = celsius;
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
        /// Get the current temperature in Celsius.
        /// </summary>
        public float GetTemperatureCelsius()
        {
            return temperatureC;
        }

        #endregion

        #region Private Helper Methods

        private void UpdateAccelRegisters()
        {
            // Convert g values to raw 16-bit signed values
            // Using the scaling from pico-examples: scaling = 64 / 0.004 = 16000
            short rawX = (short)(xAccelG * AccelScaleFactor);
            short rawY = (short)(yAccelG * AccelScaleFactor);
            short rawZ = (short)(zAccelG * AccelScaleFactor);

            // Store in registers (little-endian)
            registers[REG_OUT_X_L] = (byte)(rawX & 0xFF);
            registers[REG_OUT_X_H] = (byte)((rawX >> 8) & 0xFF);
            registers[REG_OUT_Y_L] = (byte)(rawY & 0xFF);
            registers[REG_OUT_Y_H] = (byte)((rawY >> 8) & 0xFF);
            registers[REG_OUT_Z_L] = (byte)(rawZ & 0xFF);
            registers[REG_OUT_Z_H] = (byte)((rawZ >> 8) & 0xFF);
        }

        private void UpdateTempRegisters()
        {
            // Convert temperature to raw value
            // Temperature scaling: raw = temp_C * 64
            short rawTemp = (short)(temperatureC * TempScaleFactor);

            // Store in registers (little-endian)
            registers[REG_OUT_ADC3_L] = (byte)(rawTemp & 0xFF);
            registers[REG_OUT_ADC3_H] = (byte)((rawTemp >> 8) & 0xFF);
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
        private float temperatureC;
    }
}
