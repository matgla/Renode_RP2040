/**
 * rp2040_gpio.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.GPIOPort
{

    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public class RP2040GPIO : RpGpioBase
    {
        public RP2040GPIO(IMachine machine, int numberOfPins, int numberOfCores, ulong address) : base(machine, numberOfPins, numberOfCores, address)
        {
        }

        protected override DoubleWordRegister CreateStatusRegister(int pinId)
        {
            return new DoubleWordRegister(this)
                    .WithReservedBits(0, 8)
                    .WithFlag(8, FieldMode.Read,
                        valueProviderCallback: _ => State[pinId],
                        name: "GPIO" + pinId + "_STATUS_OUTFROMPERI")
                    .WithFlag(9, FieldMode.Read,
                        valueProviderCallback: _ => State[pinId],
                        name: "GPIO" + pinId + "_STATUS_OUTTOPAD")
                    .WithReservedBits(10, 2)
                    .WithFlag(12, FieldMode.Read,
                        valueProviderCallback: _ => IsPinOutput(pinId),
                        name: "GPIO" + pinId + "_STATUS_OEFROMPERI")
                    .WithFlag(13, FieldMode.Read,
                        valueProviderCallback: _ => IsPinOutput(pinId),
                        name: "GPIO" + pinId + "_STATUS_OETOPAD")
                    .WithReservedBits(14, 3)
                    .WithFlag(17, FieldMode.Read,
                        valueProviderCallback: _ => State[pinId],
                        name: "GPIO" + pinId + "_STATUS_INFROMPAD")
                    .WithReservedBits(18, 1)
                    .WithFlag(19, FieldMode.Read,
                        valueProviderCallback: _ => State[pinId],
                        name: "GPIO" + pinId + "_STATUS_INTOPERI")
                    .WithReservedBits(20, 4)
                    .WithFlag(24, FieldMode.Read,
                        valueProviderCallback: _ => IRQ1.IsSet || IRQ0.IsSet,
                        name: "GPIO" + pinId + "_STATUS_IRQFROMPAD")
                    .WithReservedBits(25, 1)
                    .WithFlag(26, FieldMode.Read,
                        valueProviderCallback: _ => IRQ1.IsSet || IRQ0.IsSet,
                        name: "GPIO" + pinId + "_STATUS_IRQTOPROC")
                    .WithReservedBits(27, 5);
        }

    }
}
