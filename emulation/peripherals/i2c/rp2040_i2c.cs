using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.Bus;


namespace Antmicro.Renode.Peripherals.I2C
{

class RP2040I2C : SimpleContainer<II2CPeripheral>, II2CPeripheral, IDoubleWordPeripheral, IKnownSize
{
    public RP2040I2C()
    {

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
    public virtual uint ReadDoubleWordSet(long offset)
    {
        return RegistersCollection.Read(offset);
    }

    [ConnectionRegion("CLEAR")]
    public virtual uint ReadDoubleWordClear(long offset)
    {
        return RegistersCollection.Read(offset);
    }

    public long Size
    {
        get { return 0x1000; }
    }



};

}