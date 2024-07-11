using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using System;
using Antmicro.Renode.Time;
using Xwt;
using Antmicro.Renode.Logging;
using System.Collections.Generic;
using System.Linq;


namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class PioStateMachine 
    {
        public PioStateMachine(IMachine machine, ushort[] program)
        {
            long frequency = machine.ClockSource.GetAllClockEntries().First().Frequency;

            this.Enabled = false;
            executionThread = machine.ObtainManagedThread(Step, (uint)frequency, "piosm");
            this.program = program;
        }

        private IManagedThread executionThread;
        private ushort[] program;
        public bool Enabled {get; private set;}
        
        protected void Step()
        {
            var cmd = new PioDecodedInstruction(instruction);
        }

        public void Enable()
        {
            Enabled = true;
            executionThread.Start();
        }
    }


    public class RP2040PIO : BasicDoubleWordPeripheral, IKnownSize
    {
        private enum Registers
        {
            CTRL = 0x00,
            FSTAT = 0x04,
            FDEBUG = 0x08,
            FLEVEL = 0x0c,
            IRQ = 0x30,
            IRQ_FORCE = 0x34,
            INPUT_SYNC_BYPASS = 0x38,
            SM0_CLKDIV = 0xc8,
            SM0_EXECCTRL = 0xcc,
            SM0_SHIFTCTRL = 0xd0,
            SM0_ADDR = 0xd4,
            SM0_INSTR = 0xd8,
            SM0_PINCTRL = 0xdc,
            SM1_CLKDIV = 0xe0,
            SM1_EXECCTRL = 0xe4,
            SM1_SHIFTCTRL = 0xe8,
            SM1_ADDR = 0xec,
            SM1_INSTR = 0xf0,
            SM1_PINCTRL = 0xf4,
            SM2_CLKDIV = 0xf8,
            SM2_EXECCTRL = 0xfc,
            SM2_SHIFTCTRL = 0x100,
            SM2_ADDR = 0x104,
            SM2_INSTR = 0x108,
            SM2_PINCTRL = 0x10c,
            SM3_CLKDIV = 0x110,
            SM3_EXECCTRL = 0x114,
            SM3_SHIFTCTRL = 0x118,
            SM3_ADDR = 0x11c,
            SM3_INSTR = 0x120,
            SM3_PINCTRL = 0x124,
        }

        public RP2040PIO(Machine machine) : base(machine)
        {
            IRQs = new GPIO[2];
            for (int i = 0; i < IRQs.Length; ++i)
            {
                IRQs[i] = new GPIO();
            }
            Instructions = new ushort[32];
            TxFifos = new Queue<long>[4];
            RxFifos = new Queue<long>[4];
            for (int i = 0; i < TxFifos.Length; ++i)
            {
                TxFifos[i] = new Queue<long>();
            }

            for (int i = 0; i < RxFifos.Length; ++i)
            {
                RxFifos[i] = new Queue<long>();
            }

            StateMachines = new PioStateMachine[4];
            for (int i = 0; i < StateMachines.Length; ++i)
            {
                StateMachines[i] = new PioStateMachine(machine, Instructions); 
            }

            DefineRegisters();
            Reset();
        }
       
        private PioStateMachine[] StateMachines;
        private Queue<long>[] TxFifos;
        private Queue<long>[] RxFifos;
        public long Size { get { return 0x1000; } }
        public GPIO[] IRQs { get; private set;}
        public GPIO IRQ0 => IRQs[0];
        public GPIO IRQ1 => IRQs[1];
        private ushort[] Instructions;
        public override void Reset()
        {
        }

        public void DefineRegisters()
        {
            Registers.CTRL.Define(this)
                .WithValueField(0, 4, FieldMode.Read | FieldMode.Write,
                    writeCallback: (_, value) => {
                        for (int i = 0; i < StateMachines.Length; ++i)
                        {
                            if (((1ul << i) & (ulong)value) != 0ul)
                            {
                                StateMachines[i].Enable();
                            }
                        }
                    },
                    valueProviderCallback: _ => {
                        ulong enabledStateMachines = 0;
                        for (int i = 0; i < StateMachines.Length; ++i) 
                        {
                            enabledStateMachines |= Convert.ToUInt32(StateMachines[i].Enabled) << i;    
                        }
                        return enabledStateMachines;
                    },
                    name: "CTRL");
            for (int i = 0; i < Instructions.Length; ++i)
            {
                int key = i;
                var reg = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write,
                        writeCallback: (_, value) => {
                            Instructions[key] = (ushort)value;
                        },
                    name: "INSTR_MEM" + i);
                RegistersCollection.AddRegister(0x048 + i * 4, reg);
            }
            
            Registers.FSTAT.Define(this)
                .WithValueField(0, 4, FieldMode.Read, 
                    valueProviderCallback: _ => {
                        ulong ret = 0;
                        for (int i = 0; i < RxFifos.Length; ++i)
                        {
                            if (RxFifos[i].Count == 4)
                            {
                                ret |= (1ul << i);
                            }
                        }
                        return ret;
                    }, name: "RXFULL")
                .WithReservedBits(4, 4)
                .WithValueField(8, 4, FieldMode.Read,
                    valueProviderCallback: _ => {
                        ulong ret = 0;
                        for (int i = 0; i < RxFifos.Length; ++i)
                        {
                            if (RxFifos[i].Count == 0)
                            {
                                ret |= (1ul << i);
                            }
                        }
                        return ret;
                    }, name: "RXEMPTY")
                .WithReservedBits(12, 4)
                .WithValueField(16, 4, FieldMode.Read,
                    valueProviderCallback: _ => {
                        ulong ret = 0;
                        for (int i = 0; i < TxFifos.Length; ++i)
                        {
                            if (TxFifos[i].Count == 4)
                            {
                                ret |= (1ul << i);
                            }
                        }
                        return ret;
                    }, name: "TXFULL")
                .WithReservedBits(20, 4)
                .WithValueField(24, 4, FieldMode.Read,
                    valueProviderCallback: _ => {
                        ulong ret = 0;
                        for (int i = 0; i < TxFifos.Length; ++i)
                        {
                            if (TxFifos[i].Count == 0)
                            {
                                ret |= (1ul << i);
                            }
                        }
                        return ret;
                    }, name: "TXEMPTY")
                .WithReservedBits(28, 4);

        }
    }
}
