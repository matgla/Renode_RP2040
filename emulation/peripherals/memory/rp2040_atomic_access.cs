using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.Memory
{
    public class RP2040XorRegisterAlias : IDoubleWordPeripheral, IKnownSize, IMemory 
    {
        public long Size { get { return 0x1000; }}

        private Machine machine;
        private ulong address;
        public RP2040XorRegisterAlias(Machine machine, ulong originalAddress)
        {
            this.machine = machine;
            address = originalAddress;
        }

        public uint ReadDoubleWord(long offset)
        {
            // write-only
            return 0;
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
            ulong address = this.address + (ulong)offset;
            uint original = machine.SystemBus.ReadDoubleWord(address);
            machine.SystemBus.WriteDoubleWord(address, original ^ value);
        }

        public void Reset()
        {
        }
    }

    public class RP2040BitmaskSetRegisterAlias : IDoubleWordPeripheral, IKnownSize, IMemory
    {
        public long Size { get { return 0x1000; }}

        private Machine machine;
        private ulong address;
        public RP2040BitmaskSetRegisterAlias(Machine machine, ulong originalAddress)
        {
            this.machine = machine;
            address = originalAddress;
        }

        public uint ReadDoubleWord(long offset)
        {
            // write-only
            return 0;
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
            ulong address = this.address + (ulong)offset;
            uint original = machine.SystemBus.ReadDoubleWord(address);
            machine.SystemBus.WriteDoubleWord(address, original | value);
        }

        public void Reset()
        {
        }
    }
    public class RP2040BitmaskClearRegisterAlias : IDoubleWordPeripheral, IKnownSize, IMemory
    {
        public long Size { get { return 0x1000; }}

        private Machine machine;
        private ulong address;
        public RP2040BitmaskClearRegisterAlias(Machine machine, ulong originalAddress)
        {
            this.machine = machine;
            address = originalAddress;
        }

        public uint ReadDoubleWord(long offset)
        {
            // write-only
            return 0;
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
            ulong address = this.address + (ulong)offset;
            uint original = machine.SystemBus.ReadDoubleWord(address);
            machine.SystemBus.WriteDoubleWord(address, original & (~value));
        }

        public void Reset()
        {
        }
    }
}
