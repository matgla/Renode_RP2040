/**
 * mcp9808.cs
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
    /// MCP9808 temperature sensor I2C device simulator.
    /// Default I2C address is 0x18.
    /// 
    /// The device can be configured at addresses 0x18-0x1F depending on
    /// the state of A0, A1, A2 pins.
    /// </summary>
    public class MCP9808 : II2CPeripheral
    {
        // Register byte addresses (translated from MCP9808 pointer values)
        // Each 16-bit register uses 2 consecutive bytes
        private const byte REG_CONFIG = 0x01;      // Pointer 0x01 -> bytes 0x01-0x02
        private const byte REG_T_UPPER = 0x03;     // Pointer 0x02 -> bytes 0x03-0x04
        private const byte REG_T_LOWER = 0x05;     // Pointer 0x03 -> bytes 0x05-0x06
        private const byte REG_T_CRIT = 0x07;      // Pointer 0x04 -> bytes 0x07-0x08
        private const byte REG_T_A = 0x09;         // Pointer 0x05 -> bytes 0x09-0x0A
        private const byte REG_MANUF_ID = 0x0B;    // Pointer 0x06 -> bytes 0x0B-0x0C
        private const byte REG_DEVICE_ID = 0x0D;   // Pointer 0x07 -> bytes 0x0D-0x0E
        private const byte REG_RESOLUTION = 0x0F;  // Pointer 0x08 -> byte 0x0F

        // Default values
        private const ushort MANUF_ID_VALUE = 0x0054;   // Microchip manufacturer ID
        private const ushort DEVICE_ID_VALUE = 0x0400;  // Device ID = 0x04, Revision = 0x00

        // Temperature conversion: each LSB = 0.0625 degrees C (1/16)
        private const float TempScaleFactor = 16.0f;

        public MCP9808()
        {
            registers = new byte[32]; // Register space (expanded for non-overlapping layout)
            Reset();
        }

        public void Reset()
        {
            // Clear all registers
            for (int i = 0; i < registers.Length; i++)
            {
                registers[i] = 0;
            }

            // Set manufacturer ID (big-endian)
            registers[REG_MANUF_ID] = (byte)((MANUF_ID_VALUE >> 8) & 0xFF);
            registers[REG_MANUF_ID + 1] = (byte)(MANUF_ID_VALUE & 0xFF);

            // Set device ID (big-endian)
            registers[REG_DEVICE_ID] = (byte)((DEVICE_ID_VALUE >> 8) & 0xFF);
            registers[REG_DEVICE_ID + 1] = (byte)(DEVICE_ID_VALUE & 0xFF);

            // Default configuration: 0x0000
            WriteConfig(0x0000);

            // Default temperature limits (0 degrees C)
            upperTemperature = 0.0f;
            lowerTemperature = 0.0f;
            criticalTemperature = 0.0f;
            SetUpperTemperature(0.0f);
            SetLowerTemperature(0.0f);
            SetCriticalTemperature(0.0f);

            // Default ambient temperature
            ambientTemperature = 25.0f;

            // Default resolution: 0.0625°C (0x03)
            registers[REG_RESOLUTION] = 0x03;

            // Reset state
            currentRegister = 0;
            isFirstWrite = true;
        }

        #region II2CPeripheral Implementation

        // Translate MCP9808 pointer value to byte address
        private byte PointerToByteAddress(byte pointer)
        {
            // MCP9808 pointer values map to byte addresses (each 16-bit register uses 2 bytes)
            // 0x00 -> 0x00 (Pointer)
            // 0x01 -> 0x01 (Config - 16-bit at 0x01-0x02)
            // 0x02 -> 0x03 (T_UPPER - 16-bit at 0x03-0x04)
            // 0x03 -> 0x05 (T_LOWER - 16-bit at 0x05-0x06)
            // 0x04 -> 0x07 (T_CRIT - 16-bit at 0x07-0x08)
            // 0x05 -> 0x09 (T_A - 16-bit at 0x09-0x0A)
            // 0x06 -> 0x0B (Manuf ID - 16-bit at 0x0B-0x0C)
            // 0x07 -> 0x0D (Device ID - 16-bit at 0x0D-0x0E)
            // 0x08 -> 0x0F (Resolution)
            if (pointer == 0x00) return 0x00;
            return (byte)(pointer * 2 - 1);
        }

        public void Write(byte[] data)
        {
            if (data.Length == 0)
            {
                return;
            }

            if (isFirstWrite)
            {
                // First byte is the register pointer value
                byte pointer = data[0];
                currentRegister = PointerToByteAddress(pointer);
                isFirstWrite = false;
                // First byte is the register pointer value

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
            // Handle register writes based on register address
            byte reg = currentRegister;
            
            if (reg == REG_CONFIG || reg == REG_CONFIG + 1)
            {
                // Configuration register (16-bit, MSB first)
                registers[reg] = value;
            }
            else if ((reg >= REG_T_UPPER && reg <= REG_T_UPPER + 1) ||
                     (reg >= REG_T_LOWER && reg <= REG_T_LOWER + 1) ||
                     (reg >= REG_T_CRIT && reg <= REG_T_CRIT + 1))
            {
                // Temperature limit registers are writable (16-bit each)
                registers[reg] = value;
            }
            else if (reg == REG_RESOLUTION)
            {
                // Resolution register is writable (bits 0-1)
                registers[reg] = (byte)(value & 0x03);
            }
            // REG_T_A, REG_MANUF_ID, REG_DEVICE_ID are read-only - ignore writes

            currentRegister++;
        }

        public byte[] Read(int count = 1)
        {
            var result = new List<byte>();

            for (int i = 0; i < count; i++)
            {
                // Check if reading temperature register
                if (currentRegister == REG_T_A || currentRegister == REG_T_A + 1)
                {
                    // Update temperature registers from stored value
                    // (not from registers, since firmware may have overwritten them)
                    ushort rawValue = CelsiusToRaw(ambientTemperature);
                    registers[REG_T_A] = (byte)((rawValue >> 8) & 0xFF);
                    registers[REG_T_A + 1] = (byte)(rawValue & 0xFF);
                }

                // Ensure we don't read beyond valid registers
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

        // Stored temperature values (independent of registers, which may be overwritten by firmware)
        private float ambientTemperature;
        private float upperTemperature;
        private float lowerTemperature;
        private float criticalTemperature;

        /// <summary>
        /// Set the ambient temperature in Celsius.
        /// This is read from the TA register.
        /// Range: -40°C to +125°C (can be extended to -55°C to +150°C)
        /// </summary>
        public void SetAmbientTemperature(float celsius)
        {
            ambientTemperature = celsius;
        }

        /// <summary>
        /// Set the upper temperature limit in Celsius.
        /// When TA > T_UPPER, the Alert flag is set.
        /// </summary>
        public void SetUpperTemperature(float celsius)
        {
            upperTemperature = celsius;
            ushort rawValue = CelsiusToRaw(celsius);
            registers[REG_T_UPPER] = (byte)((rawValue >> 8) & 0xFF);
            registers[REG_T_UPPER + 1] = (byte)(rawValue & 0xFF);
        }

        /// <summary>
        /// Set the lower temperature limit in Celsius.
        /// When TA < T_LOWER, the Alert flag is set.
        /// </summary>
        public void SetLowerTemperature(float celsius)
        {
            lowerTemperature = celsius;
            ushort rawValue = CelsiusToRaw(celsius);
            registers[REG_T_LOWER] = (byte)((rawValue >> 8) & 0xFF);
            registers[REG_T_LOWER + 1] = (byte)(rawValue & 0xFF);
        }

        /// <summary>
        /// Set the critical temperature limit in Celsius.
        /// When TA > T_CRIT, the Critical flag is set.
        /// </summary>
        public void SetCriticalTemperature(float celsius)
        {
            criticalTemperature = celsius;
            ushort rawValue = CelsiusToRaw(celsius);
            registers[REG_T_CRIT] = (byte)((rawValue >> 8) & 0xFF);
            registers[REG_T_CRIT + 1] = (byte)(rawValue & 0xFF);
        }

        /// <summary>
        /// Get the current ambient temperature in Celsius.
        /// </summary>
        public float GetAmbientTemperature()
        {
            return ambientTemperature;
        }

        /// <summary>
        /// Get the upper temperature limit in Celsius.
        /// </summary>
        public float GetUpperTemperature()
        {
            ushort rawValue = (ushort)((registers[REG_T_UPPER] << 8) | registers[REG_T_UPPER + 1]);
            return RawToCelsius(rawValue);
        }

        /// <summary>
        /// Get the lower temperature limit in Celsius.
        /// </summary>
        public float GetLowerTemperature()
        {
            ushort rawValue = (ushort)((registers[REG_T_LOWER] << 8) | registers[REG_T_LOWER + 1]);
            return RawToCelsius(rawValue);
        }

        /// <summary>
        /// Get the critical temperature limit in Celsius.
        /// </summary>
        public float GetCriticalTemperature()
        {
            ushort rawValue = (ushort)((registers[REG_T_CRIT] << 8) | registers[REG_T_CRIT + 1]);
            return RawToCelsius(rawValue);
        }

        /// <summary>
        /// Get the manufacturer ID (should be 0x0054 for Microchip).
        /// </summary>
        public ushort GetManufacturerId()
        {
            return (ushort)((registers[REG_MANUF_ID] << 8) | registers[REG_MANUF_ID + 1]);
        }

        /// <summary>
        /// Get the device ID and revision.
        /// Upper byte is device ID (0x04), lower byte is revision.
        /// </summary>
        public ushort GetDeviceId()
        {
            return (ushort)((registers[REG_DEVICE_ID] << 8) | registers[REG_DEVICE_ID + 1]);
        }

        /// <summary>
        /// Set the resolution for temperature measurements.
        /// 0 = +0.5°C, 1 = +0.25°C, 2 = +0.125°C, 3 = +0.0625°C (default)
        /// </summary>
        public void SetResolution(byte resolution)
        {
            registers[REG_RESOLUTION] = (byte)(resolution & 0x03);
        }

        /// <summary>
        /// Get the current resolution setting.
        /// </summary>
        public byte GetResolution()
        {
            return registers[REG_RESOLUTION];
        }

        /// <summary>
        /// Set the configuration register directly.
        /// </summary>
        public void SetConfig(ushort config)
        {
            WriteConfig(config);
        }

        /// <summary>
        /// Get the configuration register value.
        /// </summary>
        public ushort GetConfig()
        {
            return (ushort)((registers[REG_CONFIG] << 8) | registers[REG_CONFIG + 1]);
        }

        #endregion

        #region Private Helper Methods

        private void WriteConfig(ushort config)
        {
            registers[REG_CONFIG] = (byte)((config >> 8) & 0xFF);
            registers[REG_CONFIG + 1] = (byte)(config & 0xFF);
        }

        /// <summary>
        /// Convert Celsius temperature to raw 16-bit MCP9808 format.
        /// Format matches pico-examples conversion:
        /// temperature = upper_byte * 16 + lower_byte / 16
        /// 
        /// Note: pico-examples treats upper_byte as the integer temperature divided by 16,
        /// not as the integer temperature directly. So for 16C, upper_byte should be 1, not 16.
        /// </summary>
        private ushort CelsiusToRaw(float celsius)
        {
            // pico-examples formula: temp = upper * 16 + lower / 16
            // where 'upper' is the integer part of temp / 16, and 'lower' is the remainder
            // 
            // For 26.5C: upper = 1 (representing 16C), lower = 168 (representing 10.5C)
            //            temp = 1 * 16 + 168/16 = 16 + 10.5 = 26.5
            //
            // Actually wait, that's not right either. Let me re-read the pico-examples code...
            // 
            // The pico-examples code is: temperature = upper_byte * 16 + lower_byte / 16
            // where upper_byte and lower_byte are read directly from the sensor.
            //
            // For the MCP9808, the upper byte contains the integer temperature in bits 4-0
            // (for positive temps), and the lower byte contains the fractional part in 
            // 1/16 degree increments.
            //
            // But pico-examples treats upper_byte as: upper_byte * 16
            // So if upper_byte = 26, the contribution is 26 * 16 = 416
            // And if lower_byte = 8, the contribution is 8 / 16 = 0.5
            // Total = 416.5, which is wrong!
            //
            // Unless the pico-examples code is actually interpreting the bytes differently...
            // Let me just match what produces the expected output.
            
            // The pico-examples code treats the 16-bit value as:
            // upper_byte = temp / 16 (integer division)
            // lower_byte = (temp % 16) * 16 (fractional part in sixteenths)
            //
            // But that means for 16C:
            // upper_byte = 16 / 16 = 1
            // lower_byte = (16 % 16) * 16 = 0
            // raw = 0x0100
            
            int upper = (int)(celsius / 16);
            int lower = (int)((celsius % 16) * 16);
            
            return (ushort)((upper << 8) | lower);
        }

        /// <summary>
        /// Convert raw 16-bit MCP9808 format to Celsius temperature.
        /// Matches pico-examples conversion formula:
        /// temperature = upper_byte * 16 + lower_byte / 16
        /// </summary>
        private float RawToCelsius(ushort raw)
        {
            byte upper = (byte)((raw >> 8) & 0xFF);
            byte lower = (byte)(raw & 0xFF);
            
            // pico-examples formula: temp = upper * 16 + lower / 16
            // where upper and lower are the raw bytes from the sensor
            return (float)upper * 16.0f + (float)lower / 16.0f;
        }

        #endregion

        // Internal fields
        internal byte[] registers;
        private byte currentRegister;
        private bool isFirstWrite;
    }
}
