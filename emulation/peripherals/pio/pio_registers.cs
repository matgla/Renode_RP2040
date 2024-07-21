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

        public int TxFifoCount()
        {
            return _txFifo.Count;
        }

        public int RxFifoCount()
        {
            return _rxFifo.Count;
        }

        public bool TxFifoFull()
        {
            return _txFifo.Count == TxFifoSize;
        }

        public bool TxFifoEmpty()
        {
            return _txFifo.Count == 0;
        }

        public bool RxFifoFull()
        {
            return _rxFifo.Count == RxFifoSize;
        }

        public bool RxFifoEmpty()
        {
            return _rxFifo.Count == 0;
        }

        public void PushTxFifo(uint value)
        {
            _log(LogLevel.Info, "Push: " + value);
            if (!TxFifoFull())
            {
                _txFifo.Enqueue(value);
            }
        }

        public uint PopTxFifo()
        {
            if (!TxFifoEmpty())
            {
                return _txFifo.Dequeue();
            }
            return 0;
        }

        public void PushRxFifo(uint value)
        {
            if (!RxFifoFull())
            {
                _rxFifo.Enqueue(value);
            }
        }

        public uint PopRxFifo()
        {
            if (!RxFifoEmpty())
            {
                return _rxFifo.Dequeue();
            }
            return 0;
        }

        public void WriteInputShiftRegister(int bits, uint data)
        {
            if (InShiftDirection == Direction.Left)
            {
                InputShiftRegister = ((InputShiftRegister << bits) | data);
            }
            else
            {
                InputShiftRegister = (InputShiftRegister >> bits) | (data << (32 - bits));
            }
            InputShiftRegisterCounter += (byte)bits;

            if (AutoPush && InputShiftRegisterCounter >= PushThreshold)
            {
                PushInputShiftRegister();
            }
        }

        public uint ReadOutputShiftRegister(int bits)
        {
            uint data = 0;
            uint mask = (uint)((1ul << bits) - 1);
            if (OutShiftDirection == Direction.Right)
            {
                data = OutputShiftRegister & mask;
                OutputShiftRegister >>= bits;
            }
            else
            {
                data = (OutputShiftRegister >> (32 - bits)) & mask;
                OutputShiftRegister <<= bits;
            }

            OutputShiftRegisterCounter += (byte)bits;
            if (OutputShiftRegisterCounter >= PullThreshold && AutoPull)
            {
                LoadOutputShiftRegister();
            }

            return data;
        }

        public void PushInputShiftRegister()
        {
            PushRxFifo((uint)InputShiftRegister);
            InputShiftRegister = 0;
            InputShiftRegisterCounter = 0;
        }

        public void SetInputShiftRegister(uint data, int counter)
        {
            InputShiftRegister = data;
            InputShiftRegisterCounter = (byte)counter;
        }

        public void SetOutputShiftRegister(uint data, int counter)
        {
            OutputShiftRegister = data;
            OutputShiftRegisterCounter = (byte)counter;
        }

        public void LoadOutputShiftRegister(uint value)
        {
            OutputShiftRegister = value;
        }

        public void LoadOutputShiftRegister()
        {
            OutputShiftRegister = PopTxFifo();
            _log(LogLevel.Info, "Readed from tx: " + OutputShiftRegister);
            OutputShiftRegisterCounter = 0;
        }


        private Action<LogLevel, string> _log;
        private Queue<uint> _txFifo;
        public int TxFifoSize { get; private set; }
        private Queue<uint> _rxFifo;
        public int RxFifoSize { get; private set; }

        public byte OutputShiftRegisterCounter { get; private set; }
        public uint OutputShiftRegister { get; private set; }
        public byte InputShiftRegisterCounter { get; private set; }
        public uint InputShiftRegister { get; private set; }

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

    public class PIOIRQ
    {
        public PIOIRQ()
        {
            this.SM3 = null;
            this.SM2 = null;
            this.SM1 = null;
            this.SM0 = null;
            this.SM3_TXNFULL = null;
            this.SM2_TXNFULL = null;
            this.SM1_TXNFULL = null;
            this.SM0_TXNFULL = null;
            this.SM3_RXNEMPTY = null;
            this.SM2_RXNEMPTY = null;
            this.SM1_RXNEMPTY = null;
            this.SM0_RXNEMPTY = null;
        }

        public bool? GetByIndex(int index)
        {
            switch (index)
            {
                case 0: return SM0_RXNEMPTY;
                case 1: return SM1_RXNEMPTY;
                case 2: return SM2_RXNEMPTY;
                case 3: return SM3_RXNEMPTY;
                case 4: return SM0_TXNFULL;
                case 5: return SM1_TXNFULL;
                case 6: return SM2_TXNFULL;
                case 7: return SM3_TXNFULL;
                case 8: return SM0;
                case 9: return SM1;
                case 10: return SM2;
                case 11: return SM3;
            }
            return null;
        }

        public bool? SM3 { get; set; }
        public bool? SM2 { get; set; }
        public bool? SM1 { get; set; }
        public bool? SM0 { get; set; }
        public bool? SM3_TXNFULL { get; set; }
        public bool? SM2_TXNFULL { get; set; }
        public bool? SM1_TXNFULL { get; set; }
        public bool? SM0_TXNFULL { get; set; }
        public bool? SM3_RXNEMPTY { get; set; }
        public bool? SM2_RXNEMPTY { get; set; }
        public bool? SM1_RXNEMPTY { get; set; }
        public bool? SM0_RXNEMPTY { get; set; }
    }
}
