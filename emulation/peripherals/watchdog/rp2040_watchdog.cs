/**
 * rp2040_watchdog.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{

public class RP2040Watchdog : RP2040PeripheralBase
{
    public RP2040Watchdog(Machine machine, ulong address, RP2040Clocks clocks) : base(machine, address)
    {
        timer = new LimitTimer(machine.ClockSource, (long)clocks.ReferenceClockFrequency, this, "WATCHDOG", direction: Direction.Ascending, enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
        clocks.OnRefClockChange((value) => timer.Frequency = value);
    }

    private LimitTimer timer;
}

}