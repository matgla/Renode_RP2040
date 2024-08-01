using System;
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
            PinDirections = new Direction[NumberOfPins];
            ReevaluatePio = (uint cycles) => { };
        }

        public void SubscribeOnFunctionChange(Action<int, GpioFunction> callback)
        {
            functionSelectCallbacks.Add(callback);
        }

        public IGPIO GetGpio(int id)
        {
            return Connections[id];
        }

        public long Size { get { return 0x1000; } }
        public int[] functionSelect;

        public int NumberOfPins;
        public enum Direction : byte
        {
            Input,
            Output
        };
        public Direction[] PinDirections { get; set; }

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

        private DoubleWordRegisterCollection CreateRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>();

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
                    .WithValueField(8, 2, FieldMode.Read | FieldMode.Write,
                        valueProviderCallback: _ =>
                        {
                            if (State[i])
                            {
                                return 0x02;
                            }
                            return 0x03;
                        },
                        writeCallback: (_, value) =>
                        {
                            if (value == 0x02)
                            {
                                WritePin(i, false);
                            }
                            else if (value == 0x03)
                            {
                                WritePin(i, true);
                            }
                        },
                        name: "GPIO" + i + "_CTRL_OUTOVER")
                    .WithReservedBits(10, 2)
                    .WithValueField(12, 2, FieldMode.Read | FieldMode.Write,
                        valueProviderCallback: _ =>
                        {
                            return 0;
                        },
                        writeCallback: (_, value) =>
                        {
                        },
                        name: "GPIO" + i + "_CTRL_OEOVER")
                    .WithReservedBits(14, 2)
                    .WithValueField(16, 2, FieldMode.Read | FieldMode.Write,
                        valueProviderCallback: _ =>
                        {
                            return 0;
                        },
                        writeCallback: (_, value) =>
                        {
                        },
                        name: "GPIO" + i + "_CTRL_INOVER")
                    .WithReservedBits(18, 10)
                    .WithValueField(28, 2, FieldMode.Read | FieldMode.Write,
                        valueProviderCallback: _ =>
                        {
                            return 0;
                        },
                        name: "GPIO" + i + "_CTRL_IRQOVER")
                    .WithReservedBits(30, 2);

            }

            return new DoubleWordRegisterCollection(this, registersMap);
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

        public void SetGpioBitmap(ulong bitmap)
        {
            for (int i = 0; i < NumberOfPins; ++i)
            {
                if ((bitmap & (1UL << i)) != 0)
                {
                    WritePin(i, true);
                }
                else
                {
                    WritePin(i, false);
                }
            }
        }

        public void SetGpioBitset(ulong bitset, ulong bitmask = 0xfffffff)
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
                        WritePin(i, true);
                    }
                }
                else
                {
                    if (State[i] == true)
                    {
                        WritePin(i, false);
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
                    PinDirections[i] = Direction.Output;
                }
                else
                {
                    PinDirections[i] = Direction.Input;
                }

            }
        }


        public void ClearGpioBitset(ulong bitset)
        {
            for (int i = 0; i < NumberOfPins; ++i)
            {
                if ((bitset & (1UL << i)) != 0)
                {
                    WritePin(i, false);
                }
            }
        }

        public void XorGpioBitset(ulong bitset)
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
                WritePin(i, state);
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
            WritePin(number, value);
        }

        public void WritePin(int number, bool value)
        {
            this.Log(LogLevel.Noisy, "Setting GPIO" + number + " to: " + value + ", time: " + machine.ElapsedVirtualTime.TimeElapsed);
            bool previousState = State[number];
            State[number] = value;
            Connections[number].Set(value);
        }

        private readonly DoubleWordRegisterCollection registers;

        // Currently I have no better idea how to retrigger CPU evaluation when GPIO state changes 
        // This is necessary to have synchronized PIO with System Clock
        public Action<uint> ReevaluatePio { get; set; }


        List<Action<int, GpioFunction>> functionSelectCallbacks;
    }

}
