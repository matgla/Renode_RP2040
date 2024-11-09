/**
 * rp2040_watchdog.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Logging;
using System.Linq;
using System;

namespace Antmicro.Renode.Peripherals.Timers
{

    public class RP2040Watchdog : RP2040PeripheralBase
    {
        public RP2040Watchdog(Machine machine, ulong address, RP2040Clocks clocks) : base(machine, address)
        {
            // tick is 1us always according to datasheet
            // but due to errata ticks are incremented twice instead of once, so easiest way
            // is to just multiply frequency by two
            timer = new LimitTimer(machine.ClockSource, 2000000, this, "WATCHDOG", direction: Direction.Descending, enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += TimeoutReboot;
            this.clocks = clocks;
            scratch = new uint[8];
            forced = false;
            DefineRegisters();
            Reset();
            clocks.OnRefClockChange(UpdateTimerFrequency);
        }

        public override void Reset()
        {
            pauseDbg0.Value = false;
            pauseDbg1.Value = false;
            pauseJtag.Value = false;
            timer.Enabled = false;
            timer.Limit = 0;
            timer.Value = 0;
        }

        private void UpdateTimerFrequency(long frequency)
        {
            if (cycles.Value == 0)
            {
                this.Log(LogLevel.Debug, "Watchdog disabled due to lack of cycles configuration");
                timer.Enabled = false;
            }

            // * 2 due to bug in RP2040, please check errrata RP2040-E1 inside datasheet
            long newFrequency = frequency / (long)cycles.Value * 2;
            this.Log(LogLevel.Debug, "Changed frequency to: {0}", newFrequency);

            this.Log(LogLevel.Debug, "Enabled: " + timer.Enabled + ", timer limit: " + timer.Limit + ", timer value: " + timer.Value);
            timer.Frequency = newFrequency;
        }
        private void DefineRegisters()
        {
            Registers.CTRL.Define(this)
                .WithValueField(0, 24, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        return timer.Value;
                    }, name: "TIME")
                .WithFlag(24, out pauseJtag, name: "PAUSE_JTAG")
                .WithFlag(25, out pauseDbg0, name: "PAUSE_DBG0")
                .WithFlag(26, out pauseDbg1, name: "PAUSE_DBG1")
                .WithReservedBits(27, 3)
                .WithFlag(30, writeCallback: (_, value) =>
                {
                    timer.Enabled = value;
                    this.Log(LogLevel.Debug, "Watchdog enable: " + value);
                }, valueProviderCallback: _ => timer.Enabled, name: "ENABLED")
                .WithFlag(31, FieldMode.Write, writeCallback: (_, value) =>
                {
                    if (value)
                    {
                        forced = true;
                        Reboot();
                    }
                });
            Registers.LOAD.Define(this)
                .WithValueField(0, 24, valueProviderCallback: _ => timer.Value,
                    writeCallback: (_, value) => timer.Value = value, name: "LOAD")
                .WithReservedBits(24, 8);

            Registers.REASON.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => !forced, name: "TIMER")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => forced, name: "FORCE")
                .WithReservedBits(2, 30);

            int scratchId = 0;
            foreach (Registers r in Enum.GetValues(typeof(Registers)))
            {
                if (r >= Registers.SCRATCH0 && r <= Registers.SCRATCH7)
                {
                    int id = scratchId;
                    r.Define(this)
                        .WithValueField(0, 32, FieldMode.Write | FieldMode.Read,
                            valueProviderCallback: _ => scratch[id],
                            writeCallback: (_, value) => scratch[id] = (uint)value,
                            name: "SCRATCH" + id);
                    scratchId++;
                }
            }

            Registers.TICK.Define(this)
                .WithValueField(0, 9, out cycles,
                    writeCallback: (_, value) =>
                    {
                        UpdateTimerFrequency(clocks.ReferenceClockFrequency);
                    }, name: "CYCLES")
                .WithFlag(9, out tickEnable, name: "ENABLE")
                .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => timer.Enabled, name: "RUNNING")
                .WithValueField(11, 9, FieldMode.Read, valueProviderCallback: _ => 0, name: "COUNT")
                .WithReservedBits(20, 12);

        }

        private void TimeoutReboot()
        {
            this.Log(LogLevel.Info, "Watchdog fired!");
            forced = false;
            Reboot();
        }
        private void Reboot()
        {
            if (pauseJtag.Value)
            {
                this.Log(LogLevel.Info, "Watchdog prevented: jtag");
                return;
            }
            var cpus = machine.SystemBus.GetCPUs().OfType<ICpuSupportingGdb>();
            foreach (var cpu in cpus)
            {
                if (cpu.DebuggerConnected && machine.SystemBus.GetCPUSlot(cpu) == 0 && pauseDbg0.Value)
                {
                    this.Log(LogLevel.Info, "Watchdog prevented: debugger on core 0");
                    return;
                }
                if (cpu.DebuggerConnected && machine.SystemBus.GetCPUSlot(cpu) == 1 && pauseDbg1.Value)
                {
                    this.Log(LogLevel.Info, "Watchdog prevented: debugger on core 1");
                    return;
                }
            }
            this.Log(LogLevel.Info, "Machine reboot scheduled");
            machine.LocalTimeSource.ExecuteInNearestSyncedState(_ => machine.Reset());
        }

        private LimitTimer timer;
        private RP2040Clocks clocks;

        private IFlagRegisterField pauseJtag;
        private IFlagRegisterField pauseDbg0;
        private IFlagRegisterField pauseDbg1;
        private IFlagRegisterField tickEnable;

        private IValueRegisterField cycles;
        private bool forced;
        private uint[] scratch;

        private enum Registers
        {
            CTRL = 0x00,
            LOAD = 0x04,
            REASON = 0x08,
            SCRATCH0 = 0x0c,
            SCRATCH1 = 0x10,
            SCRATCH2 = 0x14,
            SCRATCH3 = 0x18,
            SCRATCH4 = 0x1c,
            SCRATCH5 = 0x20,
            SCRATCH6 = 0x24,
            SCRATCH7 = 0x28,
            TICK = 0x2c
        }
    }

}