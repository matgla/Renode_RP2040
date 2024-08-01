/**
 * rp2040_rosc.cs
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

    public class RP2040ROSC : BasicDoubleWordPeripheral, IKnownSize
    {
        private enum Registers
        {
            CTRL = 0x00,
            FREQA = 0x04,
            FREQB = 0x08,
            DORMANT = 0x0c,
            DIV = 0x10,
            PHASE = 0x14,
            STATUS = 0x18,
            RANDOMBIT = 0x1c,
            COUNT = 0x20
        }

        public RP2040ROSC(Machine machine) : base(machine)
        {
            this.Frequency = 8000000;

            this.freqRange = 0xaa0;
            this.enabled = true;
            this.enableFlag = 0xfab;

            this.ds = new byte[8];
            this.scheduledDs = new byte[8];
            for (int i = 0; i < scheduledDs.Length; ++i)
            {
                scheduledDs[i] = 0;
            }
            this.freqaPasswd = 0;
            this.freqbPasswd = 0;

            this.div = 16;

            this.phaseFlip = false;
            this.phaseShift = 0;
            this.phaseEnable = true;
            this.phasePasswd = 0;

            this.stable = false;
            this.badwrite = false;
            this.dormant = 0x77616b65;
            this.count = new LimitTimer(machine.ClockSource, (long)Frequency, this, "XOSC_COUNT", direction: Direction.Descending, enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            this.stagesUsed = 8;
            this.random = new Random();
            DefineRegisters();
            CalculateFrequency();
        }

        // don't ask, it's my magical equation for ROSC frequency
        // not really reallistic 
        UInt64 CalculateFrequencyStage(UInt64 freq, UInt32 value)
        {
            double delay = 1.024 + ((double)((0.0005 * (double)value - 7) / (0.6 * (double)value + 3) / 7.2));
            return (UInt64)(freq * delay);
        }

        private void CalculateFrequency()
        {
            // In reality it's non linear and with not fully characterized frequencies per setting (vary on external conditions)
            // I simplified that for simulation purposes
            // 6.5 MHz by default for div 16, 12 MHz max, 1.8 MHz min
            // 1.8 MHz - 12 MHz during startup 

            // don't ask, it's my magical equation for ROSC frequency
            // not really reallistic 
            Frequency = (ulong)((double)3.5 * 498ul * 1000000);

            for (int i = 0; i < stagesUsed; ++i)
            {
                // higher ds number less delay
                Frequency = CalculateFrequencyStage(Frequency, ds[i]);
            }
            Frequency = Frequency / div;

            this.Log(LogLevel.Info, "Setting ROSC frequency to: " + Frequency / 1000000 + "MHz");
        }

        private void DefineRegisters()
        {
            Registers.CTRL.Define(this)
                .WithValueField(0, 12, valueProviderCallback: _ => freqRange,
                    writeCallback: (_, value) =>
                    {
                        freqRange = (ushort)value;
                        if (freqRange == 0xfa6)
                            stagesUsed = 2;

                        CalculateFrequency();
                    }, name: "ROSC_CTRL_FREQ_RANGE")
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
                            enabled = true;
                        }
                        else if (enableFlag == 0xd1e)
                        {
                            stable = false;
                            enabled = false;
                        }
                    }, name: "ROSC_CTRL_ENABLE")
                .WithReservedBits(24, 8);

            Registers.FREQA.Define(this)
                .WithValueField(0, 3, valueProviderCallback: _ => ds[0],
                    writeCallback: (_, value) => scheduledDs[0] = (byte)value, name: "ROSC_FREQA_DS0")
                .WithReservedBits(3, 1)
                .WithValueField(4, 3, valueProviderCallback: _ => ds[1],
                    writeCallback: (_, value) => scheduledDs[1] = (byte)value, name: "ROSC_FREQA_DS1")

                .WithReservedBits(7, 1)
                .WithValueField(8, 3, valueProviderCallback: _ => ds[2],
                    writeCallback: (_, value) => scheduledDs[2] = (byte)value, name: "ROSC_FREQA_DS2")
                .WithReservedBits(11, 1)
                .WithValueField(12, 3, valueProviderCallback: _ => ds[3],
                    writeCallback: (_, value) => scheduledDs[3] = (byte)value, name: "ROSC_FREQA_DS3")
                .WithReservedBits(15, 1)
                .WithValueField(16, 16, valueProviderCallback: _ => freqaPasswd,
                    writeCallback: (_, value) =>
                    {
                        freqaPasswd = (ushort)value;
                        if (freqaPasswd == 0x9696)
                        {
                            for (int i = 0; i < 4; ++i)
                            {
                                ds[i] = scheduledDs[i];
                            }
                        }
                        else
                        {
                            for (int i = 0; i < 4; ++i)
                            {
                                ds[i] = 0;
                            }
                        }

                        CalculateFrequency();
                    }, name: "ROSC_FREQA_PASSWD");

            Registers.FREQB.Define(this)
                .WithValueField(0, 3, valueProviderCallback: _ => ds[4],
                    writeCallback: (_, value) => scheduledDs[4] = (byte)value, name: "ROSC_FREQB_DS4")
                .WithReservedBits(3, 1)
                .WithValueField(4, 3, valueProviderCallback: _ => ds[5],
                    writeCallback: (_, value) => scheduledDs[5] = (byte)value, name: "ROSC_FREQB_DS5")

                .WithReservedBits(7, 1)
                .WithValueField(8, 3, valueProviderCallback: _ => ds[6],
                    writeCallback: (_, value) => scheduledDs[6] = (byte)value, name: "ROSC_FREQB_DS6")
                .WithReservedBits(11, 1)
                .WithValueField(12, 3, valueProviderCallback: _ => ds[7],
                    writeCallback: (_, value) => scheduledDs[7] = (byte)value, name: "ROSC_FREQB_DS7")
                .WithReservedBits(15, 1)
                .WithValueField(16, 16, valueProviderCallback: _ => freqbPasswd,
                    writeCallback: (_, value) =>
                    {
                        freqbPasswd = (ushort)value;
                        if (freqbPasswd == 0x9696)
                        {
                            for (int i = 4; i < 8; ++i)
                            {
                                ds[i] = scheduledDs[i];
                            }
                        }
                        else
                        {
                            for (int i = 4; i < 8; ++i)
                            {
                                ds[i] = 0;
                            }
                        }
                        CalculateFrequency();
                    }, name: "ROSC_FREQB_PASSWD");

            Registers.DORMANT.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => dormant,
                    writeCallback: (_, value) =>
                    {
                        dormant = (uint)value;
                        if (value != 0x636f6d61 || value != 0x77616b65)
                        {
                            badwrite = true;
                        }
                    }, name: "ROSC_DORMANT");


            Registers.DIV.Define(this)
                .WithValueField(0, 12, valueProviderCallback: _ =>
                {
                    if (div == 32)
                    {
                        return 0xaa0;
                    }
                    return (ulong)(0xaa0 + div);
                }, writeCallback: (_, value) =>
                {
                    div = (ushort)(value - 0xaa0);
                    if (div == 0 || div >= 32)
                    {
                        div = 32;
                    }
                }, name: "ROSC_DIV")
                .WithReservedBits(12, 20);

            Registers.PHASE.Define(this)
                .WithValueField(0, 2, valueProviderCallback: _ => phasePasswd == 0xaa ? phaseShift : 0u,
                    writeCallback: (_, value) => phaseShift = (byte)(value),
                    name: "ROSC_PHASE_SHIFT")
                .WithFlag(2, valueProviderCallback: _ => phaseFlip,
                    writeCallback: (_, value) => phaseFlip = value,
                    name: "ROSC_PHASE_FLIP")
                .WithFlag(3, valueProviderCallback: _ => phaseEnable,
                    writeCallback: (_, value) => phaseEnable = value,
                    name: "ROSC_PHASE_ENABLE")
                .WithValueField(4, 8, valueProviderCallback: _ => phasePasswd,
                    writeCallback: (_, value) => phasePasswd = (byte)value)
                .WithReservedBits(12, 20);

            Registers.STATUS.Define(this)
                .WithReservedBits(0, 12)
                .WithFlag(12, FieldMode.Read,
                    valueProviderCallback: _ => enabled,
                    name: "ROSC_STATUS_ENABLED")
                .WithReservedBits(13, 3)
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => true,
                    name: "ROSC_DIV_RUNNING")
                .WithReservedBits(17, 7)
                .WithFlag(24, FieldMode.Read | FieldMode.Write,
                    valueProviderCallback: _ => badwrite,
                    writeCallback: (_, value) => badwrite = false,
                    name: "ROSC_STATUS_BADWRITE")
                .WithReservedBits(25, 6)
                .WithFlag(31, FieldMode.Read, valueProviderCallback: _ => stable);

            Registers.RANDOMBIT.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => (bool)((random.Next() & 1) != 0),
                    name: "ROSC_RANDOMBIT")
                .WithReservedBits(1, 31);

            Registers.COUNT.Define(this)
                .WithValueField(0, 8, valueProviderCallback: _ => count.Value,
                    writeCallback: (_, value) =>
                    {
                        count.Value = value;
                        count.Enabled = true;
                    }, name: "ROSC_COUNT")
                .WithReservedBits(8, 24);

        }

        public long Size { get { return 0x1000; } }

        public ulong Frequency { get; private set; }

        private ushort freqRange;
        private ushort enableFlag;
        private bool enabled;

        private byte[] ds;
        private byte[] scheduledDs;
        private ushort freqaPasswd;
        private ushort freqbPasswd;

        private ushort div;

        private ushort phasePasswd;
        private byte phaseShift;
        private bool phaseFlip;
        private bool phaseEnable;

        private bool badwrite;
        private bool stable;
        private uint dormant;
        private LimitTimer count;
        private Random random;
        private byte stagesUsed;
    }
}
