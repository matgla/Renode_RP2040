using System;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public interface ISegmentDisplay : IPeripheral
    {
        event Action<ISegmentDisplay, bool[], bool[]> StateChanged;
        bool[] Segments { get; }
        bool[] Cells { get; }
        void OnGPIO(int number, bool value);
        void SetSegment(int number, bool state);
    }
}
