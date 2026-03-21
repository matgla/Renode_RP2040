/**
 * mpu6050.cs
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
    /// MPU6050 6-axis motion tracking device (accelerometer + gyroscope) I2C simulator.
    /// Default I2C address is 0x68 (can be changed to 0x69 with AD0 pin high).
    /// 
    /// Provides 16-bit accelerometer, gyroscope, and temperature data.
    /// </summary>
    public class MPU6050 : II2CPeripheral
    {
        // Register addresses
        private const byte REG_ACCEL_XOUT_H = 0x3B;  // Accelerometer X high byte
        private const byte REG_ACCEL_XOUT_L = 0x3C;  // Accelerometer X low byte
        private const byte REG_ACCEL_YOUT_H = 0x3D;  // Accelerometer Y high byte
        private const byte REG_ACCEL_YOUT_L = 0x3E;  // Accelerometer Y low byte
        private const byte REG_ACCEL_ZOUT_H = 0x3F;  // Accelerometer Z high byte
        private const byte REG_ACCEL_ZOUT_L = 0x40;  // Accelerometer Z low byte
        private const byte REG_TEMP_OUT_H = 0x41;    // Temperature high byte
        private const byte REG_TEMP_OUT_L = 0x42;    // Temperature low byte
        private const byte REG_GYRO_XOUT_H = 0x43;   // Gyroscope X high byte
        private const byte REG_GYRO_XOUT_L = 0x44;   // Gyroscope X low byte
        private const byte REG_GYRO_YOUT_H = 0x45;   // Gyroscope Y high byte
        private const byte REG_GYRO_YOUT_L = 0x46;   // Gyroscope Y low byte
        private const byte REG_GYRO_ZOUT_H = 0x47;   // Gyroscope Z high byte
        private const byte REG_GYRO_ZOUT_L = 0x48;   // Gyroscope Z low byte
        private const byte REG_PWR_MGMT_1 = 0x6B;    // Power management 1
        private const byte REG_WHO_AM_I = 0x75;      // Device ID

        // Default values
        private const byte WHO_AM_I_VALUE = 0x68;    // MPU6050 device ID

        // Temperature conversion constants (from datasheet)
        // Temp in degrees C = (raw / 340.0) + 36.53
        private const float TempScaleFactor = 340.0f;
        private const float TempOffset = 36.53f;

        public MPU6050()
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

            // Power management: device starts in sleep mode (bit 6 = 1)
            registers[REG_PWR_MGMT_1] = 0x40;

            // Reset state
            currentRegister = 0;
            isFirstWrite = true;

            // Default sensor values
            accelX = 0;
            accelY = 0;
            accelZ = 0;
            gyroX = 0;
            gyroY = 0;
            gyroZ = 0;
            temperatureC = 25.0f; // Room temperature

            // Update registers with default values
            UpdateAccelRegisters();
            UpdateGyroRegisters();
            UpdateTempRegisters();
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
            if (currentRegister == REG_PWR_MGMT_1)
            {
                // Check for device reset (bit 7 = 1)
                if ((value & 0x80) != 0)
                {
                    Reset();
                    return;
                }
                registers[REG_PWR_MGMT_1] = value;
            }
            else if (currentRegister >= REG_ACCEL_XOUT_H && currentRegister <= REG_GYRO_ZOUT_L)
            {
                // Output registers are read-only, ignore writes
            }
            else if (currentRegister == REG_WHO_AM_I)
            {
                // WHO_AM_I is read-only, ignore writes
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
                // Update output registers before reading
                if (currentRegister >= REG_ACCEL_XOUT_H && currentRegister <= REG_ACCEL_ZOUT_L)
                {
                    UpdateAccelRegisters();
                }
                else if (currentRegister >= REG_GYRO_XOUT_H && currentRegister <= REG_GYRO_ZOUT_L)
                {
                    UpdateGyroRegisters();
                }
                else if (currentRegister == REG_TEMP_OUT_H || currentRegister == REG_TEMP_OUT_L)
                {
                    UpdateTempRegisters();
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
        /// Set the X-axis accelerometer raw value (16-bit signed).
        /// </summary>
        public void SetAccelX(short value)
        {
            accelX = value;
        }

        /// <summary>
        /// Set the Y-axis accelerometer raw value (16-bit signed).
        /// </summary>
        public void SetAccelY(short value)
        {
            accelY = value;
        }

        /// <summary>
        /// Set the Z-axis accelerometer raw value (16-bit signed).
        /// </summary>
        public void SetAccelZ(short value)
        {
            accelZ = value;
        }

        /// <summary>
        /// Set all three accelerometer axes at once.
        /// </summary>
        public void SetAcceleration(short x, short y, short z)
        {
            accelX = x;
            accelY = y;
            accelZ = z;
        }

        /// <summary>
        /// Set the X-axis gyroscope raw value (16-bit signed).
        /// </summary>
        public void SetGyroX(short value)
        {
            gyroX = value;
        }

        /// <summary>
        /// Set the Y-axis gyroscope raw value (16-bit signed).
        /// </summary>
        public void SetGyroY(short value)
        {
            gyroY = value;
        }

        /// <summary>
        /// Set the Z-axis gyroscope raw value (16-bit signed).
        /// </summary>
        public void SetGyroZ(short value)
        {
            gyroZ = value;
        }

        /// <summary>
        /// Set all three gyroscope axes at once.
        /// </summary>
        public void SetGyro(short x, short y, short z)
        {
            gyroX = x;
            gyroY = y;
            gyroZ = z;
        }

        /// <summary>
        /// Set the temperature in Celsius.
        /// Formula: raw = (temp - 36.53) * 340
        /// </summary>
        public void SetTemperature(float celsius)
        {
            temperatureC = celsius;
        }

        /// <summary>
        /// Get the current X-axis accelerometer value.
        /// </summary>
        public short GetAccelX()
        {
            return accelX;
        }

        /// <summary>
        /// Get the current Y-axis accelerometer value.
        /// </summary>
        public short GetAccelY()
        {
            return accelY;
        }

        /// <summary>
        /// Get the current Z-axis accelerometer value.
        /// </summary>
        public short GetAccelZ()
        {
            return accelZ;
        }

        /// <summary>
        /// Get the current X-axis gyroscope value.
        /// </summary>
        public short GetGyroX()
        {
            return gyroX;
        }

        /// <summary>
        /// Get the current Y-axis gyroscope value.
        /// </summary>
        public short GetGyroY()
        {
            return gyroY;
        }

        /// <summary>
        /// Get the current Z-axis gyroscope value.
        /// </summary>
        public short GetGyroZ()
        {
            return gyroZ;
        }

        /// <summary>
        /// Get the current temperature in Celsius.
        /// </summary>
        public float GetTemperature()
        {
            return temperatureC;
        }

        /// <summary>
        /// Get the device ID (should be 0x68 for MPU6050).
        /// </summary>
        public byte GetDeviceId()
        {
            return WHO_AM_I_VALUE;
        }

        /// <summary>
        /// Check if device is in sleep mode.
        /// </summary>
        public bool IsSleeping()
        {
            return (registers[REG_PWR_MGMT_1] & 0x40) != 0;
        }

        /// <summary>
        /// Wake up the device (clear sleep bit).
        /// </summary>
        public void WakeUp()
        {
            registers[REG_PWR_MGMT_1] &= unchecked((byte)(~0x40));
        }

        #endregion

        #region Private Helper Methods

        private void UpdateAccelRegisters()
        {
            // Store accelerometer values in registers (big-endian)
            registers[REG_ACCEL_XOUT_H] = (byte)((accelX >> 8) & 0xFF);
            registers[REG_ACCEL_XOUT_L] = (byte)(accelX & 0xFF);
            registers[REG_ACCEL_YOUT_H] = (byte)((accelY >> 8) & 0xFF);
            registers[REG_ACCEL_YOUT_L] = (byte)(accelY & 0xFF);
            registers[REG_ACCEL_ZOUT_H] = (byte)((accelZ >> 8) & 0xFF);
            registers[REG_ACCEL_ZOUT_L] = (byte)(accelZ & 0xFF);
        }

        private void UpdateGyroRegisters()
        {
            // Store gyroscope values in registers (big-endian)
            registers[REG_GYRO_XOUT_H] = (byte)((gyroX >> 8) & 0xFF);
            registers[REG_GYRO_XOUT_L] = (byte)(gyroX & 0xFF);
            registers[REG_GYRO_YOUT_H] = (byte)((gyroY >> 8) & 0xFF);
            registers[REG_GYRO_YOUT_L] = (byte)(gyroY & 0xFF);
            registers[REG_GYRO_ZOUT_H] = (byte)((gyroZ >> 8) & 0xFF);
            registers[REG_GYRO_ZOUT_L] = (byte)(gyroZ & 0xFF);
        }

        private void UpdateTempRegisters()
        {
            // Convert temperature to raw value
            // Formula from datasheet: Temp in C = (raw / 340.0) + 36.53
            // So: raw = (temp - 36.53) * 340
            short tempRaw = (short)((temperatureC - TempOffset) * TempScaleFactor);
            
            // Store temperature value in registers (big-endian)
            registers[REG_TEMP_OUT_H] = (byte)((tempRaw >> 8) & 0xFF);
            registers[REG_TEMP_OUT_L] = (byte)(tempRaw & 0xFF);
        }

        #endregion

        // Internal fields
        internal byte[] registers;
        private byte currentRegister;
        private bool isFirstWrite;
        
        // Sensor values (raw 16-bit signed)
        private short accelX;
        private short accelY;
        private short accelZ;
        private short gyroX;
        private short gyroY;
        private short gyroZ;
        private float temperatureC;
    }
}
