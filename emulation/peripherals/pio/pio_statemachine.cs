using Antmicro.Renode.Core;
using System;
using Antmicro.Renode.Logging;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Peripherals.GPIOPort;
using Antmicro.Renode.Peripherals.Miscellaneous.RP2040PIORegisters;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class PioStateMachine
    {
        public PioStateMachine(IMachine machine, ushort[] program, int id, RP2040GPIO gpio, Action<LogLevel, string> log)
        {
            this._gpio = gpio;
            this._log = log;
            this._program = program;
            this._immediateInstruction = null;
            this._machine = machine;
            this.ClockDivider = new StateMachineClockDivider();
            this.ExecutionControl = new StateMachineExecutionControl();
            this.ShiftControl = new StateMachineShiftControl(Log);
           
            this.Enabled = false;
            this.Id = id;
        
            long frequency = machine.ClockSource.GetAllClockEntries().First().Frequency;
            this._executionThread = machine.ObtainManagedThread(Step, (uint)frequency, "RP2040PIO_SM" + id);
        }

        public void SetClockDivider(int Integral, int Fractal)
        {
            this._executionThread.Stop();
            ClockDivider.SetDivider(Integral, Fractal);
            long frequency = _machine.ClockSource.GetAllClockEntries().First().Frequency;
            this._executionThread.Frequency = (uint)ClockDivider.CalculateTargetFrequency(frequency);
            this._executionThread.Start();
        }

        // true if finished
        private bool ProcessDelaySideSet(int data)
        {
            if (this._delay != null)
            {
                --this.delay.Value;
                if (this._delay.Value <= 0)
                {
                    this._delay = null;
                    return true;
                }

                return false;
            }

            int delayBits = (5 - PinControl.SideSetCount);
            int delayMask = (1 << delayBits) - 1;
            int delay = data & delayMask;

            if (delay != 0)
            {
                this._delay = delay;
                return false;
            }

            if (PinControl.SideSetCount > 0)
            {
                if (ExecutionControl.SidePinDirection)
                {
                    int sideSetMask = (1 << PinControl.SideSetCount) - 1;
                    int sideSet = (data >> delayBits) & sideSetMask;
                    this._gpio.SetPinDirectionBitset()
                }
                else 
                {
                    this._gpio.SetGpioBitset(sideSet << PinControl.SideSetBase);
                }
            }

            return true;
        }



        private void Log(LogLevel level, string message)
        {
            this._log(level, "SM" + Id + ": " + message);
        }

        public ushort GetCurrentInstruction()
        {
            var instruction = _program[_programCounter];
            if (_immediateInstruction.HasValue)
            {
                instruction = _immediateInstruction.Value;
                _immediateInstruction = null;
            }
            return instruction;
        }

        protected void Step()
        {
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
                case PioDecodedInstruction.Opcode.Out:
                    {
                        if (ProcessOut((ushort)cmd.ImmediateData))
                        {
                            IncrementProgramCounter();
                        }
                        return;
                    }
                case PioDecodedInstruction.Opcode.Set:
                    {
                        ProcessSet((ushort)cmd.ImmediateData);
                        IncrementProgramCounter();
                        return;
                    }
                default:
                    {
                        break;
                    }
            }


        }




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

        private void ProcessSet(ushort data)
        {
            ushort destination = (ushort)((data >> 5) & 0x7);
            ushort immediateData = (ushort)(data & 0x1f);
            switch (destination)
            {
                case 0:
                    {
                        gpio.SetGpioBitset((ulong)((immediateData & ((1 << SetCount) - 1)) << SetBase));
                        return;
                    }
                case 1:
                    {
                        x = immediateData;
                        return;
                    }
                case 2:
                    {
                        y = immediateData;
                        return;
                    }
                case 3:
                    {
                        return;
                    }
                case 4:
                    {
                        // pindirs not yet supported 
                        return;
                    }
            }
        }

        private bool ProcessOut(ushort data)
        {
            ushort destination = (ushort)((data >> 5) & 0x7);
            ushort bitcount = (ushort)(data & 0x1f);
            if (bitcount == 0) bitcount = 32;

            uint output = 0;
            // true == right shift
            if (OutShiftDirection)
            {
                output = (uint)(OutputShiftRegister & ((1 << bitcount) - 1));
                OutputShiftRegister >>= bitcount;
            }
            else
            {
                output = (uint)((OutputShiftRegister >> (32 - bitcount)) & ((1 << bitcount) - 1));
                OutputShiftRegister <<= bitcount;
            }

            switch (destination)
            {
                case 0:
                    {
                        gpio.SetGpioBitset((ulong)((output & ((1 << OutCount) - 1)) << OutBase));
                        return true;
                    }
                case 1:
                    {
                        x = output;
                        return true;
                    }
                case 2:
                    {
                        y = output;
                        return true;
                    }
                case 3:
                    {
                        return true;
                    }
                case 4:
                    {
                        // pindirs not supported yet
                        return true;
                    }
                case 5:
                    {
                        pc = (ushort)output;
                        return false;
                    }
                case 6:
                    {
                        InputShiftRegister = output;
                        InputShiftRegisterCounter = bitcount;
                        return true;
                    }
                case 7:
                    {
                        immediateInstruction = (ushort)output;
                        return false;
                    }
            }
            return true;

        }

        public void Enable()
        {
            Enabled = true;
            stopped = false;
            _executionThread.Start();
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
        private uint x;
        private uint y;


        private Action<LogLevel, string> _log;
        private RP2040GPIO _gpio;
        private IMachine _machine;
        private IManagedThread _executionThread;
        
        private ushort[] _program;
        private ushort? _immediateInstruction;
        private ushort _programCounter;

        private int? _delay;



        public StateMachineClockDivider ClockDivider { get; private set; }
        public StateMachineExecutionControl ExecutionControl { get; set; }
        public StateMachineShiftControl ShiftControl { get; set; }
        public StateMachinePinControl PinControl { get; set; }
        public bool Enabled { get; private set; }
        public int Id { get; private set; }
    }

}
