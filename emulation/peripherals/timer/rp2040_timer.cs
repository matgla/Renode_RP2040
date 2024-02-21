/*
 *   Copyright (c) 2024
 *   All rights reserved.
 */
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;
using System;
using Antmicro.Renode.Time;
using System.IO;

namespace Antmicro.Renode.Peripherals.Timers
{

    public class Alarm 
    {
        public ulong Value {get; set;}
        public bool Armed {get;set;}
        public bool IrqEnabled{get;set;}
        public GPIO Irq {get;set;} 

        public bool Fired {get; set;}

        public Alarm()
        {
            Irq = new GPIO();
            Armed = false;
            IrqEnabled = false;
        }
        public void Run(ulong Current)
        {
            if (Armed)
            {
                if (Current > Value)
                {
                    if (IrqEnabled)
                    {
                        Fired = true;
                        Irq.Set(true);
                    }
                    Armed = false;
                }
            }
        }

        public void Reset()
        {
        }
    }
    public class RP2040Timer : BasicDoubleWordPeripheral, IKnownSize
    {
        private IClockSource clockSource;

        private ulong counter;
        private void OnCounterFired()
        {
            counter += 100;
        
            foreach (var alarm in alarms)
            {
                alarm.Run(counter);
            }
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
            INTE = 0x38,
            INTF = 0x3c,
            INTS = 0x40
        }

        Alarm[] alarms;
        public RP2040Timer(Machine machine) : base(machine)
        {
            IRQ = new GPIO();
            alarms = new Alarm[4];
            this.clockSource = machine.ClockSource;
            Reset();
            DefineRegisters();
        }
        public long Size { get { return 0x1000; } }

        public GPIO IRQ { get; private set; }

        public override void Reset()
        {
            // 1us timer counter
            ClockEntry clock = new ClockEntry(1, 10000, OnCounterFired, this, "SystemClock", true, Direction.Ascending, WorkMode.Periodic, 1L);
            clock.Value = 0;
            clockSource.ExchangeClockEntryWith(OnCounterFired, (ClockEntry x) => clock, () => clock);
            // timer.LimitReached += () => UpdateTimer();
            for (int i = 0; i < alarms.Length; ++i)
            {
                if (i == 3)
                {
                alarms[i] = new Alarm()
                {
                    Irq = IRQ
                };
                }
                else 
                {
                alarms[i] = new Alarm();
 
                }
            }
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
                    return (counter >> 32) & 0xffffffff;
                },
                name: "TIMERAWH");
            Registers.TIMERAWL.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                {
                    return counter & 0xffffffff;
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
                    alarms[0].Armed = !value;
                })
                .WithFlag(1, FieldMode.Write, writeCallback: (_, value) =>
                {
                    alarms[1].Armed = !value;
                })
                .WithFlag(2, FieldMode.Write, writeCallback: (_, value) =>
                {
                    alarms[2].Armed = !value;
                })
                .WithFlag(3, FieldMode.Write, writeCallback: (_, value) =>
                {
                    alarms[3].Armed = !value;
                },
                name: "ARMED");

            Registers.INTS.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ =>
                {
                    return alarms[0].IrqEnabled;
                }, name: "INTS0")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ =>
                {
                    return alarms[1].IrqEnabled;
                }, name: "INTS1")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ =>
                {
                    return alarms[2].IrqEnabled;
                }, name: "INTS2")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ =>
                {
                    return alarms[3].IrqEnabled;
                }, name: "INTS3");

            Registers.INTE.Define(this)
                .WithFlag(0, FieldMode.Write | FieldMode.Read, writeCallback: (_, value) =>
                {
                    alarms[0].IrqEnabled = true;
                }, valueProviderCallback: _ => 
                {
                    return alarms[0].IrqEnabled;
                }, name: "INTF0")
                .WithFlag(1, FieldMode.Write | FieldMode.Read, writeCallback: (_, value) =>
                {
                    alarms[1].IrqEnabled = true;
                }, valueProviderCallback: _ => 
                {
                    return alarms[1].IrqEnabled;
                }, name: "INTF1")
                .WithFlag(2, FieldMode.Write | FieldMode.Read, writeCallback: (_, value) =>
                {
                    alarms[2].IrqEnabled = true;
                }, valueProviderCallback: _ => 
                {
                    return alarms[2].IrqEnabled;
                }, name: "INTF2")
                .WithFlag(3, FieldMode.Write | FieldMode.Read, writeCallback: (_, value) =>
                {
                    alarms[3].IrqEnabled = true;
                }, valueProviderCallback: _ => 
                {
                    return alarms[3].IrqEnabled;
                }, name: "INTF3");

            Registers.INTF.Define(this)
                .WithFlag(0, FieldMode.Write | FieldMode.Read, writeCallback: (_, value) =>
                {
                    alarms[0].Irq.Set(false);
                }, valueProviderCallback: _ => 
                {
                    return false;
                }, name: "INTF0")
                .WithFlag(1, FieldMode.Write | FieldMode.Read, writeCallback: (_, value) =>
                {
                    alarms[1].Irq.Set(false);
                }, valueProviderCallback: _ => 
                {
                    return false;
                }, name: "INTF1")
                .WithFlag(2, FieldMode.Write | FieldMode.Read, writeCallback: (_, value) =>
                {
                    alarms[2].Irq.Set(false);
                }, valueProviderCallback: _ => 
                {
                    return false;
                }, name: "INTF2")
                .WithFlag(3, FieldMode.Write | FieldMode.Read, writeCallback: (_, value) =>
                {
                    alarms[3].Irq.Set(false);
                }, valueProviderCallback: _ => 
                {
                    return false;
                }, name: "INTF3");

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
                            alarms[id].Armed = true;
                            alarms[id].Value = val;
                        },
                        valueProviderCallback: _ =>
                        {
                            return alarms[id].Value * 1000;
                        },
                        name: "ALARM" + id);
                    alarm_number++;
                }
            }


        }
    }
}