/**
 * MockGPIO.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    /// <summary>
    /// Mock GPIO implementation for unit testing I2C peripheral.
    /// Tracks pin states and function changes without full GPIO emulation.
    /// </summary>
    public class MockGPIO : IGPIOReceiver, IPeripheral
    {
        private readonly int numberOfPins;
        private readonly Dictionary<int, bool> pinStates;
        private readonly Dictionary<int, bool> pinDirections; // true = output
        private readonly Dictionary<int, RP2040GPIO.GpioFunction> pinFunctions;
        private readonly List<Action<int, RP2040GPIO.GpioFunction>> functionChangeCallbacks;

        public MockGPIO(int numberOfPins = 30)
        {
            this.numberOfPins = numberOfPins;
            pinStates = new Dictionary<int, bool>();
            pinDirections = new Dictionary<int, bool>();
            pinFunctions = new Dictionary<int, RP2040GPIO.GpioFunction>();
            functionChangeCallbacks = new List<Action<int, RP2040GPIO.GpioFunction>>();

            // Initialize all pins to default state (high/input/none)
            for (int i = 0; i < numberOfPins; i++)
            {
                pinStates[i] = true;  // Default high (pulled up)
                pinDirections[i] = false;  // Default input
                pinFunctions[i] = RP2040GPIO.GpioFunction.NONE;
            }
        }

        /// <summary>
        /// Reset the mock GPIO state.
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < numberOfPins; i++)
            {
                pinStates[i] = true;
                pinDirections[i] = false;
                pinFunctions[i] = RP2040GPIO.GpioFunction.NONE;
            }
            functionChangeCallbacks.Clear();
        }

        /// <summary>
        /// Subscribe to GPIO function change events.
        /// Required by I2C to detect when pins are configured for I2C function.
        /// </summary>
        public void SubscribeOnFunctionChange(Action<int, RP2040GPIO.GpioFunction> callback)
        {
            functionChangeCallbacks.Add(callback);
        }

        /// <summary>
        /// Set the function of a pin and notify subscribers.
        /// Used by tests to simulate I2C pin configuration.
        /// </summary>
        public void SetPinFunction(int pin, RP2040GPIO.GpioFunction function)
        {
            if (pin < 0 || pin >= numberOfPins)
                throw new ArgumentOutOfRangeException(nameof(pin));

            pinFunctions[pin] = function;
            
            // Notify all subscribers
            foreach (var callback in functionChangeCallbacks)
            {
                callback(pin, function);
            }
        }

        /// <summary>
        /// Get the current function of a pin.
        /// </summary>
        public RP2040GPIO.GpioFunction GetPinFunction(int pin)
        {
            if (pin < 0 || pin >= numberOfPins)
                throw new ArgumentOutOfRangeException(nameof(pin));

            return pinFunctions[pin];
        }

        /// <summary>
        /// Set pin as output or input.
        /// Called by I2C peripheral to drive SDA/SCL lines.
        /// </summary>
        public void SetPinOutput(int pin, bool output)
        {
            if (pin < 0 || pin >= numberOfPins)
                throw new ArgumentOutOfRangeException(nameof(pin));

            pinDirections[pin] = output;
        }

        /// <summary>
        /// Write a value to a pin.
        /// Called by I2C peripheral to drive lines low (SDA/SCL).
        /// </summary>
        public void WritePin(int pin, bool value)
        {
            if (pin < 0 || pin >= numberOfPins)
                throw new ArgumentOutOfRangeException(nameof(pin));

            // Only write if pin is configured as output
            if (pinDirections[pin])
            {
                pinStates[pin] = value;
            }
        }

        /// <summary>
        /// Get the state of a GPIO pin.
        /// Called by I2C peripheral to read SDA line.
        /// </summary>
        public bool GetGpioState(uint pin)
        {
            if (pin < 0 || pin >= numberOfPins)
                return true; // Default high (pulled up)

            return pinStates[(int)pin];
        }

        /// <summary>
        /// Set the state of a pin (simulating external pull-up or device driving).
        /// Used by tests to simulate I2C device responses.
        /// </summary>
        public void SetGpioState(int pin, bool value)
        {
            if (pin < 0 || pin >= numberOfPins)
                throw new ArgumentOutOfRangeException(nameof(pin));

            pinStates[pin] = value;
        }

        /// <summary>
        /// Check if a pin is configured as output.
        /// </summary>
        public bool IsPinOutput(int pin)
        {
            if (pin < 0 || pin >= numberOfPins)
                throw new ArgumentOutOfRangeException(nameof(pin));

            return pinDirections[pin];
        }

        /// <summary>
        /// IGPIOReceiver interface implementation.
        /// Called when external signals drive GPIO pins.
        /// </summary>
        public void OnGPIO(int number, bool value)
        {
            // In mock, just update the state
            if (number >= 0 && number < numberOfPins)
            {
                pinStates[number] = value;
            }
        }

        /// <summary>
        /// Configure pins for I2C0 function.
        /// Convenience method for tests.
        /// </summary>
        public void ConfigureI2C0Pins(int sdaPin = 0, int sclPin = 1)
        {
            SetPinFunction(sdaPin, RP2040GPIO.GpioFunction.I2C0_SDA);
            SetPinFunction(sclPin, RP2040GPIO.GpioFunction.I2C0_SCL);
        }

        /// <summary>
        /// Configure pins for I2C1 function.
        /// Convenience method for tests.
        /// </summary>
        public void ConfigureI2C1Pins(int sdaPin = 2, int sclPin = 3)
        {
            SetPinFunction(sdaPin, RP2040GPIO.GpioFunction.I2C1_SDA);
            SetPinFunction(sclPin, RP2040GPIO.GpioFunction.I2C1_SCL);
        }

        /// <summary>
        /// Get the number of pins.
        /// </summary>
        public int NumberOfPins => numberOfPins;
    }
}
