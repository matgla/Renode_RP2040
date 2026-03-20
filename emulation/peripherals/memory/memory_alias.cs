using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Memory
{

    public class MemoryAlias : IDoubleWordPeripheral, IKnownSize, IMemory, IBytePeripheral, IWordPeripheral, IQuadWordPeripheral, IMultibyteWritePeripheral
    {
        public long Size { get; }


        public MemoryAlias(Machine machine, ulong address, long size)
        {
            this.machine = machine;
            Size = size;
            this.address = address;
        }

        public uint ReadDoubleWord(long offset)
        {
            uint data = machine.SystemBus.ReadDoubleWord(address + (ulong)offset);
            return data;
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
            machine.SystemBus.WriteDoubleWord(address + (ulong)offset, value);
        }

        public byte ReadByte(long offset)
        {
            return machine.SystemBus.ReadByte(address + (ulong)offset);
        }

        public void WriteByte(long offset, byte value)
        {
            machine.SystemBus.WriteByte(address + (ulong)offset, value);
        }

        public ushort ReadWord(long offset)
        {
            return machine.SystemBus.ReadWord(address + (ulong)offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            machine.SystemBus.WriteWord(address + (ulong)offset, value);
        }

        public ulong ReadQuadWord(long offset)
        {
            return machine.SystemBus.ReadQuadWord(address + (ulong)offset);
        }

        public void WriteQuadWord(long offset, ulong value)
        {
            machine.SystemBus.WriteQuadWord(address + (ulong)offset, value);
        }

        public byte[] ReadBytes(long offset, int count, IPeripheral context = null)
        {
            return machine.SystemBus.ReadBytes(address + (ulong)offset, count);
        }

        public void WriteBytes(long offset, byte[] data, int startingIndex, int count, IPeripheral context = null)
        {
            machine.SystemBus.WriteBytes(data, address + (ulong)offset, startingIndex, count);
        }

        public byte[] ReadBytes(long offset, int count, ICPU context)
        {
            return ReadBytes(offset, count, (IPeripheral)null);
        }

        public void WriteBytes(long offset, byte[] data, int startingIndex, int count, ICPU context)
        {
            WriteBytes(offset, data, startingIndex, count, (IPeripheral)null);
        }

        public virtual void Reset()
        {

        }

        private Machine machine;
        private ulong address;
    }
}