using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.GPIOPort;


namespace Antmicro.Renode.Peripherals.Miscellaneous
{

    public class RP2040PIO : BasicDoubleWordPeripheral, IKnownSize
    {
        private enum Registers
        {
            CTRL = 0x00,
            FSTAT = 0x04,
            FDEBUG = 0x08,
            FLEVEL = 0x0c,
            IRQ = 0x30,
            IRQ_FORCE = 0x34,
            INPUT_SYNC_BYPASS = 0x38,
            SM0_CLKDIV = 0xc8,
            SM0_EXECCTRL = 0xcc,
            SM0_SHIFTCTRL = 0xd0,
            SM0_ADDR = 0xd4,
            SM0_INSTR = 0xd8,
            SM0_PINCTRL = 0xdc,
            SM1_CLKDIV = 0xe0,
            SM1_EXECCTRL = 0xe4,
            SM1_SHIFTCTRL = 0xe8,
            SM1_ADDR = 0xec,
            SM1_INSTR = 0xf0,
            SM1_PINCTRL = 0xf4,
            SM2_CLKDIV = 0xf8,
            SM2_EXECCTRL = 0xfc,
            SM2_SHIFTCTRL = 0x100,
            SM2_ADDR = 0x104,
            SM2_INSTR = 0x108,
            SM2_PINCTRL = 0x10c,
            SM3_CLKDIV = 0x110,
            SM3_EXECCTRL = 0x114,
            SM3_SHIFTCTRL = 0x118,
            SM3_ADDR = 0x11c,
            SM3_INSTR = 0x120,
            SM3_PINCTRL = 0x124,
        }

        public RP2040PIO(Machine machine, RP2040GPIO gpio) : base(machine)
        {
            this.Log(LogLevel.Error, "RP2040 PIO");
            IRQs = new GPIO[2];
            for (int i = 0; i < IRQs.Length; ++i)
            {
                IRQs[i] = new GPIO();
            }
            Instructions = new ushort[32];
            StateMachines = new PioStateMachine[4];

            this.gpio = gpio;
            for (int i = 0; i < StateMachines.Length; ++i)
            {
                StateMachines[i] = new PioStateMachine(machine, Instructions, i, gpio, this.Log);
            }

            DefineRegisters();
            Reset();
        }

        private PioStateMachine[] StateMachines;
        public long Size { get { return 0x1000; } }
        public GPIO[] IRQs { get; private set; }
        public GPIO IRQ0 => IRQs[0];
        public GPIO IRQ1 => IRQs[1];
        private RP2040GPIO gpio;
        private ushort[] Instructions;
        public override void Reset()
        {
        }

        public void DefineRegisters()
        {
            for (int i = 0; i < StateMachines.Length; ++i)
            {
                int key = i;
                var reg = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write,
                        writeCallback: (_, value) => { StateMachines[key].PushFifo((uint)value); },
                    name: "TXF" + i);
                RegistersCollection.AddRegister(0x10 + i * 0x4, reg);
            }

            for (int i = 0; i < StateMachines.Length; ++i)
            {
                int key = i;
                var reg = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read,
                        valueProviderCallback: _ => StateMachines[key].PopFifo(),
                    name: "RXF" + i);
                RegistersCollection.AddRegister(0x20 + i * 0x4, reg);
            }

            for (int i = 0; i < StateMachines.Length; ++i)
            {
                int key = i;
                var reg = new DoubleWordRegister(this)
                    .WithValueField(0, 4,
                        valueProviderCallback: _ => StateMachines[key].Status,
                        writeCallback: (_, value) => StateMachines[key].Status = (uint)value,
                        name: "SM" + key + "_EXECCTRL_STATUS_N")
                    .WithFlag(4,
                        valueProviderCallback: _ => StateMachines[key].StatusSelect,
                        writeCallback: (_, value) => StateMachines[key].StatusSelect = (bool)value,
                        name: "SM" + key + "_EXECCTRL_STATUS_SELECT")
                    .WithReservedBits(5, 2)
                    .WithValueField(7, 5,
                        valueProviderCallback: _ => StateMachines[key].WrapBottom,
                        writeCallback: (_, value) => StateMachines[key].WrapBottom = (uint)value,
                        name: "SM" + key + "_EXECCTRL_WRAP_BOTTOM")
                    .WithValueField(12, 5,
                        valueProviderCallback: _ => StateMachines[key].WrapTop,
                        writeCallback: (_, value) => StateMachines[key].WrapTop = (uint)value,
                        name: "SM" + key + "_EXECCTRL_WRAP_TOP")
                    .WithFlag(17,
                        valueProviderCallback: _ => StateMachines[key].OutSticky,
                        writeCallback: (_, value) => StateMachines[key].OutSticky = (bool)value,
                        name: "SM" + key + "_EXECCTRL_OUT_STICKY")
                    .WithFlag(18,
                        valueProviderCallback: _ => StateMachines[key].InlineOutEnable,
                        writeCallback: (_, value) => StateMachines[key].InlineOutEnable = (bool)value)
                    .WithValueField(19, 5,
                        valueProviderCallback: _ => StateMachines[key].OutEnableSelect,
                        writeCallback: (_, value) => StateMachines[key].OutEnableSelect = (uint)value)
                    .WithValueField(24, 5,
                        valueProviderCallback: _ => StateMachines[key].JumpPin,
                        writeCallback: (_, value) => StateMachines[key].JumpPin = (uint)value)
                    .WithFlag(29,
                        valueProviderCallback: _ => StateMachines[key].SidePinDirection,
                        writeCallback: (_, value) => StateMachines[key].SidePinDirection = (bool)value)
                    .WithFlag(30,
                        valueProviderCallback: _ => StateMachines[key].SideEnable,
                        writeCallback: (_, value) => StateMachines[key].SideEnable = (bool)value)
                    .WithFlag(31, FieldMode.Read,
                        valueProviderCallback: _ => { return false; });

                RegistersCollection.AddRegister(0x0cc + i * 0x18, reg);
            }



            for (int i = 0; i < StateMachines.Length; ++i)
            {
                int key = i;
                var reg = new DoubleWordRegister(this)
                    .WithReservedBits(0, 16)
                    .WithFlag(16,
                        valueProviderCallback: _ => StateMachines[key].AutoPush,
                        writeCallback: (_, value) => StateMachines[key].AutoPush = (bool)value,
                        name: "SM" + key + "_SHIFTCTRL_AUTOPUSH")
                    .WithFlag(17,
                        valueProviderCallback: _ => StateMachines[key].AutoPull,
                        writeCallback: (_, value) => StateMachines[key].AutoPull = (bool)value,
                        name: "SM" + key + "_SHIFTCTRL_AUTOPULL")
                    .WithFlag(18,
                        valueProviderCallback: _ => StateMachines[key].InShiftDirection,
                        writeCallback: (_, value) => StateMachines[key].InShiftDirection = (bool)value,
                        name: "SM" + key + "_SHIFTCTRL_IN_SHIFTDIR")
                    .WithFlag(19,
                        valueProviderCallback: _ => StateMachines[key].OutShiftDirection,
                        writeCallback: (_, value) => StateMachines[key].OutShiftDirection = (bool)value,
                        name: "SM" + key + "_SHIFTCTRL_OUT_SHIFTDIR")
                    .WithValueField(20, 5,
                        valueProviderCallback: _ => StateMachines[key].PushThreshold == 32 ? 0 : StateMachines[key].PushThreshold,
                        writeCallback: (_, value) =>
                        {
                            StateMachines[key].PushThreshold = (uint)value;
                            if (value == 0)
                            {
                                StateMachines[key].PushThreshold = 32;
                            }
                        },
                        name: "SM" + key + "_SHIFTCTRL_PUSH_THRESH")
                    .WithValueField(25, 5,
                        valueProviderCallback: _ => StateMachines[key].PullThreshold == 32 ? 0 : StateMachines[key].PullThreshold,
                        writeCallback: (_, value) =>
                        {
                            StateMachines[key].PullThreshold = (uint)value;
                            if (value == 0)
                            {
                                StateMachines[key].PullThreshold = 32;
                            }
                        },
                        name: "SM" + key + "_SHIFTCTRL_PULL_THRESH")
                    .WithFlag(30, writeCallback: (_, value) => StateMachines[key].JoinTxFifo((bool)value),
                        valueProviderCallback: _ => StateMachines[key].IsTxFifoJoined())
                    .WithFlag(31, writeCallback: (_, value) => StateMachines[key].JoinRxFifo((bool)value),
                        valueProviderCallback: _ => StateMachines[key].IsRxFifoJoined());

                RegistersCollection.AddRegister(0x0d0 + i * 0x18, reg);
            }

            for (int i = 0; i < StateMachines.Length; ++i)
            {
                int key = i;
                var reg = new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                        {
                            StateMachines[key].ExecuteInstruction((ushort)value);
                        },
                        valueProviderCallback: _ =>
                        {
                            return StateMachines[key].GetCurrentInstruction();
                        },
                    name: "SM" + i + "_INSTR");
                RegistersCollection.AddRegister(0x0d8 + i * 0x18, reg);
            }

            for (int i = 0; i < StateMachines.Length; ++i)
            {
                int key = i;
                var reg = new DoubleWordRegister(this)
                    .WithValueField(0, 5, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                            StateMachines[key].OutBase = (int)value,
                        valueProviderCallback: _ =>
                            (uint)StateMachines[key].OutBase,
                    name: "SM" + i + "_PINCTRL_OUTBASE")
                    .WithValueField(5, 5, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                            StateMachines[key].SetBase = (int)value,
                        valueProviderCallback: _ =>
                            (uint)StateMachines[key].SetBase,
                    name: "SM" + i + "_PINCTRL_SETBASE")
                    .WithValueField(10, 5, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                            StateMachines[key].SidesetBase = (int)value,
                        valueProviderCallback: _ =>
                            (uint)StateMachines[key].SidesetBase,
                    name: "SM" + i + "_PINCTRL_SIDESETBASE")
                    .WithValueField(15, 5, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                            StateMachines[key].InBase = (int)value,
                        valueProviderCallback: _ =>
                            (uint)StateMachines[key].InBase,
                    name: "SM" + i + "_PINCTRL_INBASE")
                    .WithValueField(20, 6, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                            StateMachines[key].OutCount = (int)value,
                        valueProviderCallback: _ =>
                            (uint)StateMachines[key].OutCount,
                    name: "SM" + i + "_PINCTRL_OUTCOUNT")
                    .WithValueField(26, 3, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                            StateMachines[key].SetCount = (int)value,
                        valueProviderCallback: _ =>
                            (uint)StateMachines[key].SetCount,
                    name: "SM" + i + "_PINCTRL_SETCOUNT")
                    .WithValueField(29, 3, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                            StateMachines[key].SidesetCount = (int)value,
                        valueProviderCallback: _ =>
                            (uint)StateMachines[key].SidesetCount,
                    name: "SM" + i + "_PINCTRL_SIDESETCOUNT");

                RegistersCollection.AddRegister(0x0dc + i * 0x18, reg);
            }


            Registers.CTRL.Define(this)
                .WithValueField(0, 4, FieldMode.Read | FieldMode.Write,
                    writeCallback: (_, value) =>
                    {
                        for (int i = 0; i < StateMachines.Length; ++i)
                        {
                            if (((1ul << i) & (ulong)value) != 0ul)
                            {
                                StateMachines[i].Enable();
                            }
                        }
                    },
                    valueProviderCallback: _ =>
                    {
                        ulong enabledStateMachines = 0;
                        for (int i = 0; i < StateMachines.Length; ++i)
                        {
                            enabledStateMachines |= Convert.ToUInt32(StateMachines[i].Enabled) << i;
                        }
                        return enabledStateMachines;
                    },
                    name: "CTRL");
            for (int i = 0; i < Instructions.Length; ++i)
            {
                int key = i;
                var reg = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write,
                        writeCallback: (_, value) =>
                        {
                            Instructions[key] = (ushort)value;
                        },
                    name: "INSTR_MEM" + i);
                RegistersCollection.AddRegister(0x048 + i * 4, reg);
            }

            Registers.FSTAT.Define(this)
                .WithValueField(0, 4, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        ulong ret = 0;
                        for (int i = 0; i < StateMachines.Length; ++i)
                        {
                            if (StateMachines[i].FullRxFifo())
                            {
                                ret |= (1ul << i);
                            }
                        }
                        return ret;
                    }, name: "RXFULL")
                .WithReservedBits(4, 4)
                .WithValueField(8, 4, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        ulong ret = 0;
                        for (int i = 0; i < StateMachines.Length; ++i)
                        {
                            if (StateMachines[i].EmptyRxFifo())
                            {
                                ret |= (1ul << i);
                            }
                        }
                        return ret;
                    }, name: "RXEMPTY")
                .WithReservedBits(12, 4)
                .WithValueField(16, 4, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        ulong ret = 0;
                        for (int i = 0; i < StateMachines.Length; ++i)
                        {
                            if (StateMachines[i].FullTxFifo())
                            {
                                ret |= (1ul << i);
                            }
                        }
                        return ret;
                    }, name: "TXFULL")
                .WithReservedBits(20, 4)
                .WithValueField(24, 4, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        ulong ret = 0;
                        for (int i = 0; i < StateMachines.Length; ++i)
                        {
                            if (StateMachines[i].EmptyTxFifo())
                            {
                                ret |= (1ul << i);
                            }
                        }
                        return ret;
                    }, name: "TXEMPTY")
                .WithReservedBits(28, 4);

        }
    }
}
