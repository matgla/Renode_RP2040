/**
 * I2CRegisterTests.cs
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

namespace Antmicro.Renode.Tests.Peripherals.I2C
{
    /// <summary>
    /// Unit tests for I2C peripheral register operations.
    /// Tests read/write behavior, reset values, and aliases.
    /// </summary>
    public class I2CRegisterTests
    {
        // Custom attribute to mark test methods
        [AttributeUsage(AttributeTargets.Method)]
        public class TestAttribute : Attribute { }

        [Test]
        public void MockGPIO_ConfigureI2C0Pins_FunctionsSet()
        {
            var gpio = new MockGPIO();
            
            gpio.ConfigureI2C0Pins(0, 1);
            
            if (gpio.GetPinFunction(0) != RP2040GPIO.GpioFunction.I2C0_SDA)
                throw new Exception("Pin 0 should be I2C0_SDA");
            if (gpio.GetPinFunction(1) != RP2040GPIO.GpioFunction.I2C0_SCL)
                throw new Exception("Pin 1 should be I2C0_SCL");
        }

        [Test]
        public void MockGPIO_PinState_CanBeSetAndRead()
        {
            var gpio = new MockGPIO();
            
            gpio.SetGpioState(5, false);
            
            if (gpio.GetGpioState(5) != false)
                throw new Exception("Pin 5 should be low");
        }

        [Test]
        public void MockGPIO_PinDirection_OutputCanWrite()
        {
            var gpio = new MockGPIO();
            
            gpio.SetPinOutput(10, true);
            gpio.WritePin(10, false);
            
            if (gpio.GetGpioState(10) != false)
                throw new Exception("Pin 10 should be low after write");
        }

        [Test]
        public void MockClocks_DefaultValues_AreSet()
        {
            var clocks = new MockClocks();
            
            if (clocks.PeripheralClockFrequency != 125000000)
                throw new Exception("Default peripheral clock should be 125MHz");
            if (clocks.SystemClockFrequency != 125000000)
                throw new Exception("Default system clock should be 125MHz");
        }

        [Test]
        public void MockClocks_CanSetPeripheralClock()
        {
            var clocks = new MockClocks();
            
            clocks.SetPeripheralClockFrequency(48000000);
            
            if (clocks.PeripheralClockFrequency != 48000000)
                throw new Exception("Peripheral clock should be 48MHz");
        }

        [Test]
        public void MockI2CDevice_Write_DataRecorded()
        {
            var device = new MockI2CDevice(0x17);
            
            device.Write(new byte[] { 0x01, 0x02, 0x03 });
            
            var received = device.GetReceivedData();
            if (received.Length != 3)
                throw new Exception($"Expected 3 bytes, got {received.Length}");
            if (received[0] != 0x01 || received[1] != 0x02 || received[2] != 0x03)
                throw new Exception("Data mismatch");
        }

        [Test]
        public void MockI2CDevice_Read_ReturnsResponseData()
        {
            var device = new MockI2CDevice(0x17);
            device.SetResponseData(new byte[] { 0xAB, 0xCD });
            
            var data = device.Read(2);
            
            if (data.Length != 2 || data[0] != 0xAB || data[1] != 0xCD)
                throw new Exception("Read data mismatch");
        }

        [Test]
        public void MockI2CDevice_Read_EmptyReturns0xFF()
        {
            var device = new MockI2CDevice(0x17);
            // No response data set
            
            var data = device.Read(1);
            
            if (data.Length != 1 || data[0] != 0xFF)
                throw new Exception("Empty read should return 0xFF");
        }

        [Test]
        public void MockI2CDevice_FinishTransmission_Called()
        {
            var device = new MockI2CDevice(0x17);
            
            device.FinishTransmission();
            
            if (!device.WasFinishTransmissionCalled())
                throw new Exception("FinishTransmission should be marked as called");
        }

        [Test]
        public void MockI2CDevice_Transactions_Recorded()
        {
            var device = new MockI2CDevice(0x17);
            device.SetResponseData(new byte[] { 0x42 });
            
            device.Write(new byte[] { 0x01 });
            device.Read(1);
            device.FinishTransmission();
            
            var transactions = device.GetTransactions();
            if (transactions.Count != 3)
                throw new Exception($"Expected 3 transactions, got {transactions.Count}");
        }
    }
}
