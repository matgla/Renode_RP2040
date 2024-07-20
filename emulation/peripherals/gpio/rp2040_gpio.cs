using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.GPIOPort
{

    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public class RP2040GPIO : BaseGPIOPort, IDoubleWordPeripheral, IGPIOReceiver, IKnownSize
    {
        public RP2040GPIO(IMachine machine) : base(machine, NumberOfPins)
        {
            registers = CreateRegisters();
            Reset();
            functionSelect = new int[NumberOfPins];
            PinDirections = new Direction[NumberOfPins];
        }

        public IGPIO GetGpio(int id)
        {
            return Connections[id];
        }

        public long Size { get { return 0x1000; } }
        public int[] functionSelect;

        public const int NumberOfPins = 29;
        public enum Direction : byte
        {
            Input,
            Output
        };
        public Direction[] PinDirections { get; set; }

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
                            functionSelect[i] = (int)value;
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
            this.Log(LogLevel.Error, "GPIO " + number + " -> " + value);
            base.OnGPIO(number, value);
        }

        public void WritePin(int number, bool value)
        {
            this.Log(LogLevel.Error, "WritingPin " + number + " -> " + value);
            State[number] = value;
            Connections[number].Set(value);
        }

        private readonly DoubleWordRegisterCollection registers;

    }

}
