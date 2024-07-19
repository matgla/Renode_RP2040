using System;
using System.Collections.Generic;

using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous.RP2040PIORegisters
{

    public class StateMachineClockDivider
    {
        public StateMachineClockDivider()
        {
            this.Integral = 1;
            this.Fractal = 0;
        }

        public void SetDivider(int integral, int fractal)
        {
            Integral = integral;
            Fractal = fractal;
            if (Integral == 0)
            {
                Integral = 65536;
                Fractal = 0;
            }
        }

        public long CalculateTargetFrequency(long frequency)
        {
            return (long)((double)frequency / ((double)Integral + (double)(Fractal) / 256));
        }

        public int Integral { get; private set; }
        public int Fractal { get; private set; }
    }

    public class StateMachineExecutionControl
    {
        public StateMachineExecutionControl()
        {
            this.SideEnabled = false;
            this.SidePinDirection = false;
            this.JumpPin = 0x00;
            this.OutEnableSelect = 0x00;
            this.OutInlineEnable = false;
            this.OutSticky = false;
            this.WrapTop = 0x1f;
            this.WrapBottom = 0x00;
            this.StatusSelect = false;
            this.StatusLevel = 0x00;
        }


        public bool SideEnabled { get; set; }
        public bool SidePinDirection { get; set; }
        public byte JumpPin { get; set; }
        public byte OutEnableSelect { get; set; }
        public bool OutInlineEnable { get; set; }
        public bool OutSticky { get; set; }
        public byte WrapTop { get; set; }
        public byte WrapBottom { get; set; }
        public bool StatusSelect { get; set; }
        public byte StatusLevel { get; set; }
    }

    public class StateMachineShiftControl
    {
        public enum Direction : byte { Left = 0, Right = 1 };

        public StateMachineShiftControl(Action<LogLevel, string> log)
        {
            this._log = log;
            this._txFifo = new Queue<uint>();
            this.TxFifoSize = 4;
            this._rxFifo = new Queue<uint>();
            this.RxFifoSize = 4;
            this.OutputShiftRegister = 0;
            this.OutputShiftRegisterCounter = 32;
            this.InputShiftRegister = 0;
            this.InputShiftRegisterCounter = 0;
            this.FifoTxJoin = false;
            this.FifoRxJoin = false;
            this.PullThreshold = 0;
            this.PushThreshold = 0;
            this.OutShiftDirection = Direction.Right;
            this.InShiftDirection = Direction.Right;
            this.AutoPull = false;
            this.AutoPush = false;
        }

        public void JoinTxFifo(bool join)
        {
            if (TxFifoSize != 8 && join == true)
            {
                _txFifo.Clear();
                _rxFifo.Clear();
                TxFifoSize = 8;
                RxFifoSize = 0;
                FifoTxJoin = true;
                FifoRxJoin = false;
            }
            else if (TxFifoSize == 8 && join == false)
            {
                _txFifo.Clear();
                _rxFifo.Clear(); TxFifoSize = 4;
                RxFifoSize = 4;
                FifoTxJoin = false;
                FifoRxJoin = false;
            }
        }

        public void JoinRxFifo(bool join)
        {
            if (RxFifoSize != 8 && join == true)
            {
                _txFifo.Clear();
                _rxFifo.Clear();
                RxFifoSize = 8;
                TxFifoSize = 0;
                FifoRxJoin = true;
                FifoTxJoin = false;
            }
            else if (RxFifoSize == 8 && join == false)
            {
                _txFifo.Clear();
                _rxFifo.Clear();
                TxFifoSize = 4;
                RxFifoSize = 4;
                FifoTxJoin = false;
                FifoRxJoin = false;
            }
        }

        private Action<LogLevel, string> _log;
        private Queue<uint> _txFifo;
        public int TxFifoSize { get; private set; }
        private Queue<uint> _rxFifo;
        public int RxFifoSize { get; private set; }

        public byte OutputShiftRegisterCounter { get; private set; }
        public int OutputShiftRegister { get; private set; }
        public byte InputShiftRegisterCounter { get; private set; }
        public int InputShiftRegister { get; private set; }

        public bool FifoTxJoin { get; private set; }
        public bool FifoRxJoin { get; private set; }
        public byte PullThreshold { get; set; }
        public byte PushThreshold { get; set; }
        public Direction OutShiftDirection { get; set; }
        public Direction InShiftDirection { get; set; }
        public bool AutoPull { get; set; }
        public bool AutoPush { get; set; }

    }

    public class StateMachinePinControl
    {
        public StateMachinePinControl()
        {
            this.SideSetCount = 0;
            this.SetCount = 0;
            this.OutCount = 0;
            this.InBase = 0;
            this.SideSetBase = 0;
            this.SetBase = 0;
            this.OutBase = 0;
        }

        public byte SideSetCount { get; set; }
        public byte SetCount { get; set; }
        public byte OutCount { get; set; }
        public byte InBase { get; set; }
        public byte SideSetBase { get; set; }
        public byte SetBase { get; set; }
        public byte OutBase { get; set; }
    }
}
