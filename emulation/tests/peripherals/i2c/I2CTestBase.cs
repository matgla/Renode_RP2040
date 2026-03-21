/**
 * I2CTestBase.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.GPIOPort;
using Antmicro.Renode.Peripherals.Clocks;
using Antmicro.Renode.Peripherals.I2C;
using Xunit;

namespace Antmicro.Renode.Tests.Peripherals.I2C
{
    /// <summary>
    /// Base class for I2C peripheral unit tests.
    /// Provides common setup and helper methods.
    /// </summary>
    public abstract class I2CTestBase : IDisposable
    {
        protected MockMachine Machine { get; private set; }
        protected MockGPIO Gpio { get; private set; }
        protected MockClocks Clocks { get; private set; }
        protected RP2040I2C I2C { get; private set; }
        protected int I2CId { get; private set; }

        /// <summary>
        /// Creates a fresh I2C peripheral instance for each test.
        /// </summary>
        protected void InitializeI2C(int i2cId = 0)
        {
            I2CId = i2cId;
            Machine = new MockMachine();
            Gpio = new MockGPIO();
            Clocks = new MockClocks();
            I2C = new RP2040I2C(Machine, Gpio, i2cId, Clocks);
        }

        /// <summary>
        /// Configures GPIO pins for I2C function.
        /// </summary>
        protected void ConfigureI2CPins(int sdaPin = 0, int sclPin = 1)
        {
            if (I2CId == 0)
            {
                Gpio.ConfigureI2C0Pins(sdaPin, sclPin);
            }
            else
            {
                Gpio.ConfigureI2C1Pins(sdaPin, sclPin);
            }
        }

        /// <summary>
        /// Enables the I2C peripheral in master mode.
        /// </summary>
        protected void EnableI2C()
        {
            // IC_CON: Master mode (bit 0), speed = fast mode (bits 1-2 = 2), slave disable (bit 6)
            I2C.WriteDoubleWord(0x00, 0x00000065);
            // IC_ENABLE: Enable I2C
            I2C.WriteDoubleWord(0x6C, 0x00000001);
        }

        /// <summary>
        /// Sets the target address for I2C communication.
        /// </summary>
        protected void SetTargetAddress(byte address)
        {
            // IC_TAR: Set target address
            I2C.WriteDoubleWord(0x04, (uint)address);
        }

        /// <summary>
        /// Writes data to the I2C data/command register.
        /// </summary>
        protected void WriteDataCommand(byte data, bool stop = false, bool read = false, bool restart = false)
        {
            uint value = data;
            if (stop) value |= 0x200;      // STOP bit
            if (read) value |= 0x100;      // READ bit (CMD)
            if (restart) value |= 0x400;   // RESTART bit

            I2C.WriteDoubleWord(0x10, value);
        }

        /// <summary>
        /// Registers a mock I2C device at the specified address.
        /// </summary>
        protected MockI2CDevice RegisterMockDevice(byte address)
        {
            var device = new MockI2CDevice(address);
            I2C.RegisterChild(device, address);
            return device;
        }

        /// <summary>
        /// Asserts that a register read returns the expected value.
        /// </summary>
        protected void AssertRegisterValue(long offset, uint expected, string message = null)
        {
            var actual = I2C.ReadDoubleWord(offset);
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Asserts that the IRQ signal is set.
        /// </summary>
        protected void AssertIrqSet()
        {
            Assert.True(I2C.IRQ.IsSet, "Expected IRQ to be set");
        }

        /// <summary>
        /// Asserts that the IRQ signal is not set.
        /// </summary>
        protected void AssertIrqNotSet()
        {
            Assert.False(I2C.IRQ.IsSet, "Expected IRQ to not be set");
        }

        /// <summary>
        /// Disposes test resources.
        /// </summary>
        public virtual void Dispose()
        {
            I2C?.Reset();
        }
    }
}
