/**
 * bmp280.cs
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
    /// BMP280 temperature and pressure sensor I2C device simulator.
    /// Default I2C address is 0x76.
    /// </summary>
    public class BMP280 : II2CPeripheral
    {
        // Register addresses
        private const byte REG_DIG_T1_LSB = 0x88;
        private const byte REG_DIG_T1_MSB = 0x89;
        private const byte REG_DIG_T2_LSB = 0x8A;
        private const byte REG_DIG_T2_MSB = 0x8B;
        private const byte REG_DIG_T3_LSB = 0x8C;
        private const byte REG_DIG_T3_MSB = 0x8D;
        private const byte REG_DIG_P1_LSB = 0x8E;
        private const byte REG_DIG_P1_MSB = 0x8F;
        private const byte REG_DIG_P2_LSB = 0x90;
        private const byte REG_DIG_P2_MSB = 0x91;
        private const byte REG_DIG_P3_LSB = 0x92;
        private const byte REG_DIG_P3_MSB = 0x93;
        private const byte REG_DIG_P4_LSB = 0x94;
        private const byte REG_DIG_P4_MSB = 0x95;
        private const byte REG_DIG_P5_LSB = 0x96;
        private const byte REG_DIG_P5_MSB = 0x97;
        private const byte REG_DIG_P6_LSB = 0x98;
        private const byte REG_DIG_P6_MSB = 0x99;
        private const byte REG_DIG_P7_LSB = 0x9A;
        private const byte REG_DIG_P7_MSB = 0x9B;
        private const byte REG_DIG_P8_LSB = 0x9C;
        private const byte REG_DIG_P8_MSB = 0x9D;
        private const byte REG_DIG_P9_LSB = 0x9E;
        private const byte REG_DIG_P9_MSB = 0x9F;

        private const byte REG_ID = 0xD0;
        private const byte REG_RESET = 0xE0;
        private const byte REG_STATUS = 0xF3;
        private const byte REG_CTRL_MEAS = 0xF4;
        private const byte REG_CONFIG = 0xF5;
        private const byte REG_PRESSURE_MSB = 0xF7;
        private const byte REG_PRESSURE_LSB = 0xF8;
        private const byte REG_PRESSURE_XLSB = 0xF9;
        private const byte REG_TEMP_MSB = 0xFA;
        private const byte REG_TEMP_LSB = 0xFB;
        private const byte REG_TEMP_XLSB = 0xFC;

        // Calibration parameters storage
        internal struct CalibParams
        {
            public ushort dig_t1;
            public short dig_t2;
            public short dig_t3;
            public ushort dig_p1;
            public short dig_p2;
            public short dig_p3;
            public short dig_p4;
            public short dig_p5;
            public short dig_p6;
            public short dig_p7;
            public short dig_p8;
            public short dig_p9;
        }

        public BMP280()
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

            // Chip ID register - BMP280 ID is 0x58
            registers[REG_ID] = 0x58;

            // Initialize with default calibration parameters (example values)
            // These are typical values that would be programmed during manufacturing
            calibParams = new CalibParams
            {
                dig_t1 = 27504,
                dig_t2 = 26435,
                dig_t3 = -1000,
                dig_p1 = 36477,
                dig_p2 = -10635,
                dig_p3 = 3024,
                dig_p4 = 2855,
                dig_p5 = 140,
                dig_p6 = -7,
                dig_p7 = 15500,
                dig_p8 = -14600,
                dig_p9 = 6000
            };

            // Write calibration parameters to registers
            WriteCalibrationParams();

            // Default register values
            registers[REG_STATUS] = 0x00;
            registers[REG_CTRL_MEAS] = 0x00;
            registers[REG_CONFIG] = 0x00;

            configuredTemperatureCelsius = null;
            configuredPressureHpa = null;

            SetTemperatureCelsius(25.0);
            SetPressureHpa(1013.25);

            currentRegister = 0;
            isFirstWrite = true;
        }

        private void WriteCalibrationParams()
        {
            // Temperature calibration params
            registers[REG_DIG_T1_LSB] = (byte)(calibParams.dig_t1 & 0xFF);
            registers[REG_DIG_T1_MSB] = (byte)((calibParams.dig_t1 >> 8) & 0xFF);
            registers[REG_DIG_T2_LSB] = (byte)(calibParams.dig_t2 & 0xFF);
            registers[REG_DIG_T2_MSB] = (byte)((calibParams.dig_t2 >> 8) & 0xFF);
            registers[REG_DIG_T3_LSB] = (byte)(calibParams.dig_t3 & 0xFF);
            registers[REG_DIG_T3_MSB] = (byte)((calibParams.dig_t3 >> 8) & 0xFF);

            // Pressure calibration params
            registers[REG_DIG_P1_LSB] = (byte)(calibParams.dig_p1 & 0xFF);
            registers[REG_DIG_P1_MSB] = (byte)((calibParams.dig_p1 >> 8) & 0xFF);
            registers[REG_DIG_P2_LSB] = (byte)(calibParams.dig_p2 & 0xFF);
            registers[REG_DIG_P2_MSB] = (byte)((calibParams.dig_p2 >> 8) & 0xFF);
            registers[REG_DIG_P3_LSB] = (byte)(calibParams.dig_p3 & 0xFF);
            registers[REG_DIG_P3_MSB] = (byte)((calibParams.dig_p3 >> 8) & 0xFF);
            registers[REG_DIG_P4_LSB] = (byte)(calibParams.dig_p4 & 0xFF);
            registers[REG_DIG_P4_MSB] = (byte)((calibParams.dig_p4 >> 8) & 0xFF);
            registers[REG_DIG_P5_LSB] = (byte)(calibParams.dig_p5 & 0xFF);
            registers[REG_DIG_P5_MSB] = (byte)((calibParams.dig_p5 >> 8) & 0xFF);
            registers[REG_DIG_P6_LSB] = (byte)(calibParams.dig_p6 & 0xFF);
            registers[REG_DIG_P6_MSB] = (byte)((calibParams.dig_p6 >> 8) & 0xFF);
            registers[REG_DIG_P7_LSB] = (byte)(calibParams.dig_p7 & 0xFF);
            registers[REG_DIG_P7_MSB] = (byte)((calibParams.dig_p7 >> 8) & 0xFF);
            registers[REG_DIG_P8_LSB] = (byte)(calibParams.dig_p8 & 0xFF);
            registers[REG_DIG_P8_MSB] = (byte)((calibParams.dig_p8 >> 8) & 0xFF);
            registers[REG_DIG_P9_LSB] = (byte)(calibParams.dig_p9 & 0xFF);
            registers[REG_DIG_P9_MSB] = (byte)((calibParams.dig_p9 >> 8) & 0xFF);
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
            if (currentRegister == REG_RESET && value == 0xB6)
            {
                // Reset command
                Reset();
                return;
            }

            // Write to register if writable
            if (currentRegister <= 0xF3 || (currentRegister >= 0xF4 && currentRegister <= 0xF7))
            {
                registers[currentRegister] = value;
            }

            currentRegister++;
        }

        public byte[] Read(int count = 1)
        {
            var result = new List<byte>();

            for (int i = 0; i < count; i++)
            {
                // Read from current register
                result.Add(registers[currentRegister]);

                // Auto-increment register address (wrap around at 0xFF)
                currentRegister++;
            }

            return result.ToArray();
        }

        public void FinishTransmission()
        {
            // Reset state for next transaction
            isFirstWrite = true;
        }

        /// <summary>
        /// Set the raw temperature ADC value (20-bit value stored in 3 bytes)
        /// </summary>
        public void SetRawTemperature(int rawTemp)
        {
            // Raw temperature is 20-bit, stored in bits [19:4] of the 3-byte value
            // The raw value format: MSB[7:0], LSB[7:0], XLSB[7:4] contain bits [19:12], [11:4], [3:0]
            uint shifted = (uint)(rawTemp << 4);
            registers[REG_TEMP_MSB] = (byte)((shifted >> 16) & 0xFF);
            registers[REG_TEMP_LSB] = (byte)((shifted >> 8) & 0xFF);
            registers[REG_TEMP_XLSB] = (byte)(shifted & 0xF0);
        }

        /// <summary>
        /// Set the raw pressure ADC value (20-bit value stored in 3 bytes)
        /// </summary>
        public void SetRawPressure(uint rawPressure)
        {
            // Raw pressure is 20-bit, stored in bits [19:4] of the 3-byte value
            uint shifted = (rawPressure << 4);
            registers[REG_PRESSURE_MSB] = (byte)((shifted >> 16) & 0xFF);
            registers[REG_PRESSURE_LSB] = (byte)((shifted >> 8) & 0xFF);
            registers[REG_PRESSURE_XLSB] = (byte)(shifted & 0xF0);
        }

        /// <summary>
        /// Get the current raw temperature value
        /// </summary>
        public int GetRawTemperature()
        {
            return ((int)registers[REG_TEMP_MSB] << 12) |
                   ((int)registers[REG_TEMP_LSB] << 4) |
                   ((int)registers[REG_TEMP_XLSB] >> 4);
        }

        /// <summary>
        /// Get the current raw pressure value
        /// </summary>
        public int GetRawPressure()
        {
            return ((int)registers[REG_PRESSURE_MSB] << 12) |
                   ((int)registers[REG_PRESSURE_LSB] << 4) |
                   ((int)registers[REG_PRESSURE_XLSB] >> 4);
        }

        /// <summary>
        /// Get calibration parameter T1
        /// </summary>
        public ushort GetCalibT1() { return calibParams.dig_t1; }

        /// <summary>
        /// Get calibration parameter T2
        /// </summary>
        public short GetCalibT2() { return calibParams.dig_t2; }

        /// <summary>
        /// Get calibration parameter T3
        /// </summary>
        public short GetCalibT3() { return calibParams.dig_t3; }

        /// <summary>
        /// Get calibration parameter P1
        /// </summary>
        public ushort GetCalibP1() { return calibParams.dig_p1; }

        /// <summary>
        /// Get calibration parameter P2
        /// </summary>
        public short GetCalibP2() { return calibParams.dig_p2; }

        /// <summary>
        /// Get calibration parameter P3
        /// </summary>
        public short GetCalibP3() { return calibParams.dig_p3; }

        /// <summary>
        /// Get calibration parameter P4
        /// </summary>
        public short GetCalibP4() { return calibParams.dig_p4; }

        /// <summary>
        /// Get calibration parameter P5
        /// </summary>
        public short GetCalibP5() { return calibParams.dig_p5; }

        /// <summary>
        /// Get calibration parameter P6
        /// </summary>
        public short GetCalibP6() { return calibParams.dig_p6; }

        /// <summary>
        /// Get calibration parameter P7
        /// </summary>
        public short GetCalibP7() { return calibParams.dig_p7; }

        /// <summary>
        /// Get calibration parameter P8
        /// </summary>
        public short GetCalibP8() { return calibParams.dig_p8; }

        /// <summary>
        /// Get calibration parameter P9
        /// </summary>
        public short GetCalibP9() { return calibParams.dig_p9; }

        /// <summary>
        /// Set temperature in Celsius (converts to raw ADC value)
        /// Uses pre-calculated lookup values for the default calibration parameters
        /// </summary>
        public void SetTemperatureCelsius(double celsius)
        {
            configuredTemperatureCelsius = celsius;

            int targetTemperature = (int)Math.Round(celsius * 100.0);
            int raw = FindClosestRawValue(CompensateTemperatureCentiCelsius, targetTemperature);
            SetRawTemperature(raw);

            if (configuredPressureHpa.HasValue)
            {
                ApplyConfiguredPressure();
            }
        }

        /// <summary>
        /// Set pressure in hPa (converts to raw ADC value)
        /// Uses pre-calculated lookup values for the default calibration parameters
        /// </summary>
        public void SetPressureHpa(double hpa)
        {
            configuredPressureHpa = hpa;
            ApplyConfiguredPressure();
        }

        private void ApplyConfiguredPressure()
        {
            if (!configuredPressureHpa.HasValue)
            {
                return;
            }

            var rawTemperature = GetRawTemperature();
            int targetPressure = (int)Math.Round(configuredPressureHpa.Value * 100.0);
            int rawPressure = FindClosestRawValue(raw => CompensatePressurePascals(raw, rawTemperature), targetPressure);
            SetRawPressure((uint)rawPressure);
        }

        private int FindClosestRawValue(Func<int, int> compensationFunction, int targetValue)
        {
            const int minRawValue = 0;
            const int maxRawValue = 0xFFFFF;

            int lowerBound = minRawValue;
            int upperBound = maxRawValue;
            int bestRawValue = minRawValue;
            long bestError = long.MaxValue;

            var lowerValue = compensationFunction(minRawValue);
            var upperValue = compensationFunction(maxRawValue);
            var isAscending = upperValue >= lowerValue;

            while (lowerBound <= upperBound)
            {
                var middle = lowerBound + ((upperBound - lowerBound) / 2);
                var compensated = compensationFunction(middle);
                var error = Math.Abs((long)compensated - targetValue);

                if (error < bestError)
                {
                    bestError = error;
                    bestRawValue = middle;
                }

                if (compensated == targetValue)
                {
                    return middle;
                }

                if (isAscending)
                {
                    if (compensated < targetValue)
                    {
                        lowerBound = middle + 1;
                    }
                    else
                    {
                        upperBound = middle - 1;
                    }
                }
                else
                {
                    if (compensated > targetValue)
                    {
                        lowerBound = middle + 1;
                    }
                    else
                    {
                        upperBound = middle - 1;
                    }
                }
            }

            return bestRawValue;
        }

        private int CompensateTemperatureCentiCelsius(int rawTemperature)
        {
            var tFine = CalculateTFine(rawTemperature);
            return (int)((tFine * 5L + 128) >> 8);
        }

        private int CompensatePressurePascals(int rawPressure, int rawTemperature)
        {
            var tFine = CalculateTFine(rawTemperature);

            long var1 = (tFine >> 1) - 64000;
            long var2 = (((var1 >> 2) * (var1 >> 2)) >> 11) * calibParams.dig_p6;
            var2 += (var1 * calibParams.dig_p5) << 1;
            var2 = (var2 >> 2) + ((long)calibParams.dig_p4 << 16);
            var1 = (((long)calibParams.dig_p3 * (((var1 >> 2) * (var1 >> 2)) >> 13)) >> 3) + (((long)calibParams.dig_p2 * var1) >> 1);
            var1 = (var1 >> 18);
            var1 = (((32768 + var1)) * calibParams.dig_p1) >> 15;

            if (var1 == 0)
            {
                return 0;
            }

            ulong converted = (ulong)((1048576 - rawPressure) - (var2 >> 12));
            converted *= 3125;

            if (converted < 0x80000000)
            {
                converted = (converted << 1) / (ulong)var1;
            }
            else
            {
                converted = (converted / (ulong)var1) * 2;
            }

            var1 = ((long)calibParams.dig_p9 * (long)(((converted >> 3) * (converted >> 3)) >> 13)) >> 12;
            var2 = (((long)(converted >> 2)) * calibParams.dig_p8) >> 13;
            converted = (ulong)((long)converted + ((var1 + var2 + calibParams.dig_p7) >> 4));

            return (int)converted;
        }

        private int CalculateTFine(int rawTemperature)
        {
            long var1 = ((((rawTemperature >> 3) - ((int)calibParams.dig_t1 << 1))) * calibParams.dig_t2) >> 11;
            long var2 = (((((rawTemperature >> 4) - calibParams.dig_t1) * ((rawTemperature >> 4) - calibParams.dig_t1)) >> 12) * calibParams.dig_t3) >> 14;
            return (int)(var1 + var2);
        }

        // Internal fields for testing access from Python
        internal byte[] registers;
        internal CalibParams calibParams;
        internal double? configuredTemperatureCelsius;
        internal double? configuredPressureHpa;

        private byte currentRegister;
        private bool isFirstWrite;
    }
}
