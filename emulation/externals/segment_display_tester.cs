using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using System.Threading;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Time;
using System.Runtime.CompilerServices;

namespace Antmicro.Renode.Testing
{
    public static class SegmentDisplayTesterExtenions
    {
        public static void CreateSegmentDisplayTester(this Emulation emulation, string name, ISegmentDisplay display, float defaultTimeout = 0)
        {
            emulation.ExternalsManager.AddExternal(new SegmentDisplayTester(display, defaultTimeout), name);
        }
    }

    public class SegmentDisplayTester : IExternal
    {
        public SegmentDisplayTester(ISegmentDisplay display, float defaultTimeout = 0)
        {
            ValidateArgument(defaultTimeout, nameof(defaultTimeout), allowZero: true);
            this.display = display;
            this.machine = display.GetMachine();
            this.defaultTimeout = defaultTimeout;
        }

        private readonly ISegmentDisplay led;
        private readonly IMachine machine;
        private readonly float defaultTimeout;
    }
}
