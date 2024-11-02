using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Memory
{

    public class MemoryAlias : IDoubleWordPeripheral, IKnownSize, IMemory
    {
        public long Size { get; }

        private Machine machine;
        private ulong address;
        public MemoryAlias(Machine machine, ulong address, long size)
        {
            this.machine = machine;
            Size = size;
            this.address = address;
        }

        public uint ReadDoubleWord(long offset)
        {
            uint data = machine.SystemBus.ReadDoubleWord((ulong)offset);
            this.Log(LogLevel.Error, "Reading from: " + offset + ", data: " + data);
            return data;
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
            machine.SystemBus.WriteDoubleWord(address, value);
        }

        public virtual void Reset()
        {

        }
    }
}