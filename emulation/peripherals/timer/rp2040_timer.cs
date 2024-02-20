/*
 *   Copyright (c) 2024
 *   All rights reserved.
 */
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using System;
using System.Xml.Serialization;
using System.Globalization;
using Antmicro.Renode.Time;
using Xwt;
using System.IO;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class RP2040Timer : BasicDoubleWordPeripheral, IKnownSize
    {
        private long frequency;
        private IClockSource clockSource;

        private void OnLimitReached()
        {
            this.Log(LogLevel.Error, "Timer Fired!!!!!!");
        }
        private enum Registers
        {
            ALARM0 = 0x10,
            ALARM1 = 0x14,
            ALARM2 = 0x18,
            ALARM3 = 0x1c,
            ARMED = 0x20,
            TIMERAWH = 0x24,
            TIMERAWL = 0x28,
            INTR = 0x34,
            INTS = 0x40
        }
        public RP2040Timer(Machine machine) : base(machine)
        {
            IRQ = new GPIO();
            frequency = 2000000;

            this.clockSource = machine.ClockSource;
            Reset();
            DefineRegisters();
        }
        public long Size { get { return 0x1000; } }

        public GPIO IRQ { get; private set; }

        private void AlarmFired(int id)
        {
            this.Log(LogLevel.Error, "Alarm" + id + " Fired!");
        }
        private void Alarm0Fired()
        {
            AlarmFired(0);
        }
        private void Alarm1Fired()
        {
            AlarmFired(1);
        }
        private void Alarm2Fired()
        {
            AlarmFired(2);
        }

        private void Alarm3Fired()
        {
            IRQ.Set(true);
            AlarmFired(3);
        }



        public override void Reset()
        {
            ClockEntry clock = new ClockEntry(10000, 2000000, OnLimitReached, this, "SystemClock", true, Direction.Ascending, WorkMode.OneShot, 1L);
            clock.Value = 0;
            clockSource.ExchangeClockEntryWith(OnLimitReached, (ClockEntry x) => clock, () => clock);
            // timer.LimitReached += () => UpdateTimer();

            ClockEntry a = new ClockEntry(ulong.MaxValue, 2000000, Alarm0Fired, this, "Alarm0", false, Direction.Ascending, WorkMode.Periodic, 1L);
            a.Value = 0;
            clockSource.AddClockEntry(a);

            ClockEntry b = new ClockEntry(ulong.MaxValue, 2000000, Alarm1Fired, this, "Alarm1", false, Direction.Ascending, WorkMode.Periodic, 1L);
            b.Value = 0;
            clockSource.AddClockEntry(b);

            ClockEntry c = new ClockEntry(ulong.MaxValue, 2000000, Alarm2Fired, this, "Alarm2", false, Direction.Ascending, WorkMode.Periodic, 1L);
            c.Value = 0;
            clockSource.AddClockEntry(c);

            ClockEntry d = new ClockEntry(ulong.MaxValue, 200000000, Alarm3Fired, this, "Alarm3", false, Direction.Ascending, WorkMode.Periodic, 1L);
            d.Value = 0;
            clockSource.AddClockEntry(d);
        }

        private void UpdateTimer()
        {
            this.Log(LogLevel.Error, "Timer updated");
        }

        private ClockEntry FindClock(string name)
        {
            foreach (ClockEntry e in machine.ClockSource.GetAllClockEntries())
            {
                if (e.LocalName == name)
                {
                    return e;
                }
            }
            throw new InvalidDataException();
        }
        public void DefineRegisters()
        {
            Registers.TIMERAWH.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                {
                    var clock = FindClock("SystemClock");
                    this.Log(LogLevel.Error, "Responding WH: " + clock.Value);
                    return (clock.Value >> 32) & 0xffffffff;
                },
                name: "TIMERAWH");
            Registers.TIMERAWL.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                {
                    var clock = FindClock("SystemClock");
                    this.Log(LogLevel.Error, "Responding WL: " + clock.Value);
                    return clock.Value & 0xffffffff;
                },
                name: "TIMERAWL");
            Registers.INTR.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) =>
                {
                }, name: "INTR0")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, value) =>
                {
                }, name: "INTR1")
                .WithFlag(2, FieldMode.Write, writeCallback: (_, value) =>
                {
                }, name: "INTR2")
                .WithFlag(3, FieldMode.Write, writeCallback: (_, value) =>
                {
                }, name: "INTR3");
            Registers.ARMED.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) =>
                {
                })
                .WithFlag(1, FieldMode.Write, writeCallback: (_, value) =>
                {
                })
                .WithFlag(2, FieldMode.Write, writeCallback: (_, value) =>
                {
                })
                .WithFlag(3, FieldMode.Write, writeCallback: (_, value) =>
                {
                    this.Log(LogLevel.Error, "Armed A3: " + value);
                },
                name: "ARMED");

            Registers.INTS.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ =>
                {
                    ClockEntry clock = FindClock("Alarm0");
                    return clock.Enabled;
                }, name: "INTS0")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ =>
                {
                    ClockEntry clock = FindClock("Alarm1");
                    return clock.Enabled;
                }, name: "INTS1")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ =>
                {
                    ClockEntry clock = FindClock("Alarm2");
                    return clock.Enabled;
                }, name: "INTS2")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ =>
                {
                    ClockEntry clock = FindClock("Alarm3");
                    return clock.Enabled;
                }, name: "INTS3");

            int alarm_number = 0;
            foreach (Registers r in Enum.GetValues(typeof(Registers)))
            {
                if (r >= Registers.ALARM0 && r <= Registers.ALARM3)
                {
                    int id = alarm_number;
                    r.Define(this)
                        .WithValueField(0, 32, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, val) =>
                        {
                            this.Log(LogLevel.Error, "Set A" + id + ": " + val);
                            ClockEntry clock = FindClock("Alarm" + id);
                            if (id == 0)
                            {

                                this.clockSource.ExchangeClockEntryWith(Alarm0Fired, x => {
                                    this.Log(LogLevel.Error, "Calling for: " + x.LocalName);
                                    return x;
                                });
                            }
                            if (id == 1)
                            {
                                this.clockSource.ExchangeClockEntryWith(Alarm1Fired, x => {
                                    this.Log(LogLevel.Error, "Calling for: " + x.LocalName);
                                    return x;
                                });
                            }
                            if (id == 2)
                            {
                                this.clockSource.ExchangeClockEntryWith(Alarm2Fired, x => {
                                    this.Log(LogLevel.Error, "Calling for: " + x.LocalName);
                                    return x;
                                });
                            }
                            if (id == 3)
                            {
                                ClockEntry newEntry = new ClockEntry(val, frequency, Alarm3Fired, this, "Alarm3", true, Direction.Ascending, WorkMode.OneShot);
                                this.clockSource.ExchangeClockEntryWith(Alarm3Fired, x => {
                                    this.Log(LogLevel.Error, "Calling for: " + x.LocalName);
                                    return newEntry;
                                });
                            }
                        },
                        valueProviderCallback: _ =>
                        {
                            ClockEntry clock = FindClock("Alarm" + id);
                            return clock.Value;
                        },
                        name: "ALARM" + id);
                    alarm_number++;
                }
            }


        }
    }
}