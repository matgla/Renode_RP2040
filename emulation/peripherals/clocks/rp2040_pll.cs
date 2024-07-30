/**
 * rp2040_pll.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Logging;


namespace Antmicro.Renode.Peripherals.Miscellaneous
{

    public class RP2040PLL : BasicDoubleWordPeripheral, IKnownSize
    {
        private enum Registers
        {
            CS = 0x00,
            PWR = 0x04,
            FBDIV_INT = 0x08,
            PRIM = 0x0c,
        }

        public RP2040PLL(Machine machine) : base(machine)
        {
            locked = false;
            bypass = false;
            refdiv = 1;
            vcopd = true;
            postdivpd = true;
            dsmpd = true;
            pd = true;
            fbdiv_int = 0;
            postdiv1 = 0x7;
            postdiv2 = 0x7;

            DefineRegisters();
        }

        private void DefineRegisters()
        {
        }

        public long Size { get { return 0x1000; } }

        private bool locked;
        private bool bypass;
        private byte refdiv;
        private bool vcopd;
        private bool postdivpd;
        private bool dsmpd;
        private bool pd;
        private ushort fbdiv_int;
        private byte postdiv1;
        private byte postdiv2;
    }
}
