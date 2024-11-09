using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using System;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.Timers
{

    public class Alarm
    {
        public bool IrqEnabled { get; set; }
        public GPIO Irq { get; set; }

        public bool Fired { get; set; }
        public LimitTimer Clock { get; private set; }

        public Alarm(RP2040Timer timer, IMachine machine, int id)
        {
            Irq = new GPIO();
            IrqEnabled = false;

            Clock = new LimitTimer(machine.ClockSource, 1000000, timer, "AlarmTimer" + id, direction: Direction.Ascending, enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true)
            {
                AutoUpdate = true,
                Value = 0
            };
            Clock.LimitReached += OnCounterFired;
        }

        public void Reset()
        {
            Irq.Unset();
            IrqEnabled = false;
            Clock.Enabled = false;
            Clock.Value = 0;
            Clock.Limit = 0;
        }

        public void Enable(bool value)
        {
            Clock.Enabled = value;
        }
        public void SetAlarm(ulong currentTicks, ulong limit)
        {
            Fired = false;
            Clock.Limit = limit;
            Clock.Value = currentTicks;
        }

        private void OnCounterFired()
        {
            if (IrqEnabled)
            {
                Fired = true;
                Clock.Enabled = false;
                Irq.Set(true);
            }
        }
    }
    public class RP2040Timer : RP2040PeripheralBase, IKnownSize
    {
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
        public RP2040Timer(Machine machine, ulong address) : base(machine, address)
        {
            IRQs = new GPIO[4];
            for (int i = 0; i < 4; ++i)
            {
                IRQs[i] = new GPIO();
            }
            alarms = new Alarm[4];
            for (int i = 0; i < alarms.Length; ++i)
            {
                alarms[i] = new Alarm(this, machine, i)
                {
                    Irq = IRQs[i]
                };
            }

            Clock = new LimitTimer(machine.ClockSource, 1000000, this, "SystemClock", limit: 0xffffffffffffffff, direction: Direction.Ascending, eventEnabled: false, enabled: true, workMode: WorkMode.Periodic);
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            for (int i = 0; i < IRQs.Length; ++i)
            {
                IRQs[i].Unset();
            }
            for (int i = 0; i < alarms.Length; ++i)
            {
                alarms[i].Reset();
            }
        }
        public void DefineRegisters()
        {
            Registers.TIMERAWH.Define(this)
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ => (Clock.Value >> 32) & 0xffffffff,
                    name: "TIMERAWH");
            Registers.TIMERAWL.Define(this)
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ => Clock.Value & 0xffffffff,
                    name: "TIMERAWL");

            Registers.INTR.Define(this)
                .WithFlags(0, 4, FieldMode.Write,
                    writeCallback: (i, _, value) =>
                    {
                        if (value == false)
                        {
                            alarms[i].Irq.Unset();
                        }
                    },
                    name: "INTR");

            Registers.ARMED.Define(this)
                .WithFlags(0, 4, FieldMode.Write,
                    writeCallback: (i, _, value) => alarms[i].Enable(!value),
                    name: "ARMED");

            Registers.INTS.Define(this)
                .WithFlags(0, 4, FieldMode.Read,
                    valueProviderCallback: (i, _) => alarms[i].IrqEnabled,
                    name: "INTS");

            Registers.INTE.Define(this)
                .WithFlags(0, 4, FieldMode.Write | FieldMode.Read,
                    writeCallback: (i, _, value) => alarms[i].IrqEnabled = true,
                    name: "INTE");

            Registers.INTF.Define(this)
                .WithFlags(0, 4, FieldMode.Write | FieldMode.Read,
                    writeCallback: (i, _, value) => alarms[i].Irq.Set(value),
                    name: "INTF");

            int alarmNumber = 0;
            foreach (Registers r in Enum.GetValues(typeof(Registers)))
            {
                if (r >= Registers.ALARM0 && r <= Registers.ALARM3)
                {
                    int id = alarmNumber;
                    r.Define(this)
                        .WithValueField(0, 32, FieldMode.Write | FieldMode.Read,
                            writeCallback: (_, val) =>
                            {
                                alarms[id].SetAlarm(Clock.Value, val);
                                alarms[id].Enable(true);
                            },
                            valueProviderCallback: _ => alarms[id].Clock.Value,
                            name: "ALARM" + id);
                    alarmNumber++;
                }
            }
        }
        public GPIO[] IRQs { get; private set; }
        public GPIO IRQ0 => IRQs[0];
        public GPIO IRQ1 => IRQs[1];
        public GPIO IRQ2 => IRQs[2];
        public GPIO IRQ3 => IRQs[3];

        private LimitTimer Clock;
    }
}
