using Antmicro.Renode.Core;
using System;
using Antmicro.Renode.Logging;
using System.Linq;
using Antmicro.Renode.Peripherals.GPIOPort;
using Antmicro.Renode.Peripherals.Miscellaneous.RP2040PIORegisters;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class PioStateMachine
    {
        public PioStateMachine(IMachine machine, ushort[] program, int id, RP2040GPIO gpio, Action<LogLevel, string> log, bool[] irq)
        {
            this._gpio = gpio;
            this._log = log;
            this._irq = irq;
            this._program = program;
            this._immediateInstruction = null;
            this._machine = machine;
            this.ClockDivider = new StateMachineClockDivider();
            this.ExecutionControl = new StateMachineExecutionControl();
            this.ShiftControl = new StateMachineShiftControl(Log);
            this.PinControl = new StateMachinePinControl();
            this._delayCycles = 0;
            this._delayCounter = 0;
            this._sideSetDone = false;
            this._ignoreDelay = false;
            this.Enabled = false;
            this.Id = id;
            this._waitForInterrupt = null;
            this._scratchY = 0;
            this._scratchX = 0;
            long frequency = machine.ClockSource.GetAllClockEntries().First().Frequency;
            log(LogLevel.Error, "freq: " + frequency);
            this._executionThread = machine.ObtainManagedThread(Step, (uint)frequency, "RP2040PIO_SM" + id);
        }

        public void SetClockDivider(int Integral, int Fractal)
        {
            this._executionThread.Stop();
            ClockDivider.SetDivider(Integral, Fractal);
            long frequency = _machine.ClockSource.GetAllClockEntries().First().Frequency;
            this._executionThread.Frequency = (uint)ClockDivider.CalculateTargetFrequency(frequency);
            this._log(LogLevel.Error, "changed freq: " + this._executionThread.Frequency);
            this._executionThread.Start();
        }

        // true if finished
        private void ProcessSideSet(uint data)
        {
            int delayBits = (5 - PinControl.SideSetCount);

            if (!_sideSetDone)
            {
                if (PinControl.SideSetCount > 0)
                {
                    uint sideSetMask = (1u << PinControl.SideSetCount) - 1;
                    uint sideSet = (data >> delayBits) & sideSetMask;
                    uint gpioBitset = RotateLeftWithWrap(sideSet, PinControl.SideSetBase, PinControl.SideSetCount);
                    uint gpioBitmap = RotateLeftWithWrap((1u << PinControl.SideSetCount) - 1, PinControl.SideSetBase, 32);
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
                        this._gpio.SetPinDirectionBitset((ulong)gpioBitset, (ulong)gpioBitmap);
                    }
                    else if ((ExecutionControl.SidePinDirection == false) && enabled) // GPIO levels
                    {
                        this._gpio.SetGpioBitset((ulong)gpioBitset, (ulong)gpioBitmap);
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
                if (_scratchX % 1000000 == 0)
                {
                    Log(LogLevel.Info, "Jump done: " + _scratchX);
                }
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
            bool polarity = ((1 << 7) & data) != 0;
            byte source = (byte)((data >> 5) & 0x3);
            byte index = (byte)(data & 0x1f);

            switch (source)
            {
                case 0:
                    {
                        bool state = _gpio.GetGpioState(index);
                        return state == polarity;
                    }
                case 1:
                    {
                        bool state = _gpio.GetGpioState((uint)((index + PinControl.InBase) % 32));
                        return state == polarity;
                    }
                case 2:
                    {
                        int id = index;
                        if ((index & (1 << 4)) != 0)
                        {
                            id = (Id + index) % 4;
                        }

                        if (_irq[id])
                        {
                            if (polarity)
                            {
                                _irq[id] = false;
                            }
                            return true;
                        }
                        return false;
                        // support for IRQ
                    }
            }
            return true;
        }

        private bool ProcessPush(ushort data)
        {
            bool ifFull = (data & (1 << 6)) != 0;
            bool block = (data & (1 << 5)) != 0;

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
            bool ifEmpty = (data & (1 << 6)) != 0;
            bool block = (data & (1 << 5)) != 0;

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
                ShiftControl.LoadOutputShiftRegister(_scratchX);
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

        private bool ProcessIn(ushort data)
        {
            byte source = (byte)((data >> 5) & 0x7);
            byte bitCount = (byte)(data & 0x1f);
            if (bitCount == 0)
            {
                bitCount = 32;
            }

            uint isrData = 0;
            switch (source)
            {
                case 0:
                    {
                        isrData = _gpio.GetGpioStateBitmap();
                        isrData = RotateRightWithWrap(isrData, PinControl.InBase, 32);
                        break;
                    }
                case 1:
                    {
                        isrData = _scratchX;
                        break;
                    }
                case 2:
                    {
                        isrData = _scratchY;
                        break;
                    }
                case 6:
                    {
                        isrData = ShiftControl.InputShiftRegister;
                        break;
                    }
                case 7:
                    {
                        isrData = ShiftControl.OutputShiftRegister;
                        break;
                    }
                default:
                    {
                        isrData = 0;
                        break;
                    }
            }

            ShiftControl.WriteInputShiftRegister(bitCount, isrData);

            IncrementProgramCounter();
            return true;
        }

        private uint RotateRightWithWrap(uint data, int pin_base, int count)
        {
            pin_base %= 32;
            uint ret = data >> pin_base;
            uint lost = (data & ((1u << pin_base) - 1));
            ulong mask = (1ul << count) - 1;
            ret |= lost << (32 - pin_base);
            return (uint)(ret & mask);
        }

        // https://godbolt.org/z/8TYG4drvf
        private uint RotateLeftWithWrap(uint data, int pin_base, int count)
        {
            pin_base %= 32;
            uint ret = data << pin_base;
            uint lost = ((data >> (32 - pin_base)) & ((1u << (32 - pin_base)) - 1));
            ulong mask = ((1ul << count) - 1) << pin_base;
            ret |= lost;
            return (uint)(ret & mask);
        }

        private bool ProcessOut(ushort data)
        {
            byte source = (byte)((data >> 5) & 0x7);
            byte bitCount = (byte)(data & 0x1f);
            if (bitCount == 0)
            {
                bitCount = 32;
            }

            uint osrData = ShiftControl.ReadOutputShiftRegister(bitCount);

            Log(LogLevel.Info, "Out with: " + source + ", count: " + bitCount + ", data: " + osrData);
            switch (source)
            {
                case 0:
                    {
                        ulong pins = RotateLeftWithWrap(osrData, PinControl.OutBase, PinControl.OutCount);
                        ulong mask = RotateLeftWithWrap((1u << PinControl.OutCount) - 1, PinControl.OutBase, 32);
                        Log(LogLevel.Info, "pins: " + pins + ", mask: " + Convert.ToString((uint)mask, 16));
                        _gpio.SetGpioBitset(pins, mask);
                        break;
                    }
                case 1:
                    {
                        _scratchX = osrData;
                        break;
                    }
                case 2:
                    {
                        _scratchY = osrData;
                        Log(LogLevel.Info, "Write to Y: " + _scratchY);
                        break;
                    }
                case 3:
                    {
                        break;
                    }
                case 4:
                    {
                        ulong pins = RotateLeftWithWrap(osrData, PinControl.OutBase, PinControl.OutCount);
                        ulong mask = RotateLeftWithWrap((1u << PinControl.OutCount) - 1, PinControl.OutBase, 32);
                        _gpio.SetPinDirectionBitset(pins, mask);
                        break;
                    }
                case 5:
                    {
                        _programCounter = (ushort)osrData;
                        return true;
                    }
                case 6:
                    {
                        ShiftControl.SetInputShiftRegister(osrData, bitCount);
                        break;
                    }
                case 7:
                    {
                        _immediateInstruction = (ushort)osrData;
                        _ignoreDelay = true;
                        return true;
                    }
                default:
                    {
                        break;
                    }
            }
            IncrementProgramCounter();

            return true;
        }

        private uint GetFromSource(ushort source)
        {
            switch (source)
            {
                case 0: return RotateRightWithWrap(_gpio.GetGpioStateBitmap(), PinControl.InBase, 32); // pins not supported yet 
                case 1: return _scratchX;
                case 2: return _scratchY;
                case 3: return 0;
                case 4: return 0;
                case 5:
                    {
                        uint data = 0;
                        if (!ExecutionControl.StatusSelect)
                        {
                            if (ShiftControl.TxFifoCount() < ExecutionControl.StatusLevel)
                            {
                                data = ~data;
                            }
                        }
                        else
                        {
                            if (ShiftControl.RxFifoCount() < ExecutionControl.StatusLevel)
                            {
                                data = ~data;
                            }
                        }
                        return data;
                    }
                case 6: return ShiftControl.InputShiftRegister;
                case 7: return ShiftControl.OutputShiftRegister;
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

            uint data = GetFromSource(source);


            if (operation == 1)
            {
                data = ~data;
            }
            else if (operation == 2)
            {
                data = BitReverse(data);
            }

            switch (destination)
            {
                case 0:
                    {
                        ulong mask = RotateLeftWithWrap((1u << PinControl.OutCount), PinControl.OutBase, 32);
                        _gpio.SetGpioBitset(RotateLeftWithWrap(data, PinControl.OutBase, PinControl.OutCount), mask);
                        break;
                    }
                case 1:
                    {
                        _scratchX = data;
                        break;
                    }
                case 2:
                    {
                        _scratchY = data;
                        break;
                    }
                case 3:
                    {
                        break;
                    }
                case 4:
                    {
                        _immediateInstruction = (ushort)data;
                        _ignoreDelay = true;
                        return true;
                    }
                case 5:
                    {
                        _programCounter = (ushort)data;
                        return true;
                    }
                case 6:
                    {
                        ShiftControl.SetInputShiftRegister(data, 0);
                        break;
                    }
                case 7:
                    {
                        ShiftControl.SetOutputShiftRegister(data, 0);
                        break;
                    }
            }
            IncrementProgramCounter();

            return true;
        }

        private bool ProcessIrq(ushort data)
        {
            if (_waitForInterrupt != null)
            {
                if (_irq[_waitForInterrupt.Value] == false)
                {
                    IncrementProgramCounter();
                    return true;
                }
                return false;
            }
            bool clear = (data & (1 << 6)) != 0;
            bool wait = (data & (1 << 5)) != 0;
            byte index = (byte)(data & 0x1f);

            int id = index;
            if ((id & (1 << 4)) != 0)
            {
                id = (Id + index) % 4;
            }

            if (clear)
            {
                _irq[id] = false;
                IncrementProgramCounter();
                return true;
            }
            else
            {
                _irq[id] = true;
                if (wait)
                {
                    _waitForInterrupt = id;
                }
            }

            return true;
        }

        private bool ProcessSet(ushort immediateData)
        {
            byte destination = (byte)((immediateData >> 5) & 0x7);
            byte data = (byte)(immediateData & 0x1f);

            switch (destination)
            {
                case 0:
                    {
                        ulong mask = RotateLeftWithWrap((1u << PinControl.SetCount) - 1, PinControl.SetBase, 32);
                        _gpio.SetGpioBitset(RotateLeftWithWrap(data, PinControl.SetBase, PinControl.SetCount), mask);
                        break;
                    }
                case 1:
                    {
                        _scratchX = data;
                        break;
                    }
                case 2:
                    {
                        _scratchY = data;
                        break;
                    }
                case 4:
                    {
                        ulong mask = RotateLeftWithWrap((1u << PinControl.SetCount) - 1, PinControl.SetBase, 32);
                        _gpio.SetPinDirectionBitset(RotateLeftWithWrap(data, PinControl.SetBase, PinControl.SetCount), mask);
                        break;
                    }
            }
            IncrementProgramCounter();
            return true;
        }

        protected void Step()
        {
            if (!ProcessDelay())
            {
                return;
            }

            var cmd = new PioDecodedInstruction(GetCurrentInstruction());
            ProcessSideSet((byte)cmd.DelayOrSideSet);

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
                case PioDecodedInstruction.Opcode.In:
                    {
                        finished = ProcessIn((ushort)cmd.ImmediateData);
                        break;
                    }
                case PioDecodedInstruction.Opcode.Out:
                    {
                        finished = ProcessOut((ushort)cmd.ImmediateData);
                        break;
                    }
                case PioDecodedInstruction.Opcode.PushPull:
                    {
                        finished = ProcessPushPull((ushort)cmd.ImmediateData);
                        break;
                    }
                case PioDecodedInstruction.Opcode.Mov:
                    {
                        finished = ProcessMov((ushort)cmd.ImmediateData);
                        break;
                    }
                case PioDecodedInstruction.Opcode.Irq:
                    {
                        finished = ProcessIrq((ushort)cmd.ImmediateData);
                        break;
                    }
                case PioDecodedInstruction.Opcode.Set:
                    {
                        finished = ProcessSet((ushort)cmd.ImmediateData);
                        break;
                    }
                default:
                    {
                        break;
                    }
            }

            if (finished)
            {
                if (!_ignoreDelay)
                {
                    ScheduleDelayCycles((ushort)cmd.DelayOrSideSet);
                }
                _ignoreDelay = false;
                _sideSetDone = false;
            }
            else
            {
                IsStalled = false;
            }


        }

        public void Enable()
        {
            Enabled = true;
            _executionThread.Start();
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

        private bool[] _irq;

        private ushort[] _program;
        private ushort? _immediateInstruction;
        private ushort _programCounter;

        private int _delayCounter;
        private int _delayCycles;
        private bool _sideSetDone;
        private bool _ignoreDelay;
        private int? _waitForInterrupt;

        public StateMachineClockDivider ClockDivider { get; private set; }
        public StateMachineExecutionControl ExecutionControl { get; set; }
        public StateMachineShiftControl ShiftControl { get; set; }
        public StateMachinePinControl PinControl { get; set; }
        public bool Enabled { get; private set; }
        public int Id { get; private set; }
        public bool IsStalled { get; private set; }
    }

}
