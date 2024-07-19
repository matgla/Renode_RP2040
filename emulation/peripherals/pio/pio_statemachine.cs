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
            this._delayCycles = 0;
            this._delayCounter = 0;
            this._sideSetDone = false;
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
        private void ProcessSideSet(int data)
        {
            int delayBits = (5 - PinControl.SideSetCount);

            if (!_sideSetDone)
            {
                if (PinControl.SideSetCount > 0)
                {
                    int sideSetMask = (1 << PinControl.SideSetCount) - 1;
                    int sideSet = (data >> delayBits) & sideSetMask;
                    int gpioBitset = (sideSet << PinControl.SideSetBase);
                    bool enabled = true;
                    if (ExecutionControl.SideEnabled)
                    {
                        // use enable pin 
                        if ((data & (1 << 5)) != 1)
                        {
                            enabled = false;
                        }
                    }

                    if (ExecutionControl.SidePinDirection && enabled) // pindirs
                    {
                        this._gpio.SetPinDirectionBitset((ulong)gpioBitset);
                    }
                    else if ((ExecutionControl.SidePinDirection == false) && enabled) // GPIO levels
                    {
                        this._gpio.SetGpioBitset((ulong)gpioBitset);
                    }
                }
                _sideSetDone = true;
            }
        }

        private void ScheduleDelayCycles(ushort data)
        {
            int delayBits = (5 - PinControl.SideSetCount);
            int delayMask = (1 << delayBits) - 1;
            int delay = data & delayMask;

            if (delay != 0)
            {
                this._delayCounter = 0;
                this._delayCycles = delay;
            }
        }

        private bool ProcessDelay()
        {
            if (IsStalled)
            {
                // process command
                return true;
            }
            ++_delayCounter;
            if (_delayCounter > _delayCycles)
            {
                return true;
            }

            return false;
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


        private bool JumpConditionMet(byte condition)
        {
            switch (condition)
            {
                case 0: return true;
                case 1: return _scratchX == 0;
                case 2: return _scratchX-- != 0;
                case 3: return _scratchY == 0;
                case 4: return _scratchY-- != 0;
                case 5: return _scratchX != _scratchY;
                case 6: return _gpio.GetGpioState(ExecutionControl.JumpPin); // pin not supported yet
                case 7: return ShiftControl.OutputShiftRegisterCounter < ShiftControl.PullThreshold;
                default: return true;
            }

        }

        private void IncrementProgramCounter()
        {
            if (_programCounter == ExecutionControl.WrapTop)
            {
                _programCounter = ExecutionControl.WrapBottom;
            }
            else
            {
                _programCounter += 1;
            }
        }

        private bool ProcessJump(ushort data)
        {
            byte condition = (byte)((data >> 5) & 0x7);
            byte address = (byte)(data & 0x1f);
            if (JumpConditionMet(condition))
            {
                _programCounter = address;
                return true;
            }
            else
            {
                IncrementProgramCounter();
                return true;
            }
        }

        // returns true when done, info for delay to start processing
        private bool ProcessWait(ushort data)
        {
            bool polarity = ((1 << 7) & data) == 1;
            byte source = (byte)((data >> 5) & 0x3);
            byte index = (byte)(data & 0x1f);

            switch (source)
            {
                case 0:
                    {
                        bool state = _gpio.GetGpioState(index);
                        if (state == polarity)
                        {
                            return true;
                        }
                        return false;
                    }
                case 1:
                    {


                    }
                case 2:
                    {
                        Log(LogLevel.Error, "Unsupported WAIT IRQ");
                        return false;
                        // support for IRQ
                    }
            }
            return true;
        }

        private bool ProcessPush(ushort data)
        {
            bool ifFull = (data & (1 << 6)) == 1;
            bool block = (data & (1 << 5)) == 1;

            if (ifFull)
            {
                if (ShiftControl.InputShiftRegisterCounter >= ShiftControl.PushThreshold)
                {
                    IncrementProgramCounter();
                    return true;
                }
            }

            if (ShiftControl.RxFifoFull())
            {
                if (block)
                {
                    return false;
                }

            }
            else
            {
                ShiftControl.PushInputShiftRegister();
            }
            IncrementProgramCounter();
            return true;
        }

        private bool ProcessPull(ushort data)
        {
            bool ifEmpty = (data & (1 << 6)) == 1;
            bool block = (data & (1 << 5)) == 1;

            if (ifEmpty)
            {
                if (ShiftControl.OutputShiftRegisterCounter >= ShiftControl.PullThreshold)
                {
                    IncrementProgramCounter();
                    return true;
                }
            }

            if (ShiftControl.TxFifoEmpty())
            {
                if (block)
                {
                    return false;
                }
                ShiftControl.LoadOutputShiftRegister((int)_scratchX);
            }
            else
            {
                ShiftControl.LoadOutputShiftRegister();
            }
            IncrementProgramCounter();
            return true;
        }

        private bool ProcessPushPull(ushort data)
        {
            bool isPush = (data & (1 << 7)) == 0;
            if (isPush)
            {
                return ProcessPush(data);
            }
            else
            {
                return ProcessPull(data);
            }
        }

        protected void Step()
        {
            if (!ProcessDelay())
            {
                return;
            }

            var cmd = new PioDecodedInstruction(GetCurrentInstruction());
            ProcessSideSet((int)cmd.DelayOrSideSet);

            bool finished = false;
            switch (cmd.OpCode)
            {
                case PioDecodedInstruction.Opcode.Jmp:
                    {

                        finished = ProcessJump((ushort)cmd.ImmediateData);
                        break;
                    }

                case PioDecodedInstruction.Opcode.Wait:
                    {
                        finished = ProcessWait((ushort)cmd.ImmediateData);
                        break;
                    }
                case PioDecodedInstruction.Opcode.PushPull:
                    {
                        finished = ProcessPushPull((ushort)cmd.ImmediateData);
                        break;
                    }
                //  case PioDecodedInstruction.Opcode.Mov:
                //      {
                //          if (ProcessMov((ushort)cmd.ImmediateData))
                //          {
                //              IncrementProgramCounter();
                //          }
                //          return;
                //      }
                //  case PioDecodedInstruction.Opcode.Out:
                //      {
                //          if (ProcessOut((ushort)cmd.ImmediateData))
                //          {
                //              IncrementProgramCounter();
                //          }
                //          return;
                //      }
                //  case PioDecodedInstruction.Opcode.Set:
                //      {
                //          ProcessSet((ushort)cmd.ImmediateData);
                //          IncrementProgramCounter();
                //          return;
                //      }
                default:
                    {
                        break;
                    }
            }

            if (finished)
            {
                ScheduleDelayCycles((ushort)cmd.DelayOrSideSet);
                _sideSetDone = false;
            }
            else
            {
                IsStalled = false;
            }


        }



        private uint GetFromSource(ushort source)
        {
            //            switch (source)
            //            {
            //                case 0: return 0; // pins not supported yet 
            //                case 1: return x;
            //                case 2: return y;
            //                case 3: return 0;
            //                case 4: return 0;
            //                case 5: return Status;
            //                case 6: return InputShiftRegister;
            //                case 7: return OutputShiftRegister;
            //            }
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
            //            ushort destination = (ushort)((immediateData >> 5) & 0x7);
            //            ushort source = (ushort)(immediateData & 0x7);
            //            ushort operation = (ushort)((immediateData >> 3) & 0x03);
            //
            //            uint from = GetFromSource(source);
            //
            //
            //            if (operation == 1)
            //            {
            //                from = ~from;
            //            }
            //            else if (operation == 2)
            //            {
            //                from = BitReverse(from);
            //            }
            //
            //            switch (destination)
            //            {
            //                case 0:
            //                    {
            //                        // TODO pins support
            //                        return true;
            //                    }
            //                case 1:
            //                    {
            //                        x = from;
            //                        return true;
            //                    }
            //                case 2:
            //                    {
            //                        y = from;
            //                        return true;
            //                    }
            //                case 3:
            //                    {
            //                        return true;
            //                    }
            //                case 4:
            //                    {
            //                        immediateInstruction = (ushort)from;
            //                        return false;
            //                    }
            //                case 5:
            //                    {
            //                        pc = (ushort)from;
            //                        return false;
            //                    }
            //                case 6:
            //                    {
            //                        InputShiftRegister = from;
            //                        InputShiftRegisterCounter = 0;
            //                        return true;
            //                    }
            //                case 7:
            //                    {
            //                        OutputShiftRegister = from;
            //                        OutputShiftRegisterCounter = 0;
            //                        return true;
            //                    }
            //            }
            //
            return true;
        }

        private void ProcessSet(ushort data)
        {
            //            ushort destination = (ushort)((data >> 5) & 0x7);
            //            ushort immediateData = (ushort)(data & 0x1f);
            //            switch (destination)
            //            {
            //                case 0:
            //                    {
            //                        gpio.SetGpioBitset((ulong)((immediateData & ((1 << SetCount) - 1)) << SetBase));
            //                        return;
            //                    }
            //                case 1:
            //                    {
            //                        x = immediateData;
            //                        return;
            //                    }
            //                case 2:
            //                    {
            //                        y = immediateData;
            //                        return;
            //                    }
            //                case 3:
            //                    {
            //                        return;
            //                    }
            //                case 4:
            //                    {
            //                        // pindirs not yet supported 
            //                        return;
            //                    }
            //            }
        }

        private bool ProcessOut(ushort data)
        {
            //           ushort destination = (ushort)((data >> 5) & 0x7);
            //           ushort bitcount = (ushort)(data & 0x1f);
            //           if (bitcount == 0) bitcount = 32;

            //           uint output = 0;
            //           // true == right shift
            //           if (OutShiftDirection)
            //           {
            //               output = (uint)(OutputShiftRegister & ((1 << bitcount) - 1));
            //               OutputShiftRegister >>= bitcount;
            //           }
            //           else
            //           {
            //               output = (uint)((OutputShiftRegister >> (32 - bitcount)) & ((1 << bitcount) - 1));
            //               OutputShiftRegister <<= bitcount;
            //           }

            //           switch (destination)
            //           {
            //               case 0:
            //                   {
            //                       gpio.SetGpioBitset((ulong)((output & ((1 << OutCount) - 1)) << OutBase));
            //                       return true;
            //                   }
            //               case 1:
            //                   {
            //                       x = output;
            //                       return true;
            //                   }
            //               case 2:
            //                   {
            //                       y = output;
            //                       return true;
            //                   }
            //               case 3:
            //                   {
            //                       return true;
            //                   }
            //               case 4:
            //                   {
            //                       // pindirs not supported yet
            //                       return true;
            //                   }
            //               case 5:
            //                   {
            //                       pc = (ushort)output;
            //                       return false;
            //                   }
            //               case 6:
            //                   {
            //                       InputShiftRegister = output;
            //                       InputShiftRegisterCounter = bitcount;
            //                       return true;
            //                   }
            //               case 7:
            //                   {
            //                       immediateInstruction = (ushort)output;
            //                       return false;
            //                   }
            //           }
            return true;

        }

        public void Enable()
        {
            Enabled = true;
            _executionThread.Start();
        }

        public void Stop()
        {
            //           stopped = true;
        }

        public void Resume()
        {
            //          stopped = false;
        }

        public void SetProgramCounter(ushort pc)
        {
            //          this.pc = pc;
        }

        public void ExecuteInstruction(ushort instruction)
        {
            _immediateInstruction = instruction;
        }
        private uint _scratchX;
        private uint _scratchY;


        private Action<LogLevel, string> _log;
        private RP2040GPIO _gpio;
        private IMachine _machine;
        private IManagedThread _executionThread;

        private ushort[] _program;
        private ushort? _immediateInstruction;
        private ushort _programCounter;

        private int _delayCounter;
        private int _delayCycles;
        private bool _sideSetDone;

        public StateMachineClockDivider ClockDivider { get; private set; }
        public StateMachineExecutionControl ExecutionControl { get; set; }
        public StateMachineShiftControl ShiftControl { get; set; }
        public StateMachinePinControl PinControl { get; set; }
        public bool Enabled { get; private set; }
        public int Id { get; private set; }
        public bool IsStalled { get; private set; }
    }

}
