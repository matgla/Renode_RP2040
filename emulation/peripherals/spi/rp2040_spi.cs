using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities.Collections;
using Antmicro.Renode.Peripherals.GPIOPort;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.SPI
{
  // Slave mode is not yet supported
  public class PL022 : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize, IGPIOReceiver
  {
    public long Size { get { return 0x1000; } }

    public const ulong aliasSize = 0x1000;
    public const ulong xorAliasOffset = 0x1000;
    public const ulong setAliasOffset = 0x2000;
    public const ulong clearAliasOffset = 0x3000;

    public PL022(IMachine machine, RP2040GPIO gpio, int id, RP2040Clocks clocks, ulong address) : base(machine)
    {
      this.id = id;
      this.gpio = gpio;
      this.txPins = new List<int>();
      this.rxPins = new List<int>();
      this.clockPins = new List<int>();
      this.csPins = new List<int>();

      rxBuffer = new CircularBuffer<ushort>(8);
      txBuffer = new CircularBuffer<ushort>(8);

      registers = new DoubleWordRegisterCollection(this);
      DefineRegisters();
      dataSize = 0;
      frameFormat = 0;
      clockPolarity = false;
      clockPhase = false;
      clockRate = 0;
      loopbackMode = false;
      synchronousSerialPort = false;
      masterSlaveSelect = false;
      slaveModeDisabled = false;
      running = false;
      clockPrescaleDivisor = 2;
      periFrequency = clocks.PeripheralClockFrequency;
      steps = 0;
      this._executionThread = machine.ObtainManagedThread(Step, 1);
      this.clocks = clocks;
      RecalculateClockRate();
      this.gpio.SubscribeOnFunctionChange(OnGpioFunctionSelect);
      clocks.OnPeripheralChange(UpdateFrequency);
      transmitCounter = 16;
      machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + xorAliasOffset, aliasSize, "XOR"));
      machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + setAliasOffset, aliasSize, "SET"));
      machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + clearAliasOffset, aliasSize, "CLEAR"));
    }


    [ConnectionRegion("XOR")]
    public virtual void WriteDoubleWordXor(long offset, uint value)
    {
      registers.Write(offset, registers.Read(offset) ^ value);
    }

    [ConnectionRegion("SET")]
    public virtual void WriteDoubleWordSet(long offset, uint value)
    {
      registers.Write(offset, registers.Read(offset) | value);
    }

    [ConnectionRegion("CLEAR")]
    public virtual void WriteDoubleWordClear(long offset, uint value)
    {
      registers.Write(offset, registers.Read(offset) & (~value));
    }

    [ConnectionRegion("XOR")]
    public virtual uint ReadDoubleWordXor(long offset)
    {
      return registers.Read(offset);
    }

    [ConnectionRegion("SET")]
    public virtual uint ReadDoubleWordSet(long offset)
    {
      return registers.Read(offset);
    }

    [ConnectionRegion("CLEAR")]
    public virtual uint ReadDoubleWordClear(long offset)
    {
      return registers.Read(offset);
    }


    private void UpdateFrequency(long baseFrequency)
    {
      periFrequency = (ulong)baseFrequency;
      RecalculateClockRate();
    }

    private void RecalculateClockRate()
    {
      uint newFrequency = (uint)((decimal)periFrequency / (clockPrescaleDivisor * (1 + clockRate)));
      if (newFrequency == 0)
      {
        newFrequency = 1;
      }
      if (newFrequency != this._executionThread.Frequency)
      {
        this._executionThread.Frequency = newFrequency;
        this.Log(LogLevel.Debug, "SPI" + id + ": Changed frequency to: " + newFrequency);
        steps = clocks.SystemClockFrequency / newFrequency;
      }
    }

    private void OnGpioFunctionSelect(int pin, RP2040GPIO.GpioFunction function)
    {
      if (id == 0)
      {
        switch (function)
        {
          case RP2040GPIO.GpioFunction.SPI0_TX:
            {
              txPins.Add(pin);
              return;
            }
          case RP2040GPIO.GpioFunction.SPI0_RX:
            {
              rxPins.Add(pin);
              return;
            }
          case RP2040GPIO.GpioFunction.SPI0_CSN:
            {
              csPins.Add(pin);
              return;
            }
          case RP2040GPIO.GpioFunction.SPI0_SCK:
            {
              clockPins.Add(pin);
              return;
            }
          case RP2040GPIO.GpioFunction.NONE:
            {
              txPins.Remove(pin);
              rxPins.Remove(pin);
              clockPins.Remove(pin);
              csPins.Remove(pin);
              return;
            }
        }
      }
      else if (id == 1)
      {
        switch (function)
        {
          case RP2040GPIO.GpioFunction.SPI1_TX:
            {
              txPins.Add(pin);
              return;
            }
          case RP2040GPIO.GpioFunction.SPI1_RX:
            {
              rxPins.Add(pin);
              return;
            }
          case RP2040GPIO.GpioFunction.SPI1_CSN:
            {
              csPins.Add(pin);
              return;
            }
          case RP2040GPIO.GpioFunction.SPI1_SCK:
            {
              clockPins.Add(pin);
              return;
            }
          case RP2040GPIO.GpioFunction.NONE:
            {
              txPins.Remove(pin);
              rxPins.Remove(pin);
              clockPins.Remove(pin);
              csPins.Remove(pin);
              return;
            }
        }
      }
    }

    public void OnGPIO(int number, bool value)
    {

    }

    private void SetMultiplePins(List<int> pins, bool state)
    {
      foreach (int pin in pins)
      {
        this.gpio.WritePin(pin, state);
      }
    }

    private bool ReadMultiplePins(List<int> pins, bool doOr = false)
    {
      if (pins.Count == 0)
      {
        return false;
      }

      if (!doOr)
      {
        return this.gpio.GetGpioState((uint)pins[0]);
      }

      foreach (int pin in pins)
      {
        if (this.gpio.GetGpioState((uint)pin))
        {
          return true;
        }
      }
      return false;
    }

    private void Step()
    {
      if (transmitCounter > dataSize - 1)
      {
        transmitCounter = 0;
        rxBuffer.Enqueue(receiveData);
        receiveData = 0;
        if (txBuffer.Count != 0)
        {
          txBuffer.TryDequeue(out transmitData);
        }
        else
        {
          transmitData = 0;

          SetMultiplePins(txPins, false);
          SetMultiplePins(clockPins, false);
          _executionThread.Stop();
          running = false;
        }
      }

      bool clockWasHigh = ReadMultiplePins(clockPins);
      SetMultiplePins(clockPins, !clockWasHigh);
      if (!clockWasHigh)
      {
        SetMultiplePins(txPins, Convert.ToBoolean((transmitData >> (dataSize - 1 - transmitCounter)) & 1));
        receiveData = (ushort)((receiveData << 1) | Convert.ToUInt16(ReadMultiplePins(rxPins)));
        transmitCounter += 1;
      }

      gpio.ReevaluatePio((uint)steps);
    }

    public uint ReadDoubleWord(long offset)
    {
      return registers.Read(offset);
    }

    public void WriteDoubleWord(long offset, uint value)
    {
      registers.Write(offset, value);
    }

    public override void Reset()
    {
    }

    private void DefineRegisters()
    {
      Registers.SSPCR0.Define(registers)
        .WithValueField(0, 4, valueProviderCallback: _ => (ulong)(dataSize - 1),
          writeCallback: (_, value) => dataSize = (byte)(value + 1), name: "SSPCR0_DSS")
        .WithValueField(4, 2, valueProviderCallback: _ => (ulong)frameFormat,
          writeCallback: (_, value) => frameFormat = (byte)value, name: "SSPCR0_FRF")
        .WithFlag(6, valueProviderCallback: _ => clockPolarity,
          writeCallback: (_, value) => clockPolarity = (bool)value, name: "SSPCR0_SPO")
        .WithFlag(7, valueProviderCallback: _ => clockPhase,
          writeCallback: (_, value) => clockPhase = (bool)value, name: "SSPCR0_SPH")
        .WithValueField(8, 8, valueProviderCallback: _ => (ulong)clockRate,
          writeCallback: (_, value) =>
          {
            clockRate = (byte)value;
            RecalculateClockRate();
          }, name: "SSPCR0_SCR");

      Registers.SSPCR1.Define(registers)
        .WithFlag(0, valueProviderCallback: _ => loopbackMode,
          writeCallback: (_, value) => loopbackMode = value, name: "SSPCR1_LBM")
        .WithFlag(1, valueProviderCallback: _ => synchronousSerialPort,
          writeCallback: (_, value) => synchronousSerialPort = value, name: "SSPCR1_SSE")
        .WithFlag(2, valueProviderCallback: _ => masterSlaveSelect,
          writeCallback: (_, value) => masterSlaveSelect = value, name: "SSPCR1_MS")
        .WithFlag(3, valueProviderCallback: _ => slaveModeDisabled,
          writeCallback: (_, value) => slaveModeDisabled = value, name: "SSPCR1_SOD")
        .WithReservedBits(4, 28);

      Registers.SSPDR.Define(registers)
        .WithValueField(0, 16, valueProviderCallback: _ =>
        {
          if (rxBuffer.Count < rxBuffer.Capacity)
          {
            ushort ret;
            rxBuffer.TryDequeue(out ret);
            return ret;
          }
          return 0;
        }, writeCallback: (_, value) =>
        {
          Logger.Log(LogLevel.Noisy, "SPI" + id + ": Adding to queue: " + value);
          if (txBuffer.Count < txBuffer.Capacity)
          {
            txBuffer.Enqueue((ushort)value);
            if (!running)
            {
              running = true;
              _executionThread.Start();
            }
          }
        }, name: "SSPDR_DATA");

      Registers.SSPSR.Define(registers)
        .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => txBuffer.Count == 0, name: "SSPSR_TFE")
        .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => txBuffer.Count != txBuffer.Capacity, name: "SSPSR_TNF")
        .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => rxBuffer.Count == 0, name: "SSPSR_RNE")
        .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => rxBuffer.Count != rxBuffer.Capacity, name: "SSPSR_RFF")
        .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => running, name: "SSPSR_BSY");

      Registers.SSPCPSR.Define(registers)
        .WithValueField(0, 8, valueProviderCallback: _ => clockPrescaleDivisor,
          writeCallback: (_, value) =>
          {
            clockPrescaleDivisor = (byte)value;
            RecalculateClockRate();
          }, name: "SSPCPSR_CPSDVSR");

      Registers.SSPPERIPHID0.Define(registers)
        .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0x22, name: "SSPPERIPHID0_PARTNUMBER0")
        .WithReservedBits(8, 24);
      Registers.SSPPERIPHID1.Define(registers)
            .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => 0x00, name: "SSPPERIPHID1_DESIGNER0")
            .WithValueField(4, 4, FieldMode.Read, valueProviderCallback: _ => 0x01, name: "SSPPERIPHID1_PARTNUMBER1}")
            .WithReservedBits(8, 24);
      Registers.SSPPERIPHID2.Define(registers)
            .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => 0x04, name: "SSPPERIPHID2_DESIGNER1")
            .WithValueField(4, 4, FieldMode.Read, valueProviderCallback: _ => 0x03, name: "SSPPERIPHID2_REVISION}")
            .WithReservedBits(8, 24);
      Registers.SSPPERIPHID3.Define(registers)
            .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0x00, name: "SSPPERIPHID3")
            .WithReservedBits(8, 24);

      Registers.SSPPCELLID0.Define(registers)
            .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0x0d, name: "SSPCELLID0")
            .WithReservedBits(8, 24);
      Registers.SSPPCELLID1.Define(registers)
            .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0xf0, name: "SSPCELLID1")
            .WithReservedBits(8, 24);
      Registers.SSPPCELLID2.Define(registers)
            .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0x05, name: "SSPCELLID2")
            .WithReservedBits(8, 24);
      Registers.SSPPCELLID3.Define(registers)
            .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0xb1, name: "SSPCELLID3")
            .WithReservedBits(8, 24);
    }


    public IGPIO[] IRQs { get; private set; }

    private RP2040GPIO gpio;
    private List<int> txPins;
    private List<int> rxPins;
    private List<int> clockPins;
    private List<int> csPins;
    private int id;

    private byte dataSize;
    private byte frameFormat;
    private bool clockPolarity;
    private bool clockPhase;
    private byte clockRate;
    private bool loopbackMode;
    private bool synchronousSerialPort;
    private bool masterSlaveSelect;
    private bool slaveModeDisabled;
    private bool running;
    private byte clockPrescaleDivisor;

    private DoubleWordRegisterCollection registers;

    private readonly CircularBuffer<ushort> rxBuffer;
    private readonly CircularBuffer<ushort> txBuffer;

    // SPI is executed on thread since it must manage GPIO in realtime.
    // This is crucial for PIO <-> SPI interworking
    private IManagedThread _executionThread;
    private byte transmitCounter;
    private ushort transmitData;
    private ushort receiveData;
    private enum Registers
    {
      SSPCR0 = 0x0,
      SSPCR1 = 0x4,
      SSPDR = 0x8,
      SSPSR = 0xC,
      SSPCPSR = 0x10,
      SSPIMSC = 0x14,
      SSPRIS = 0x18,
      SSPMIS = 0x1C,
      SSPICR = 0x20,
      SSPDMACR = 0x24,
      SSPPERIPHID0 = 0xfe0,
      SSPPERIPHID1 = 0xfe4,
      SSPPERIPHID2 = 0xfe8,
      SSPPERIPHID3 = 0xfec,
      SSPPCELLID0 = 0xff0,
      SSPPCELLID1 = 0xff4,
      SSPPCELLID2 = 0xff8,
      SSPPCELLID3 = 0xffc
    }
    private ulong steps;
    private ulong periFrequency;
    private RP2040Clocks clocks;
  }
}
