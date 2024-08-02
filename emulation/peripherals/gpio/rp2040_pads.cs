/**
 * rp2040_pads.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.GPIOPort
{

    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public class RP2040Pads : IDoubleWordPeripheral, IGPIOReceiver, IKnownSize
    {
        public RP2040Pads(IMachine machine, RP2040GPIO gpio)
        {
            this.gpio = gpio;
            this.registers = CreateRegisters();
        }

        private DoubleWordRegisterCollection CreateRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>();
            registersMap[0] = new DoubleWordRegister(this)
                .WithTaggedFlag("VOLTAGE_SELECT", 0)
                .WithReservedBits(1, 31);

            for (int p = 0; p < gpio.NumberOfPins; ++p)
            {
                int i = p;
                registersMap[0x4 + i * 4] = new DoubleWordRegister(this)
                    .WithTaggedFlag("SLEWFAST", 0)
                    .WithTaggedFlag("SCHMITT", 1)
                    .WithFlag(2, valueProviderCallback: _ => gpio.GetPullDown(i),
                        writeCallback: (_, value) => gpio.SetPullDown(i, value),
                        name: "PDE")
                    .WithFlag(3, valueProviderCallback: _ => gpio.GetPullUp(i),
                        writeCallback: (_, value) => gpio.SetPullUp(i, value),
                        name: "PUE")
                    .WithValueField(4, 2, name: "DRIVE")
                    .WithTaggedFlag("IE", 6)
                    .WithFlag(7, valueProviderCallback: _ => gpio.IsPinOutputForcedDisabled(i),
                        writeCallback: (_, value) => gpio.ForcePinOutputDisable(i, value),
                        name: "OD")
                    .WithReservedBits(8, 24);
            }


            // SWD pins can be ignored
            // SWCLK
            registersMap[0x7c] = new DoubleWordRegister(this)
                .WithTaggedFlag("SLEWFAST", 0)
                .WithTaggedFlag("SCHMITT", 1)
                .WithTaggedFlag("PDE", 2)
                .WithTaggedFlag("PUE", 3)
                .WithValueField(4, 2, name: "DRIVE")
                .WithTaggedFlag("IE", 6)
                .WithTaggedFlag("OD", 7)
                .WithReservedBits(8, 24);
            // SWD
            registersMap[0x80] = new DoubleWordRegister(this)
                .WithTaggedFlag("SLEWFAST", 0)
                .WithTaggedFlag("SCHMITT", 1)
                .WithTaggedFlag("PDE", 2)
                .WithTaggedFlag("PUE", 3)
                .WithValueField(4, 2, name: "DRIVE")
                .WithTaggedFlag("IE", 6)
                .WithTaggedFlag("OD", 7)
                .WithReservedBits(8, 24);

            return new DoubleWordRegisterCollection(this, registersMap);
        }


        public long Size { get { return 0x1000; } }

        private RP2040GPIO gpio;
        private readonly DoubleWordRegisterCollection registers;
    }

}
