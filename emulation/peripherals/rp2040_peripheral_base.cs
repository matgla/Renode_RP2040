/**
 * basic_rp2040_peripheral.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */


using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using IronPython.Modules;

namespace Antmicro.Renode.Peripherals
{

    public interface IRP2040Peripheral : IKnownSize, IDoubleWordPeripheral
    {
        void WriteDoubleWordXor(long offset, uint value);
        void WriteDoubleWordSet(long offset, uint value);
        void WriteDoubleWordClear(long offset, uint value);
    }

    public abstract class RP2040PeripheralBase : BasicDoubleWordPeripheral, IBytePeripheral, IWordPeripheral, IRP2040Peripheral
    {
        public RP2040PeripheralBase(IMachine machine, ulong address) : base(machine)
        {
            // sysbus.Register(this, new BusRangeRegistration(new Antmicro.Renode.Core.Range(address, (ulong)Size)));
            sysbus.Register(this, new BusMultiRegistration(address + xorAliasOffset, aliasSize, "XOR"));
            sysbus.Register(this, new BusMultiRegistration(address + setAliasOffset, aliasSize, "SET"));
            sysbus.Register(this, new BusMultiRegistration(address + clearAliasOffset, aliasSize, "CLEAR"));
        }

        [ConnectionRegion("XOR")]
        public virtual void WriteDoubleWordXor(long offset, uint value)
        {
            RegistersCollection.Write(offset, RegistersCollection.Read(offset) ^ value);
        }

        [ConnectionRegion("SET")]
        public virtual void WriteDoubleWordSet(long offset, uint value)
        {
            RegistersCollection.Write(offset, RegistersCollection.Read(offset) | value);
        }

        [ConnectionRegion("CLEAR")]
        public virtual void WriteDoubleWordClear(long offset, uint value)
        {
            RegistersCollection.Write(offset, RegistersCollection.Read(offset) & (~value));
        }

        [ConnectionRegion("XOR")]
        public virtual uint ReadDoubleWordXor(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        [ConnectionRegion("SET")]
        public virtual uint WriteDoubleWordSet(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        [ConnectionRegion("CLEAR")]
        public virtual uint ReadDoubleWordClear(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public virtual byte ReadByte(long offset)
        {
            long alignedAddress = offset - (offset % 4);
            int shift = (int)(offset % 4) * 8;
            return (byte)(RegistersCollection.Read(alignedAddress) >> shift);
        }

        public virtual void WriteByte(long offset, byte value)
        {
            long alignedAddress = offset - (offset % 4);
            int shift = (int)(offset % 4) * 8;
            uint original = RegistersCollection.Read(alignedAddress) & ~(0xFFu << shift);
            RegistersCollection.Write(alignedAddress, original | ((uint)value << shift));
        }


        public virtual ushort ReadWord(long offset)
        {
            long alignedAddress = offset - (offset % 4);
            int shift = (int)(offset % 4) * 8;
            return (ushort)(RegistersCollection.Read(alignedAddress) >> shift);
        }

        public virtual void WriteWord(long offset, ushort value)
        {
            long alignedAddress = offset - (offset % 4);
            int shift = (int)(offset % 4) * 8;
            uint original = RegistersCollection.Read(alignedAddress) & ~(0xFFFFu << shift);
            RegistersCollection.Write(alignedAddress, original | ((uint)value << shift));
        }

        public long Size
        {
            get { return 0x1000; }
        }

        public const ulong aliasSize = 0x1000;
        public const ulong xorAliasOffset = 0x1000;
        public const ulong setAliasOffset = 0x2000;
        public const ulong clearAliasOffset = 0x3000;
    }

}

