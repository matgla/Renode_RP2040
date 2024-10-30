/**
 * rp2040_xosc.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */


using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{

    public class RP2040XOSC : RP2040PeripheralBase 
    {
        private enum Registers
        {
            CTRL = 0x00,
            STATUS = 0x04,
            DORMANT = 0x08,
            STARTUP = 0x0c,
            COUNT = 0x1c
        }

        public RP2040XOSC(Machine machine, ulong frequency, ulong address) : base(machine, address)
        {
            this.Frequency = frequency;

            this.Enabled = false;
            this.stable = false;
            this.badwrite = false;
            this.dormant = 0x77616b65;
            this.x4 = false;
            this.delay = 0xc4;
            this.count = new LimitTimer(machine.ClockSource, (long)Frequency, this, "XOSC_COUNT", direction: Direction.Descending, enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);

            DefineRegisters();
        }
        private void DefineRegisters()
        {
            Registers.CTRL.Define(this)
                .WithValueField(0, 12, valueProviderCallback: _ => 0xaa0,
                    writeCallback: (_, value) =>
                    {
                        if (value != 0xaa0 || value != 0xaa1 || value != 0xaa2 || value != 0xaa3)
                        {
                            badwrite = true;
                        }
                    }, name: "XOSC_CTRL_FREQ_RANGE")
                .WithValueField(12, 12, valueProviderCallback: _ => enableFlag,
                    writeCallback: (_, value) =>
                    {
                        enableFlag = (ushort)value;
                        if (value != 0xd1e || value != 0xfab)
                        {
                            badwrite = true;
                        }

                        if (enableFlag == 0xfab)
                        {
                            // simulation have always stable clock :)
                            stable = true;
                            Enabled = true;
                        }
                        else if (enableFlag == 0xd1e)
                        {
                            stable = false;
                            Enabled = false;
                        }
                    }, name: "XOSC_CTRL_ENABLE")
                .WithReservedBits(24, 8);


            Registers.STATUS.Define(this)
                .WithValueField(0, 2, FieldMode.Read,
                    valueProviderCallback: _ => 0,
                    name: "XOSC_STATUS_FREQ_RANGE")
                .WithReservedBits(2, 10)
                .WithFlag(12, FieldMode.Read,
                    valueProviderCallback: _ => Enabled,
                    name: "XOSC_STATUS_ENABLED")
                .WithReservedBits(13, 11)
                .WithFlag(24, FieldMode.Read | FieldMode.Write,
                    valueProviderCallback: _ => badwrite,
                    writeCallback: (_, value) => badwrite = false,
                    name: "XOSC_STATUS_BADWRITE")
                .WithReservedBits(25, 6)
                .WithFlag(31, FieldMode.Read, valueProviderCallback: _ => stable);

            Registers.DORMANT.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => dormant,
                    writeCallback: (_, value) =>
                    {
                        dormant = (uint)value;
                        if (value != 0x636f6d61 || value != 0x77616b65)
                        {
                            badwrite = true;
                        }
                    }, name: "XOSC_DORMANT");

            Registers.STARTUP.Define(this)
                .WithValueField(0, 14, valueProviderCallback: _ => delay,
                    writeCallback: (_, value) => delay = (uint)value,
                    name: "XOSC_STARTUP_DELAY")
                .WithReservedBits(14, 6)
                .WithFlag(20, valueProviderCallback: _ => x4,
                    writeCallback: (_, value) => x4 = value)
                .WithReservedBits(21, 11);

            Registers.COUNT.Define(this)
                .WithValueField(0, 8, valueProviderCallback: _ => count.Value,
                    writeCallback: (_, value) =>
                    {
                        count.Value = value;
                        count.Enabled = true;
                    }, name: "XOSC_DELAY")
                .WithReservedBits(8, 24);

        }

        public ulong Frequency { get; private set; }
        public bool Enabled { get; private set; }

        private bool badwrite;
        private bool stable;
        private ushort enableFlag;
        private uint dormant;
        private uint delay;
        private bool x4;
        private LimitTimer count;
    }
}
