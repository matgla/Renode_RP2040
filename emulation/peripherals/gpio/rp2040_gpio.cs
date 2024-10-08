/**
 * rp2040_gpio.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.GPIOPort
{

    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public class RP2040GPIO : BaseGPIOPort, IDoubleWordPeripheral, IGPIOReceiver, IKnownSize
    {
        public RP2040GPIO(IMachine machine, int numberOfPins) : base(machine, numberOfPins)
        {
            functionSelectCallbacks = new List<Action<int, GpioFunction>>();
            NumberOfPins = numberOfPins;
            registers = CreateRegisters();
            Reset();
            functionSelect = new int[NumberOfPins];
            ReevaluatePioActions = new List<Action<uint>>();
            pullDown = Enumerable.Repeat<bool>(true, NumberOfPins).ToArray();
            pullUp = new bool[NumberOfPins];
            outputEnableOverride = new OutputEnableOverride[NumberOfPins];
            forcedOutputDisableMap = new bool[NumberOfPins];
            outputOverride = new OutputOverride[NumberOfPins];
            peripheralDrive = new PeripheralDrive[NumberOfPins];
            for (int i = 0; i < peripheralDrive.Length; ++i)
            {
                peripheralDrive[i] = PeripheralDrive.None;
            }
        }

        private bool IsPinOutput(int pin)
        {
            return forcedOutputDisableMap[pin] == false && outputEnableOverride[pin] != OutputEnableOverride.Disable;
        }

        public void SubscribeOnFunctionChange(Action<int, GpioFunction> callback)
        {
            functionSelectCallbacks.Add(callback);
        }

        public IGPIO GetGpio(int id)
        {
            return Connections[id];
        }

        public enum GpioFunction
        {
            SPI0_RX,
            SPI0_CSN,
            SPI0_SCK,
            SPI0_TX,
            SPI1_RX,
            SPI1_CSN,
            SPI1_SCK,
            SPI1_TX,
            UART0_TX,
            UART0_RX,
            UART0_CTS,
            UART0_RTS,
            UART1_TX,
            UART1_RX,
            UART1_CTS,
            UART1_RTS,
            I2C0_SDA,
            I2C0_SCL,
            I2C1_SDA,
            I2C1_SCL,
            PWM0_A,
            PWM0_B,
            PWM1_A,
            PWM1_B,
            PWM2_A,
            PWM2_B,
            PWM3_A,
            PWM3_B,
            PWM4_A,
            PWM4_B,
            PWM5_A,
            PWM5_B,
            PWM6_A,
            PWM6_B,
            PWM7_A,
            PWM7_B,
            SIO,
            PIO0,
            PIO1,
            CLOCK_GPIN0,
            CLOCK_GPIN1,
            CLOCK_GPOUT0,
            CLOCK_GPOUT1,
            CLOCK_GPOUT2,
            CLOCK_GPOUT3,
            USB_OVCUR_DET,
            USB_VBUS_DET,
            USB_VBUS_EN,
            NONE
        };

        private GpioFunction GetFunction(int pin)
        {
            // temporary check for QSPI GPIO, provide this by flag
            if (NumberOfPins == 6)
            {
                return GpioFunction.NONE;
            }

            if (functionSelect[pin] == 0 || functionSelect[pin] > 9)
            {
                return GpioFunction.NONE;
            }

            GpioFunction[,] pinMapping = new GpioFunction[30, 9]{
            /* 0 */    {GpioFunction.SPI0_RX,  GpioFunction.UART0_TX,  GpioFunction.I2C0_SDA, GpioFunction.PWM0_A, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_OVCUR_DET},
            /* 1 */    {GpioFunction.SPI0_CSN, GpioFunction.UART0_RX,  GpioFunction.I2C0_SCL, GpioFunction.PWM0_B, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_DET},
            /* 2 */    {GpioFunction.SPI0_SCK, GpioFunction.UART0_CTS, GpioFunction.I2C1_SDA, GpioFunction.PWM1_A, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_EN},
            /* 3 */    {GpioFunction.SPI0_TX,  GpioFunction.UART0_RTS, GpioFunction.I2C1_SCL, GpioFunction.PWM1_B, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_OVCUR_DET},
            /* 4 */    {GpioFunction.SPI0_RX,  GpioFunction.UART1_TX,  GpioFunction.I2C0_SDA, GpioFunction.PWM2_A, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_DET},
            /* 5 */    {GpioFunction.SPI0_CSN, GpioFunction.UART1_RX,  GpioFunction.I2C0_SCL, GpioFunction.PWM2_B, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_EN},
            /* 6 */    {GpioFunction.SPI0_SCK, GpioFunction.UART1_CTS, GpioFunction.I2C1_SDA, GpioFunction.PWM3_A, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_OVCUR_DET},
            /* 7 */    {GpioFunction.SPI0_TX,  GpioFunction.UART1_RTS, GpioFunction.I2C1_SCL, GpioFunction.PWM3_B, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_DET},
            /* 8 */    {GpioFunction.SPI1_RX,  GpioFunction.UART1_TX,  GpioFunction.I2C0_SDA, GpioFunction.PWM4_A, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_EN},
            /* 9 */    {GpioFunction.SPI1_SCK, GpioFunction.UART1_CTS, GpioFunction.I2C0_SCL, GpioFunction.PWM4_B, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_OVCUR_DET},
            /* 10 */   {GpioFunction.SPI1_TX,  GpioFunction.UART1_RTS, GpioFunction.I2C1_SDA, GpioFunction.PWM5_A, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_DET},
            /* 11 */   {GpioFunction.SPI1_RX,  GpioFunction.UART0_TX,  GpioFunction.I2C1_SCL, GpioFunction.PWM5_B, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_EN},
            /* 12 */   {GpioFunction.SPI1_CSN, GpioFunction.UART0_RX,  GpioFunction.I2C0_SDA, GpioFunction.PWM6_A, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_OVCUR_DET},
            /* 13 */   {GpioFunction.SPI1_SCK, GpioFunction.UART0_CTS, GpioFunction.I2C0_SCL, GpioFunction.PWM6_B, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_DET},
            /* 14 */   {GpioFunction.SPI1_TX,  GpioFunction.UART0_RTS, GpioFunction.I2C1_SDA, GpioFunction.PWM7_A, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_EN},
            /* 15 */   {GpioFunction.SPI0_RX,  GpioFunction.UART0_TX,  GpioFunction.I2C1_SCL, GpioFunction.PWM7_B, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_OVCUR_DET},
            /* 16 */   {GpioFunction.SPI0_CSN, GpioFunction.UART0_RX,  GpioFunction.I2C0_SDA, GpioFunction.PWM0_A, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_DET},
            /* 17 */   {GpioFunction.SPI0_SCK, GpioFunction.UART0_CTS, GpioFunction.I2C0_SCL, GpioFunction.PWM0_B, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_EN},
            /* 18 */   {GpioFunction.SPI0_TX,  GpioFunction.UART0_RTS, GpioFunction.I2C1_SDA, GpioFunction.PWM1_A, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_OVCUR_DET},
            /* 19 */   {GpioFunction.SPI0_RX,  GpioFunction.UART1_TX,  GpioFunction.I2C1_SCL, GpioFunction.PWM1_B, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_DET},
            /* 20 */   {GpioFunction.SPI0_CSN, GpioFunction.UART1_RX,  GpioFunction.I2C0_SDA, GpioFunction.PWM2_A, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.CLOCK_GPIN0, GpioFunction.USB_VBUS_EN},
            /* 21 */   {GpioFunction.SPI0_SCK, GpioFunction.UART1_CTS, GpioFunction.I2C0_SCL, GpioFunction.PWM2_B, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.CLOCK_GPOUT0, GpioFunction.USB_OVCUR_DET},
            /* 22 */   {GpioFunction.SPI0_TX,  GpioFunction.UART1_RTS, GpioFunction.I2C1_SDA, GpioFunction.PWM3_A, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.CLOCK_GPIN1, GpioFunction.USB_VBUS_DET},
            /* 23 */   {GpioFunction.SPI1_RX,  GpioFunction.UART1_TX,  GpioFunction.I2C1_SCL, GpioFunction.PWM3_B, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.CLOCK_GPOUT1, GpioFunction.USB_VBUS_EN},
            /* 24 */   {GpioFunction.SPI1_CSN, GpioFunction.UART1_RX,  GpioFunction.I2C0_SDA, GpioFunction.PWM4_A, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.CLOCK_GPOUT2, GpioFunction.USB_OVCUR_DET},
            /* 25 */   {GpioFunction.SPI1_SCK, GpioFunction.UART1_CTS, GpioFunction.I2C0_SCL, GpioFunction.PWM4_B, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.CLOCK_GPOUT3, GpioFunction.USB_VBUS_DET},
            /* 26 */   {GpioFunction.SPI1_TX,  GpioFunction.UART1_RTS, GpioFunction.I2C1_SDA, GpioFunction.PWM5_A, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_EN},
            /* 27 */   {GpioFunction.SPI1_RX,  GpioFunction.UART0_TX,  GpioFunction.I2C1_SCL, GpioFunction.PWM5_B, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_OVCUR_DET},
            /* 28 */   {GpioFunction.SPI1_CSN, GpioFunction.UART0_RX,  GpioFunction.I2C0_SDA, GpioFunction.PWM6_A, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_DET},
            /* 29 */   {GpioFunction.SPI1_CSN, GpioFunction.UART0_RX,  GpioFunction.I2C0_SCL, GpioFunction.PWM6_B, GpioFunction.SIO, GpioFunction.PIO0, GpioFunction.PIO1, GpioFunction.NONE, GpioFunction.USB_VBUS_EN}
            };

            return pinMapping[pin, functionSelect[pin] - 1];
        }

        private void EvaluatePinInterconnections(int[] previous)
        {
            for (int i = 0; i < NumberOfPins; ++i)
            {
                if (previous[i] != functionSelect[i])
                {
                    this.Log(LogLevel.Noisy, "GPIO" + i + ": has function: " + GetFunction(i));
                    foreach (var action in functionSelectCallbacks)
                    {
                        action(i, GetFunction(i));
                    }
                }
            }
        }

        private void SetPinAccordingToPulls(int pin)
        {
            if (pullDown[pin] == true)
            {
                State[pin] = false;
                Connections[pin].Set(false);
            }
            if (pullUp[pin] == true)
            {
                State[pin] = true;
                Connections[pin].Set(true);
            }
        }

        private DoubleWordRegisterCollection CreateRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>();
            // That's true for both GPIO and QSPI GPIO  
            // 0x00 + 0x8 * i = STATUSES
            // 0x04 + 0x8 * i = CTRL
            // NumberOfPins * 0x8 = INTR0

            for (int p = 0; p < NumberOfPins; ++p)
            {
                int i = p;

                registersMap[i * 8] = new DoubleWordRegister(this)
                    .WithReservedBits(0, 8)
                    .WithFlag(8, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_OUTFROMPERI")
                    .WithFlag(9, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_OUTTOPAD")
                    .WithReservedBits(10, 2)
                    .WithFlag(12, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_OEFROMPERI")
                    .WithFlag(13, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_OETOPAD")
                    .WithReservedBits(14, 3)
                    .WithFlag(17, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_INFROMPAD")
                    .WithReservedBits(18, 1)
                    .WithFlag(19, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_INTOPERI")
                    .WithReservedBits(20, 4)
                    .WithFlag(24, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_IRQFROMPAD")
                    .WithReservedBits(25, 1)
                    .WithFlag(26, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_IRQTOPROC")
                    .WithReservedBits(27, 5);

                registersMap[i * 8 + 0x04] = new DoubleWordRegister(this)
                    .WithValueField(0, 5, FieldMode.Read | FieldMode.Write,
                        valueProviderCallback: _ =>
                        {
                            return (ulong)functionSelect[i];
                        },
                        writeCallback: (_, value) =>
                        {
                            int[] oldFunctions = new int[NumberOfConnections];
                            functionSelect.CopyTo(oldFunctions, 0);
                            functionSelect[i] = (int)value;
                            EvaluatePinInterconnections(oldFunctions);
                        },
                        name: "GPIO" + i + "_CTRL_FUNCSEL")
                    .WithReservedBits(5, 3)
                    .WithValueField(8, 2,
                        valueProviderCallback: _ =>
                        {
                            return (ulong)outputOverride[i];
                        },
                        writeCallback: (_, value) =>
                        {
                            outputOverride[i] = (OutputOverride)value;
                            if (outputOverride[i] == OutputOverride.Peripheral || outputOverride[i] == OutputOverride.InversePeripheral)
                            {
                                // left the job for a peripheral
                                return;
                            }

                            if (IsPinOutput(i))
                            {
                                if (outputOverride[i] == OutputOverride.Low)
                                {
                                    WritePin(i, false, GetFunction(i));
                                }
                                else if (outputOverride[i] == OutputOverride.High)
                                {
                                    WritePin(i, true, GetFunction(i));
                                }
                            }
                            else
                            {
                                this.Log(LogLevel.Error, "GPIO " + i + ": Set output but not output");
                                SetPinAccordingToPulls(i);
                            }
                        },
                        name: "GPIO" + i + "_CTRL_OUTOVER")
                    .WithReservedBits(10, 2)
                    .WithValueField(12, 2,
                        valueProviderCallback: _ =>
                        {
                            return (ulong)outputEnableOverride[i];
                        },
                        writeCallback: (_, value) =>
                        {
                            outputEnableOverride[i] = (OutputEnableOverride)value;
                            switch (value)
                            {
                                case 0:
                                case 1:
                                    {
                                        // let a peripheral enable output
                                        break;
                                    }
                                case 2:
                                    {
                                        SetPinAccordingToPulls(i);
                                        break;
                                    }
                                case 3:
                                    {
                                        WritePin(i, outputOverride[i] == OutputOverride.High, GetFunction(i));
                                        break;
                                    }
                            }
                        },
                        name: "GPIO" + i + "_CTRL_OEOVER")
                    .WithReservedBits(14, 2)
                    .WithValueField(16, 2, name: "GPIO" + i + "_CTRL_INOVER")
                    .WithReservedBits(18, 10)
                    .WithValueField(28, 2, name: "GPIO" + i + "_CTRL_IRQOVER")
                    .WithReservedBits(30, 2);
            }

            int intr0p0 = NumberOfPins * 8;
            int numberOfIntRegisters = (int)Math.Ceiling((decimal)NumberOfPins / 8);
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[intr0p0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTR0" + p + "_PROC0");
            }

            int inte0p0 = intr0p0 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[inte0p0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTE0" + p + "_PROC0");
            }


            int intf0p0 = inte0p0 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[intf0p0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTF0" + p + "_PROC0");
            }


            int ints0p0 = intf0p0 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[ints0p0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTS0" + p + "_PROC0");
            }

            int intr0p1 = ints0p0 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[intr0p1 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTR0" + p + "_PROC1");
            }

            int inte0p1 = intr0p1 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[inte0p1 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTE0" + p + "_PROC1");
            }

            int intf0p1 = inte0p1 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[intf0p1 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTF0" + p + "_PROC1");
            }

            int ints0p1 = intf0p1 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[ints0p1 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTS0" + p + "_PROC1");
            }

            int dormantWakeInte0 = ints0p1 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[dormantWakeInte0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "DORMANT_WAKE_INTE" + p);
            }

            int dormantWakeIntf0 = dormantWakeInte0 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[dormantWakeIntf0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "DORMANT_WAKE_INTF" + p);
            }

            int dormantWakeInts0 = dormantWakeIntf0 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[dormantWakeInts0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "DORMANT_WAKE_INTS" + p);
            }

            return new DoubleWordRegisterCollection(this, registersMap);
        }

        public bool GetPullDown(int pin)
        {
            return pullDown[pin];
        }

        public bool GetPullUp(int pin)
        {
            return pullUp[pin];
        }

        public void SetPullDown(int pin, bool state)
        {
            pullDown[pin] = state;
            if (!IsPinOutput(pin) && state == true)
            {
                State[pin] = false;
                Connections[pin].Set(false);
            }
        }

        public void SetPullUp(int pin, bool state)
        {
            pullUp[pin] = state;
            if (!IsPinOutput(pin) && state == true)
            {
                State[pin] = true;
                Connections[pin].Set(true);
            }
        }

        public void SetPinOutput(int pin, bool state)
        {
            outputEnableOverride[pin] = state ? OutputEnableOverride.Enable : OutputEnableOverride.Disable;
        }

        // Disable from PADS has greater priority than from GPIO
        public bool IsPinOutputForcedDisabled(int pin)
        {
            return forcedOutputDisableMap[pin];
        }

        public void ForcePinOutputDisable(int pin, bool state)
        {
            forcedOutputDisableMap[pin] = state;
        }

        public bool GetGpioState(uint number)
        {
            return State[number];
        }

        public uint GetGpioStateBitmap()
        {
            uint output = 0;
            for (int i = 0; i < NumberOfPins; ++i)
            {
                output |= Convert.ToUInt32(State[i]) << i;
            }
            return output;
        }

        public void SetGpioBitmap(ulong bitmap, GpioFunction peri)
        {
            for (int i = 0; i < NumberOfPins; ++i)
            {
                if ((bitmap & (1UL << i)) != 0)
                {
                    WritePin(i, true, peri);
                }
                else
                {
                    WritePin(i, false, peri);
                }
            }
        }

        public void SetGpioBitset(ulong bitset, GpioFunction peri, ulong bitmask = 0xfffffff)
        {
            for (int i = 0; i < NumberOfPins; ++i)
            {
                if ((bitmask & (1UL << i)) == 0)
                {
                    continue;
                }

                if ((bitset & (1UL << i)) != 0)
                {
                    if (State[i] == false)
                    {
                        WritePin(i, true, peri);
                    }
                }
                else
                {
                    if (State[i] == true)
                    {
                        WritePin(i, false, peri);
                    }
                }
            }
        }

        public void SetPinDirectionBitset(ulong bitset, ulong bitmask = 0xffffffff)
        {
            for (int i = 0; i < NumberOfPins; ++i)
            {
                if ((bitmask & (1UL << i)) == 0)
                {
                    continue;
                }

                if ((bitset & (1UL << i)) != 0)
                {
                    outputEnableOverride[i] = OutputEnableOverride.Enable;
                }
                else
                {
                    outputEnableOverride[i] = OutputEnableOverride.Disable;
                }
            }
        }


        public void ClearGpioBitset(ulong bitset, GpioFunction peri)
        {
            for (int i = 0; i < NumberOfPins; ++i)
            {
                if ((bitset & (1UL << i)) != 0)
                {
                    WritePin(i, false, peri);
                }
            }
        }

        public void XorGpioBitset(ulong bitset, GpioFunction peri)
        {
            for (int i = 0; i < NumberOfPins; ++i)
            {
                bool state = State[i];
                if ((bitset & (1UL << i)) != 0)
                {
                    state = state ^ true;
                }
                else
                {
                    state = state ^ false;
                }
                WritePin(i, state, peri);
            }
        }

        public void ClearOutputEnableBitset(ulong bitset, GpioFunction peri)
        {
            for (int i = 0; i < NumberOfPins; ++i)
            {
                bool enable = (bitset & (1UL << i)) != 0;
                if (outputEnableOverride[i] == OutputEnableOverride.Peripheral || outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                {
                    if (GetFunction(i) != peri)
                    {
                        continue;
                    }

                    if (outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                    {
                        enable = !enable;
                    }
                }


                if (enable)
                {
                    outputEnableOverride[i] = OutputEnableOverride.Disable;
                }
            }
        }

        public void XorOutputEnableBitset(ulong bitset, GpioFunction peri)
        {
            for (int i = 0; i < NumberOfPins; ++i)
            {
                bool state;

                bool enable = (bitset & (1UL << i)) != 0;
                if (outputEnableOverride[i] == OutputEnableOverride.Peripheral || outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                {
                    if (GetFunction(i) != peri)
                    {
                        continue;
                    }

                    if (outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                    {
                        enable = !enable;
                    }
                }


                if (enable)
                {
                    state = IsPinOutput(i) ^ true;
                }
                else
                {
                    state = IsPinOutput(i) ^ false;
                }
                outputEnableOverride[i] = state ? OutputEnableOverride.Enable : OutputEnableOverride.Disable;
            }
        }

        public uint GetOutputEnableBitmap()
        {
            uint output = 0;
            for (int i = 0; i < NumberOfPins; ++i)
            {
                output |= Convert.ToUInt32(outputEnableOverride[i] == OutputEnableOverride.Enable) << i;
            }
            return output;
        }

        public void SetOutputEnableBitmap(ulong bitmap, GpioFunction peri)
        {

            for (int i = 0; i < NumberOfPins; ++i)
            {
                bool enable = (bitmap & (1UL << i)) != 0;
                if (outputEnableOverride[i] == OutputEnableOverride.Peripheral || outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                {
                    if (GetFunction(i) != peri)
                    {
                        continue;
                    }

                    if (outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                    {
                        enable = !enable;
                    }
                }

                if (enable)
                {
                    outputEnableOverride[i] = OutputEnableOverride.Enable;
                }
                else
                {
                    outputEnableOverride[i] = OutputEnableOverride.Disable;
                }
            }
        }

        public void SetOutputEnableBitset(ulong bitset, GpioFunction peri, ulong bitmask = 0xfffffff)
        {
            for (int i = 0; i < NumberOfPins; ++i)
            {
                if ((bitmask & (1UL << i)) == 0)
                {
                    continue;
                }

                bool enable = (bitset & (1UL << i)) != 0;
                if (outputEnableOverride[i] == OutputEnableOverride.Peripheral || outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                {
                    if (GetFunction(i) != peri)
                    {
                        continue;
                    }

                    if (outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                    {
                        enable = !enable;
                    }
                }

                if (enable)
                {
                    outputEnableOverride[i] = OutputEnableOverride.Enable;
                }
                else
                {
                    outputEnableOverride[i] = OutputEnableOverride.Disable;
                }
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void OnGPIO(int number, bool value)
        {
            base.OnGPIO(number, value);
            WritePin(number, value, GetFunction(number));
        }

        // most probably may hide some bugs, but full emulation of gpio function interconnection may not be necessary in most cases
        public void WritePin(int number, bool value)
        {
            WritePin(number, value, GetFunction(number));
        }

        public void WritePin(int number, bool value, GpioFunction peri)
        {
            this.Log(LogLevel.Noisy, "Setting GPIO" + number + " to: " + value + ", time: " + machine.ElapsedVirtualTime.TimeElapsed + ", from: " + peri);
            if (!IsPinOutput(number))
            {
                this.Log(LogLevel.Warning, "Trying to set input pin: " + number + " to: " + value);
                return;
            }

            if (peripheralDrive[number] != PeripheralDrive.None && GetFunction(number) != peri)
            {
                this.Log(LogLevel.Error, "Driving GPIO from not selected peripheral, gpio configured with: " + GetFunction(number) + ". Request received from: " + peri);
            }

            if (peripheralDrive[number] == PeripheralDrive.Inverse)
            {
                value = !value;
            }
            State[number] = value;
            Connections[number].Set(value);
        }

        public void ReevaluatePio(uint steps)
        {
            foreach (var a in ReevaluatePioActions)
            {
                a(steps);
            }
        }

        public long Size { get { return 0x1000; } }
        public int[] functionSelect;

        public int NumberOfPins;

        // Currently I have no better idea how to retrigger CPU evaluation when GPIO state changes 
        // This is necessary to have synchronized PIO with System Clock
        public List<Action<uint>> ReevaluatePioActions { get; set; }


        private readonly DoubleWordRegisterCollection registers;
        private bool[] pullDown;
        private bool[] pullUp;
        private enum OutputOverride
        {
            Peripheral = 0,
            InversePeripheral = 1,
            Low = 2,
            High = 3
        };

        private OutputOverride[] outputOverride;
        private enum OutputEnableOverride
        {
            Peripheral = 0,
            InversePeripheral = 1,
            Disable = 2,
            Enable = 3
        };
        private OutputEnableOverride[] outputEnableOverride;
        private bool[] forcedOutputDisableMap;

        private List<Action<int, GpioFunction>> functionSelectCallbacks;

        private enum PeripheralDrive
        {
            None,
            Normal,
            Inverse
        };
        private PeripheralDrive[] peripheralDrive;
    }

}
