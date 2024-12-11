using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using System.Threading;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class SegmentDisplay : ISegmentDisplay, IGPIOReceiver
    {
        public SegmentDisplay(IMachine machine, int segments = 7, int cells = 1, int colon = 0, float? filteringTime = null)
        {
            sync = new object();
            NumberOfSegments = segments;
            NumberOfCells = cells;
            this.filteringTime = filteringTime;
            Colon = colon;
            this.segments = new bool[NumberOfSegments];
            this.cells = new bool[NumberOfCells];
            this.machine = machine;
            Reset();
        }

        public void Reset()
        {
            for (int c = 0; c < NumberOfCells; ++c)
            {
                this.cells[c] = false;
            }

            for (int s = 0; s < NumberOfSegments; ++s)
            {
                this.segments[s] = false;
            }
            timeoutStarted = false;
        }

        public void OnGPIO(int number, bool value)
        {
            if (number >= NumberOfSegments + NumberOfCells)
            {
                return;
            }

            // if cells change 
            if (number < NumberOfCells)
            {
                SetCell(number, value);
            }
            else
            {
                SetSegment(number - NumberOfCells, value);
            }
        }

        public event Action<ISegmentDisplay, bool[], bool[]> StateChanged;

        public void SetSegment(int number, bool state)
        {
            bool stateChanged = false;

            lock (sync)
            {
                if (segments[number] != state)
                {
                    segments[number] = state;
                    stateChanged = true;
                }
            }

            if (stateChanged)
            {
                TriggerStateChange();
            }

        }

        public void SetCell(int number, bool state)
        {
            bool stateChanged = false;
            lock (sync)
            {
                if (cells[number] != state)
                {
                    cells[number] = state;
                    stateChanged = true;
                }
            }

            if (stateChanged)
            {
                TriggerStateChange();
            }
        }

        public bool[] Segments
        {
            get => segments;
        }

        public bool[] Cells
        {
            get => cells;
        }


        public readonly int NumberOfSegments;
        public readonly int NumberOfCells;
        public readonly int Colon;

        private void TriggerStateChange()
        {
            // if filtering time is set 
            // code will filter out events that occured in time less than filteringTime 
            // this is needed to filter out fast switching changes on GPIO to be visible as single shot 
            if (filteringTime != null)
            {
                lock (sync)
                {
                    if (timeoutStarted)
                    {
                        return;
                    }
                    timeoutStarted = true;
                }
                System.Threading.Timer timer = null;
                timer = new System.Threading.Timer((t) =>
                {
                    lock (sync)
                    {
                        StateChanged?.Invoke(this, cells, segments);
                        timeoutStarted = false;
                    }
                    timer.Dispose();
                }, null, (int)(filteringTime.Value * 1000), System.Threading.Timeout.Infinite);
            }
            else
            {
                StateChanged?.Invoke(this, cells, segments);
            }

        }

        private readonly object sync;
        private bool[] segments;
        private bool[] cells;
        private float? filteringTime;
        private IMachine machine;
        private bool timeoutStarted;
    }
}
