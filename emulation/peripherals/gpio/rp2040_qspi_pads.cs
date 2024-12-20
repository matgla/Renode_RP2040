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
using IronPython.Compiler.Ast;

namespace Antmicro.Renode.Peripherals.GPIOPort
{

    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public class RP2040QspiPads : RP2040PeripheralBase, IKnownSize
    {
        public RP2040QspiPads(IMachine machine, RP2040GPIO gpio, ulong address) : base(machine, address)
        {
            this.gpio = gpio;
            this.registers = CreateRegisters();
            Reset();
        }

        private void InitializeDefaultStates()
        {
            this.gpio.SetPullDown(1, false);
            this.gpio.SetPinOutput(1, true);
            this.gpio.SetPullUp(1, true);
            this.gpio.WritePin(1, true);
            for (int i = 0; i < 4; ++i)
            {
                this.gpio.SetPullDown(2 + i, false);
                this.gpio.SetPullUp(2 + i, false);
            }
        }

        public override uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void Reset()
        {
            InitializeDefaultStates();
        }

        private DoubleWordRegisterCollection CreateRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>();
            registersMap[0] = new DoubleWordRegister(this)
                .WithTaggedFlag("VOLTAGE_SELECT", 0)
                .WithReservedBits(1, 31);

            registersMap[0x04] = new DoubleWordRegister(this)
                .WithTaggedFlag("SLEWFAST", 0)
                .WithTaggedFlag("SCHMITT", 1)
                .WithFlag(2, valueProviderCallback: _ => gpio.GetPullDown(0),
                    writeCallback: (_, value) => gpio.SetPullDown(0, value),
                    name: "PDE")
                .WithFlag(3, valueProviderCallback: _ => gpio.GetPullUp(0),
                    writeCallback: (_, value) => gpio.SetPullUp(0, value),
                    name: "PUE")
                .WithValueField(4, 2, name: "DRIVE")
                .WithTaggedFlag("IE", 6)
                .WithFlag(7, valueProviderCallback: _ => gpio.IsPinOutputForcedDisabled(0),
                    writeCallback: (_, value) => gpio.ForcePinOutputDisable(0, value),
                    name: "OD")
                .WithReservedBits(8, 24);

            for (int i = 0; i < 4; ++i)
            {
                int p = i;
                registersMap[0x08 + i * 4] = new DoubleWordRegister(this)
                .WithTaggedFlag("SLEWFAST", 0)
                .WithTaggedFlag("SCHMITT", 1)
                .WithFlag(2, valueProviderCallback: _ => gpio.GetPullDown(2 + p),
                    writeCallback: (_, value) => gpio.SetPullDown(2 + p, value),
                    name: "PDE")
                .WithFlag(3, valueProviderCallback: _ => gpio.GetPullUp(2 + p),
                    writeCallback: (_, value) => gpio.SetPullUp(2 + p, value),
                    name: "PUE")
                .WithValueField(4, 2, name: "DRIVE")
                .WithTaggedFlag("IE", 6)
                .WithFlag(7, valueProviderCallback: _ => gpio.IsPinOutputForcedDisabled(2 + p),
                    writeCallback: (_, value) => gpio.ForcePinOutputDisable(2 + p, value),
                    name: "OD")
                .WithReservedBits(8, 24);
            }

            registersMap[0x18] = new DoubleWordRegister(this)
                .WithTaggedFlag("SLEWFAST", 0)
                .WithTaggedFlag("SCHMITT", 1)
                .WithFlag(2, valueProviderCallback: _ => gpio.GetPullDown(1),
                    writeCallback: (_, value) => gpio.SetPullDown(0, value),
                    name: "PDE")
                .WithFlag(3, valueProviderCallback: _ => gpio.GetPullUp(1),
                    writeCallback: (_, value) => gpio.SetPullUp(0, value),
                    name: "PUE")
                .WithValueField(4, 2, name: "DRIVE")
                .WithTaggedFlag("IE", 6)
                .WithFlag(7, valueProviderCallback: _ => gpio.IsPinOutputForcedDisabled(1),
                    writeCallback: (_, value) => gpio.ForcePinOutputDisable(1, value),
                    name: "OD")
                .WithReservedBits(8, 24);


            return new DoubleWordRegisterCollection(this, registersMap);
        }


        private RP2040GPIO gpio;
        private readonly DoubleWordRegisterCollection registers;
    }

}
