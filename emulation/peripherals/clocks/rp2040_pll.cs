/**
 * rp2040_pll.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;


namespace Antmicro.Renode.Peripherals.Miscellaneous
{

    public class RP2040PLL : RP2040PeripheralBase, IKnownSize
    {
        private enum Registers
        {
            CS = 0x00,
            PWR = 0x04,
            FBDIV_INT = 0x08,
            PRIM = 0x0c,
        }

        public RP2040PLL(Machine machine, ulong address) : base(machine, address)
        {
            bypass = false;
            refdiv = 1;
            vcopd = true;
            postdivpd = true;
            dsmpd = true;
            pd = true;
            fbdiv_int = 0;
            postdiv1 = 0x7;
            postdiv2 = 0x7;
            users = new List<Action>();
            DefineRegisters();
        }

        public void RegisterClient(Action callback)
        {
            users.Add(callback);
        }

        public ulong CalculateOutputFrequency(ulong frequency)
        {
            return (ulong)Math.Round((double)(((long)frequency / refdiv) * fbdiv_int / (postdiv1 * postdiv2)));
        }

        public bool PllEnabled()
        {
            return !pd;
        }

        private void UpdateUsers()
        {
            foreach (var a in users)
            {
                a();
            }
        }

        private void DefineRegisters()
        {
            Registers.CS.Define(this)
                .WithValueField(0, 6, valueProviderCallback: _ => refdiv,
                    writeCallback: (_, value) => refdiv = (byte)value,
                    name: "PLL_CS_REFDIV")
                .WithReservedBits(6, 2)
                .WithFlag(8, valueProviderCallback: _ => bypass,
                    writeCallback: (_, value) => bypass = value,
                    name: "PLL_CS_BYPASS")
                .WithReservedBits(9, 22)
                .WithFlag(31, FieldMode.Read, valueProviderCallback: _ => !pd,
                    name: "PLL_CS_LOCK");

            Registers.PWR.Define(this)
                .WithFlag(0, valueProviderCallback: _ => pd,
                    writeCallback: (_, value) =>
                    {
                        pd = value;
                        UpdateUsers();
                    },
                    name: "PLL_PWR_PD")
                .WithReservedBits(1, 1)
                .WithFlag(2, valueProviderCallback: _ => dsmpd,
                    writeCallback: (_, value) =>
                    {
                        dsmpd = value;
                        UpdateUsers();
                    },
                    name: "PLL_PWR_DSMPD")
                .WithFlag(3, valueProviderCallback: _ => postdivpd,
                    writeCallback: (_, value) =>
                    {
                        postdivpd = value;
                        UpdateUsers();
                    },
                    name: "PLL_PWR_POSTDIVPD")
                .WithReservedBits(4, 1)
                .WithFlag(5, valueProviderCallback: _ => vcopd,
                    writeCallback: (_, value) =>
                    {
                        vcopd = value;
                        UpdateUsers();
                    },
                    name: "PLL_PWR_VCOPD")
                .WithReservedBits(6, 26);

            Registers.FBDIV_INT.Define(this)
                .WithValueField(0, 12, valueProviderCallback: _ => fbdiv_int,
                    writeCallback: (_, value) => fbdiv_int = (ushort)value,
                    name: "PLL_FBDIV_INT")
                .WithReservedBits(12, 20);

            Registers.PRIM.Define(this)
                .WithReservedBits(0, 12)
                .WithValueField(12, 3, valueProviderCallback: _ => postdiv2,
                    writeCallback: (_, value) => postdiv2 = (byte)value,
                    name: "PLL_PRIM_POSTDIV2")
                .WithReservedBits(15, 1)
                .WithValueField(16, 3, valueProviderCallback: _ => postdiv1,
                    writeCallback: (_, value) => postdiv1 = (byte)value,
                    name: "PLL_PRIM_POSTDIV2")
                .WithValueField(19, 11);
        }

        public long Size { get { return 0x1000; } }

        private bool bypass;
        private byte refdiv;
        private bool vcopd;
        private bool postdivpd;
        private bool dsmpd;
        private bool pd;
        private ushort fbdiv_int;
        private byte postdiv1;
        private byte postdiv2;

        private List<Action> users;
    }
}
