/**
 * MockClocks.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

namespace Antmicro.Renode.Peripherals.Clocks
{
    /// <summary>
    /// Mock clocks implementation for unit testing I2C peripheral.
    /// Provides configurable clock frequencies without full clock tree simulation.
    /// </summary>
    public class MockClocks
    {
        public MockClocks()
        {
            // Set default frequencies for testing
            SystemClockFrequency = 125000000;      // 125 MHz
            PeripheralClockFrequency = 125000000;  // 125 MHz
            ReferenceClockFrequency = 12000000;    // 12 MHz
            UsbClockFrequency = 48000000;          // 48 MHz
            AdcClockFrequency = 48000000;          // 48 MHz
            RtcClockFrequency = 46875;             // 46875 Hz
        }

        /// <summary>
        /// Allows tests to set a specific peripheral clock frequency.
        /// </summary>
        public void SetPeripheralClockFrequency(uint frequency)
        {
            PeripheralClockFrequency = frequency;
        }

        /// <summary>
        /// Allows tests to set a specific system clock frequency.
        /// </summary>
        public void SetSystemClockFrequency(uint frequency)
        {
            SystemClockFrequency = frequency;
        }

        public uint SystemClockFrequency { get; private set; }
        public uint PeripheralClockFrequency { get; private set; }
        public uint ReferenceClockFrequency { get; private set; }
        public uint UsbClockFrequency { get; private set; }
        public uint AdcClockFrequency { get; private set; }
        public uint RtcClockFrequency { get; private set; }
    }
}
