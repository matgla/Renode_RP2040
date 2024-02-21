/*
 *   Copyright (c) 2024
 *   All rights reserved.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    class FifoStatus
    {
        public bool roe{get; set;}
        public bool wof{get; set;}
        public bool rdy{get; set;}
        public bool vld{get; set;}
    }

    class Divider
    {
        public ulong dividend{get; set;}
        public ulong divisor{get; set;}
        public ulong quotient{get; set;}
        public ulong remainder{get; set;}

        public bool ready{get; set;}
        public bool dirty{get; set;}

        public void CalculateSigned()
        {
            if (divisor != 0)
            {
                quotient = (ulong)((long)dividend/(long)divisor);
                remainder = (ulong)((long)dividend % (long)divisor);
            }
        }
        public void CalculateUnsigned()
        {
            if (divisor != 0)
            {
                quotient = dividend / divisor;
                remainder = dividend % divisor;
            }
        }
    }
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class RP2040SIO : BasicDoubleWordPeripheral, IKnownSize
    {
        private Queue<long>[] cpuFifo;
        private FifoStatus[] fifoStatus;
        private Divider[] divider;
        public long Size { get { return 0x1000; } }

        private enum Registers
        {
            CPUID = 0x0,
            FIFO_ST = 0x50,
            FIFO_WR = 0x54,
            FIFO_RD = 0x58,
            DIV_UDIVIDEND = 0x60,
            DIV_UDIVISOR = 0x64,
            DIV_SDIVIDEND = 0x68,
            DIV_SDIVISOR = 0x6c,
            DIV_QUOTIENT = 0x70,
            DIV_REMAINDER = 0x74,
            DIV_CSR = 0x78,
            SPINLOCK_0 = 0x100,
            SPINLOCK_1 = 0x104,
            SPINLOCK_2 = 0x108,
            SPINLOCK_3 = 0x10c,
            SPINLOCK_4 = 0x110,
            SPINLOCK_5 = 0x114,
            SPINLOCK_6 = 0x118,
            SPINLOCK_7 = 0x11c,
            SPINLOCK_8 = 0x120,
            SPINLOCK_9 = 0x124,
            SPINLOCK_10 = 0x128,
            SPINLOCK_11 = 0x12c,
            SPINLOCK_12 = 0x130,
            SPINLOCK_13 = 0x134,
            SPINLOCK_14 = 0x138,
            SPINLOCK_15 = 0x13c,
            SPINLOCK_16 = 0x140,
            SPINLOCK_17 = 0x144,
            SPINLOCK_18 = 0x148,
            SPINLOCK_19 = 0x14c,
            SPINLOCK_20 = 0x150,
            SPINLOCK_21 = 0x154,
            SPINLOCK_22 = 0x158,
            SPINLOCK_23 = 0x15c,
            SPINLOCK_24 = 0x160,
            SPINLOCK_25 = 0x164,
            SPINLOCK_26 = 0x168,
            SPINLOCK_27 = 0x16c,
            SPINLOCK_28 = 0x170,
            SPINLOCK_29 = 0x174,
            SPINLOCK_30 = 0x178,
            SPINLOCK_31 = 0x17c,
        }

        bool[] spinlocks;
        public RP2040SIO(Machine machine) : base(machine)
        {
            cpuFifo = new Queue<long>[2];
            fifoStatus = new FifoStatus[2];
            divider = new Divider[2];
            spinlocks = new bool[32];
            for (int i = 0; i < 32; ++i)
            {
                spinlocks[i] = false;
            }
            for (int i = 0; i < 2; ++i)
            {
                divider[i] = new Divider();
                cpuFifo[i] = new Queue<long>();
                fifoStatus[i] = new FifoStatus
                {
                    roe = false,
                    wof = false,
                    rdy = true,
                    vld = false
                };
            }

            DefineRegisters();
        }

        private void DefineRegisters()
        {
            Registers.CPUID.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    // how to determine callers cpu?
                    return cpuId == 1;
                }, name: "CPUID");
            Registers.FIFO_ST.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    return fifoStatus[cpuId].vld;
                }, name: "FIFO_ST_VLD")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    long otherCpu = Convert.ToInt64(cpuId == 0);
                    return fifoStatus[otherCpu].rdy;
                }, name: "FIFO_ST_RDY")
               .WithFlag(2, FieldMode.Read, valueProviderCallback: _ =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    long otherCpu = Convert.ToInt64(cpuId == 0);
                    return fifoStatus[otherCpu].wof;
                }, name: "FIFO_ST_WOF")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    return fifoStatus[cpuId].roe;
                }, name: "FIFO_ST_ROE");

            Registers.FIFO_RD.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);

                    if (cpuFifo[cpuId].Count != 0)
                    {
                        fifoStatus[cpuId].roe = false;
                        ulong ret = (ulong)cpuFifo[cpuId].Dequeue();
                        if (cpuFifo[cpuId].Count == 0)
                        {
                            fifoStatus[cpuId].vld = false;
                        }
                        fifoStatus[cpuId].rdy = true;
                        return ret;
                    }
                    else
                    {
                        fifoStatus[cpuId].roe = true;
                    }
                    return 0;
                }, name: "FIFO_RD");
            Registers.FIFO_WR.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    long otherCpu = Convert.ToInt64(cpuId == 0);

                    if (cpuFifo[otherCpu].Count < 7)
                    {
                        cpuFifo[otherCpu].Append((long)value);

                        fifoStatus[otherCpu].wof = false;
                        fifoStatus[otherCpu].vld = true;
                        if (cpuFifo[otherCpu].Count == 7)
                        {
                            fifoStatus[otherCpu].rdy = true;
                        }
                    }
                    else
                    {
                        fifoStatus[otherCpu].wof = true;
                    }
                }, name: "FIFO_WR");
            Registers.DIV_UDIVIDEND.Define(this)
                .WithValueField(0, 32, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    divider[cpuId].dividend = value;
                    divider[cpuId].dirty = true;
                    divider[cpuId].CalculateUnsigned();
                },
                valueProviderCallback: _ =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    return divider[cpuId].dividend;
                }, name: "DIV_UDIVIDEND");
            Registers.DIV_UDIVISOR.Define(this)
                .WithValueField(0, 32, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    divider[cpuId].divisor = value;
                    divider[cpuId].dirty = true;
                    divider[cpuId].CalculateUnsigned();
                },
                valueProviderCallback: _ =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    return divider[cpuId].divisor;
                }, name: "DIV_UDIVISOR");
            Registers.DIV_SDIVIDEND.Define(this)
                .WithValueField(0, 32, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    divider[cpuId].dividend = value;
                    divider[cpuId].dirty = true;
                    divider[cpuId].CalculateSigned();
                },
                valueProviderCallback: _ =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    return divider[cpuId].dividend;
                }, name: "DIV_SDIVIDEND");
            Registers.DIV_SDIVISOR.Define(this)
                .WithValueField(0, 32, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    divider[cpuId].divisor = value;
                    divider[cpuId].CalculateSigned();
                },
                valueProviderCallback: _ =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    return divider[cpuId].divisor;
                }, name: "DIV_SDIVISOR");
            Registers.DIV_QUOTIENT.Define(this)
                .WithValueField(0, 32, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    divider[cpuId].dirty = true;
                    divider[cpuId].quotient = value;
                },
                valueProviderCallback: _ =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    divider[cpuId].dirty = false;
                    return divider[cpuId].quotient;
                }, name: "DIV_QUOTIENT");
            Registers.DIV_REMAINDER.Define(this)
                .WithValueField(0, 32, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    divider[cpuId].dirty = true;
                    divider[cpuId].remainder = value;
                },
                valueProviderCallback: _ =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    return divider[cpuId].remainder;
                }, name: "DIV_REMAINDER");
            Registers.DIV_CSR.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    return divider[cpuId].ready;
                }, name: "DIV_CSR_READY")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ =>
                {
                    machine.SystemBus.TryGetCurrentCPUId(out var cpuId);
                    return divider[cpuId].dirty;
                }, name: "DIV_CSR_DIRTY");

            int spinlock_number = 0;
            foreach (Registers r in Enum.GetValues(typeof(Registers)))
            {
                if (r >= Registers.SPINLOCK_0 && r <= Registers.SPINLOCK_31)
                {
                    int id = spinlock_number;
                    r.Define(this)
                        .WithValueField(0, 32, FieldMode.Write | FieldMode.Read,
                            writeCallback: (_, value) =>
                            {
                                spinlocks[id] = false;
                            },
                            valueProviderCallback: _ =>
                            {
                                if (spinlocks[id] == false)
                                {
                                    spinlocks[id] = true;
                                    return (ulong)(1 << id);
                                }
                                return 0;
                            },
                            name: r.ToString());

                    spinlock_number++;
                }
            }
        }
    }
}
