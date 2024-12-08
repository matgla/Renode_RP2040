using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class SegmentDisplay : IPeripheral, IGPIOReceiver
    {
        public SegmentDisplay(int segments = 7, int cells = 1, int colon = 0)
        {
            sync = new object();
            NumberOfSegments = segments;
            NumberOfCells = cells;
            Colon = colon;
            this.segments = new bool[NumberOfSegments];
            this.cells = new bool[NumberOfCells];
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

        public event Action<SegmentDisplay, bool[], bool[]> StateChanged;

        public void SetSegment(int number, bool state)
        {
            lock (sync)
            {
                if (segments[number] != state)
                {
                    segments[number] = state;
                    StateChanged?.Invoke(this, cells, segments);
                    this.Log(LogLevel.Noisy, "Segment[{0}] state changed to: {1}", number, state);
                }
            }
        }

        public void SetCell(int number, bool state)
        {
            lock (sync)
            {
                if (cells[number] != state)
                {
                    cells[number] = state;
                    StateChanged?.Invoke(this, cells, segments);
                    this.Log(LogLevel.Noisy, "Cell[{0}] state changed to: {1}", number, state);
                }
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

        private readonly object sync;
        private bool[] segments;
        private bool[] cells;
    }
}
