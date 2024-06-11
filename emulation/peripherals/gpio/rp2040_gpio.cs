using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.GPIOPort
{

[AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
public class RP2040GPIO: BaseGPIOPort, IDoubleWordPeripheral, IGPIOReceiver, IKnownSize
{
    public RP2040GPIO(IMachine machine) : base(machine, NumberOfPins)
    {
        registers = CreateRegisters();
        this.Log(LogLevel.Error, "Construction");
        Reset();
    }

    public long Size { get { return 0x1000; }}

    private const int NumberOfPins = 29;
    
    private DoubleWordRegisterCollection CreateRegisters()
    {
        var registersMap = new Dictionary<long, DoubleWordRegister>();

        for (int i = 0; i < NumberOfPins; ++i)
        {
            registersMap[i * 8] = new DoubleWordRegister(this)
                .WithValueFields(0, 4, 8, name: "AA",
                    writeCallback: (x, _, val) => {},
                    valueProviderCallback: (x, _) => {return 0;} 
                    );
        }
        
        return new DoubleWordRegisterCollection(this, registersMap);
    }

    private enum Registers
    {
        Mode                  = 0x00, //GPIOx_MODE    Mode register
        OutputType            = 0x04, //GPIOx_OTYPER  Output type register
        OutputSpeed           = 0x08, //GPIOx_OSPEEDR Output speed register
        PullUpPullDown        = 0x0C, //GPIOx_PUPDR   Pull-up/pull-down register
        InputData             = 0x10, //GPIOx_IDR     Input data register
        OutputData            = 0x14, //GPIOx_ODR     Output data register
        BitSet                = 0x18, //GPIOx_BSRR    Bit set/reset register
        ConfigurationLock     = 0x1C, //GPIOx_LCKR    Configuration lock register
        AlternateFunctionLow  = 0x20, //GPIOx_AFRL    Alternate function low register
        AlternateFunctionHigh = 0x24, //GPIOx_AFRH    Alternate function high register
        BitReset              = 0x28, //GPIOx_BRR     Bit reset register
    }

    public uint ReadDoubleWord(long offset)
    {
        return registers.Read(offset);
    }

    public void WriteDoubleWord(long offset, uint value)
    {
        registers.Write(offset, value);
    }

    public override void OnGPIO(int number, bool value)
    {
        this.Log(LogLevel.Error, "GPIO " + number + " -> " + value);
        base.OnGPIO(number, value);
    }

    private void WritePin(int number, bool value)
    {
        this.Log(LogLevel.Error, "WritingPin " + number + " -> " + value);
        State[number] = value;
        Connections[number].Set(value); 
    }

    private readonly DoubleWordRegisterCollection registers;

}

}
