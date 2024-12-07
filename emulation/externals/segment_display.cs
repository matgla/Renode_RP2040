using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class SegmentDisplay : IGPIOReceiver
    {
        public SegmentDisplay(int segments = 7, int cells = 1)
        {
            sync = new object();
            NumberOfSegments = segments;
            NumberOfCells = cells;
            state = new bool[NumberOfSegments];
        }

        public void OnGPIO(int number, bool value)
        {
            this.log(LogLevel.Error,  "On gpio: " + number);
            if (number >= NumberOfSegments + NumberOfCells)
            {
                return;
            } 

           // State[number] = value;
        }

        public event Action<SegmentDisplay, bool[]> StateChanged;
        public bool[] State 
        {
            get => state;
            private set 
            {
                lock(sync)
                {
                    if (value == state)
                    {
                        return;
                    }

                    state = value;
                    StateChanged?.Invoke(this, state);
                    this.Log(LogLevel.Noisy, "SegmentDisplay state changed to: ");
                }
            }
        }

        public readonly int NumberOfSegments;
        public readonly int NumberOfCells;

        private readonly object sync;
        private readonly bool[] state;
    }
}