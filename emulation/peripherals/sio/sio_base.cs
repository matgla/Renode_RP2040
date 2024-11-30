/**
 * rp2350_sio.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

using Antmicro.Renode.Peripherals.GPIOPort;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class FifoStatus
    {
        public bool Roe { get; set; }
        public bool Wof { get; set; }
        public bool Rdy { get; set; }
        public bool Vld { get; set; }
    }

    public class Divider
    {
        public long Dividend { get; set; }
        public long Divisor { get; set; }
        public long Quotient { get; set; }
        public long Remainder { get; set; }

        public bool Ready { get; set; }
        public bool Dirty { get; set; }

        public void CalculateSigned()
        {
            if (Divisor != 0)
            {
                Quotient = Dividend / Divisor;
                Remainder = Dividend % Divisor;
            }
            Ready = true;
        }
        public void CalculateUnsigned()
        {
            if (Divisor != 0)
            {
                Quotient = Dividend / Divisor;
                Remainder = Dividend % Divisor;
            }
            Ready = true;
        }
    }
}
