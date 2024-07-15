using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using System;
using Antmicro.Renode.Time;
using Xwt;
using Antmicro.Renode.Logging;
using System.Collections.Generic;
using System.Linq;


namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class PioStateMachine
    {
        public PioStateMachine(IMachine machine, ushort[] program, int id)
        {
            long frequency = machine.ClockSource.GetAllClockEntries().First().Frequency;

            TxFifo = new Queue<uint>();
            RxFifo = new Queue<uint>();

            this.Enabled = false;
            executionThread = machine.ObtainManagedThread(Step, (uint)frequency, "piosm");
            this.program = program;
            pc = 0;
            OutputShiftRegisterCounter = 0xfffffff;
            TxFifoSize = 4;
            RxFifoSize = 4;
            Id = id;
            WrapTop = 31;
            WrapBottom = 0;
        }

        private IManagedThread executionThread;
        private ushort[] program;
        public bool Enabled { get; private set; }
        public int OutBase { get; set; }
        public int SetBase { get; set; }
        public int SidesetBase { get; set; }
        public int InBase { get; set; }
        public int OutCount { get; set; }
        public int SetCount { get; set; }
        public int SidesetCount { get; set; }
        public uint OutputShiftRegister { get; private set; }
        public uint InputShiftRegister { get; private set; }
        public bool AutoPush { get; set; }
        public bool AutoPull { get; set; }
        public bool InShiftDirection { get; set; }
        public bool OutShiftDirection { get; set; }
        public uint PushThreshold { get; set; }
        public uint PullThreshold { get; set; }
        public uint Status { get; set; }
        public bool StatusSelect { get; set; }
        public uint WrapBottom { get; set; }
        public uint WrapTop { get; set; }
        public bool OutSticky { get; set; }
        public bool InlineOutEnable { get; set; }
        public uint OutEnableSelect { get; set; }
        public uint JumpPin { get; set; }
        public bool SidePinDirection { get; set; }
        public bool SideEnable { get; set; }

        private int Id;
        private uint OutputShiftRegisterCounter;
        private uint InputShiftRegisterCounter;
        public void JoinTxFifo(bool join)
        {
            if (join)
            {
                Logger.Log(LogLevel.Error, "Joining TX" + Id);
                TxFifoSize = 8;
                RxFifoSize = 0;
            }
        }

        public void JoinRxFifo(bool join)
        {
            if (join)
            {
                Logger.Log(LogLevel.Error, "Joining RX" + Id);
                RxFifoSize = 8;
                TxFifoSize = 0;
            }
        }

        public bool IsTxFifoJoined()
        {
            return TxFifoSize == 8;
        }

        public bool IsRxFifoJoined()
        {
            return RxFifoSize == 8;
        }

        private int TxFifoSize;
        private int RxFifoSize;

        private Queue<uint> TxFifo;
        private Queue<uint> RxFifo;

        public bool FullRxFifo()
        {
            return RxFifo.Count() == RxFifoSize;
        }

        public bool EmptyRxFifo()
        {
            return RxFifo.Count() == 0;
        }

        public bool FullTxFifo()
        {
            // Logger.Log(LogLevel.Error, "Fifo TX size: " + TxFifo.Count() + ",l: " + TxFifoSize);
            return TxFifo.Count() == TxFifoSize;
        }

        public bool EmptyTxFifo()
        {
            return TxFifo.Count() == 0;
        }

        private int delay;
        private bool jumpCondition(ushort condition)
        {
            switch (condition)
            {
                case 0: return true;
                case 1: return x == 0;
                case 2: return x-- != 0;
                case 3: return y == 0;
                case 4: return y-- != 0;
                case 5: return x != y;
                case 6: return true; // pin not supported yet
                case 7: return OutputShiftRegisterCounter < PullThreshold;
                default: return true;
            }
        }

        private void processJump(ushort condition, ushort address)
        {
            if (jumpCondition(condition))
            {
                pc = address;
            }
            else
            {
                IncrementProgramCounter();
            }
        }

        public void PushFifo(uint value)
        {
            if (TxFifo.Count() != TxFifoSize)
            {
                TxFifo.Enqueue(value);
            }
        }

        public uint PopFifo()
        {
            if (RxFifo.Count() != 0)
            {
                return RxFifo.Dequeue();
            }
            return 0;
        }

        private void IncrementProgramCounter()
        {
            if (pc == WrapTop)
            {
                pc = (ushort)WrapBottom;
            }
            else
            {
                pc += 1;
            }
        }
        public ushort GetCurrentInstruction()
        {
            var instruction = program[pc];
            if (immediateInstruction.HasValue)
            {
                Logger.Log(LogLevel.Error, "Execute immediate: " + immediateInstruction.Value);
                instruction = immediateInstruction.Value;
                immediateInstruction = null;
            }
            return instruction;
        }

        private bool ProcessDelaySideSet(int data)
        {
            if (this.delay-- == 1)
            {
                return false;
            }

            if (this.delay-- > 0)
            {
                return true;
            }
            int delayBits = 5 - SidesetCount;
            int delayMask = (1 << delayBits) - 1;
            int delay = data & delayMask;

            // TODO: side-set implementation
            if (delay != 0)
            {
                this.delay = delay;
                return true;
            }
            return false;
        }

        private uint GetFromSource(ushort source)
        {
            switch (source)
            {
                case 0: return 0; // pins not supported yet 
                case 1: return x;
                case 2: return y;
                case 3: return 0;
                case 4: return 0;
                case 5: return Status;
                case 6: return InputShiftRegister;
                case 7: return OutputShiftRegister;
            }
            return 0;
        }

        uint BitReverse(uint data)
        {
            uint o = 0;
            for (int i = 0; i < 32; ++i)
            {
                if (Convert.ToBoolean(data & (1 << i)))
                {
                    o |= (uint)(1 << (31 - i));
                }
            }
            return o;
        }
        private bool ProcessMov(ushort immediateData)
        {
            ushort destination = (ushort)((immediateData >> 5) & 0x7);
            ushort source = (ushort)(immediateData & 0x7);
            ushort operation = (ushort)((immediateData >> 3) & 0x03);

            uint from = GetFromSource(source);


            if (operation == 1)
            {
                from = ~from;
            }
            else if (operation == 2)
            {
                from = BitReverse(from);
            }

            switch (destination)
            {
                case 0:
                    {
                        // TODO pins support
                        return true;
                    }
                case 1:
                    {
                        x = from;
                        return true;
                    }
                case 2:
                    {
                        y = from;
                        return true;
                    }
                case 3:
                    {
                        return true;
                    }
                case 4:
                    {
                        immediateInstruction = (ushort)from;
                        return false;
                    }
                case 5:
                    {
                        pc = (ushort)from;
                        return false;
                    }
                case 6:
                    {
                        InputShiftRegister = from;
                        InputShiftRegisterCounter = 0;
                        return true;
                    }
                case 7:
                    {
                        OutputShiftRegister = from;
                        OutputShiftRegisterCounter = 0;
                        return true;
                    }
            }

            return true;
        }

        protected void Step()
        {
            if (AutoPull)
            {
                if (OutputShiftRegisterCounter >= PullThreshold && TxFifo.Count() > 0)
                {
                    OutputShiftRegisterCounter = 0;
                    OutputShiftRegister = TxFifo.Dequeue();
                    Logger.Log(LogLevel.Error, "Got data from TX FIFO");
                }
            }
            var cmd = new PioDecodedInstruction(GetCurrentInstruction());

            if (ProcessDelaySideSet((int)cmd.DelayOrSideSet))
            {
                return;
            }

            switch (cmd.OpCode)
            {
                case PioDecodedInstruction.Opcode.Jmp:
                    {
                        ushort address = (ushort)(cmd.ImmediateData & 0x1f);
                        ushort condition = (ushort)((cmd.ImmediateData >> 5) & 0x07);
                        processJump(condition, address);
                        return;
                    }
                case PioDecodedInstruction.Opcode.PushPull:
                    {
                        bool block = Convert.ToBoolean((cmd.ImmediateData >> 5) & 0x1);
                        bool isPull = Convert.ToBoolean((cmd.ImmediateData >> 7) & 0x1);

                        if (!isPull)
                        {
                            bool ifFull = Convert.ToBoolean((cmd.ImmediateData >> 6) & 0x1);
                            if (ifFull && (InputShiftRegisterCounter < PushThreshold))
                            {
                                return;
                            }
                            if (!FullRxFifo())
                            {
                                RxFifo.Enqueue(InputShiftRegister);
                                IncrementProgramCounter();
                                return;
                            }
                            else if (!block)
                            {
                                IncrementProgramCounter();
                                return;
                            }
                            return;
                        }
                        else
                        {
                            bool ifEmpty = Convert.ToBoolean((cmd.ImmediateData >> 6) & 0x1);
                            if (ifEmpty && OutputShiftRegister < PullThreshold)
                            {
                                return;
                            }
                            if (!EmptyTxFifo())
                            {
                                OutputShiftRegister = TxFifo.Dequeue();
                                OutputShiftRegisterCounter = 0;
                                IncrementProgramCounter();
                                return;
                            }
                            else
                            {
                                if (!block)
                                {
                                    OutputShiftRegister = x;
                                    OutputShiftRegisterCounter = 0;
                                    IncrementProgramCounter();
                                    return;
                                }
                                return;
                            }
                        }
                    }
                case PioDecodedInstruction.Opcode.Mov:
                    {
                        if (ProcessMov((ushort)cmd.ImmediateData))
                        {
                            IncrementProgramCounter();
                        }
                        return;
                    }
                default:
                    {
                        Logger.Log(LogLevel.Error, "Unknown: " + cmd.OpCode);
                        break;
                    }
            }

            Logger.Log(LogLevel.Error, "D/SS: " + cmd.DelayOrSideSet + ", data: " + cmd.ImmediateData);

        }

        public void Enable()
        {
            Enabled = true;
            stopped = false;
            executionThread.Start();
        }

        public void Stop()
        {
            stopped = true;
        }

        public void Resume()
        {
            stopped = false;
        }

        public void SetProgramCounter(ushort pc)
        {
            this.pc = pc;
        }

        public void ExecuteInstruction(ushort instruction)
        {
            immediateInstruction = instruction;
        }
        private ushort? immediateInstruction;
        private ushort pc;
        private bool stopped;
        private uint x;
        private uint y;
    }


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

        public RP2040PIO(Machine machine) : base(machine)
        {
            IRQs = new GPIO[2];
            for (int i = 0; i < IRQs.Length; ++i)
            {
                IRQs[i] = new GPIO();
            }
            Instructions = new ushort[32];
            StateMachines = new PioStateMachine[4];
            for (int i = 0; i < StateMachines.Length; ++i)
            {
                StateMachines[i] = new PioStateMachine(machine, Instructions, i);
            }

            DefineRegisters();
            Reset();
        }

        private PioStateMachine[] StateMachines;
        public long Size { get { return 0x1000; } }
        public GPIO[] IRQs { get; private set; }
        public GPIO IRQ0 => IRQs[0];
        public GPIO IRQ1 => IRQs[1];
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
                    .WithFlag(31, writeCallback: (_, value) => Logger.Log(LogLevel.Error, "Joining + " + value) /*StateMachines[key].JoinRxFifo((bool)value)*/,
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
