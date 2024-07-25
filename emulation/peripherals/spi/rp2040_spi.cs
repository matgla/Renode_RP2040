using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Peripherals.SPI
{
  public sealed class PL022 : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IWordPeripheral, IDoubleWordPeripheral, IBytePeripheral, IKnownSize
  {
    public long Size { get { return 0x1000; } }

    public PL022(IMachine machine) : base(machine)
    {
      rxBuffer = new CircularBuffer<ushort>(8);
      txBuffer = new CircularBuffer<ushort>(8);

      registers = new DoubleWordRegisterCollection(this);
      DefineRegisters();

      this._executionThread = machine.ObtainManagedThread(Step)
    }

    private void Step()
    {

    }

    public byte ReadByte(long offset)
    {
      return 0;
    }

    public void WriteByte(long offset, byte value)
    {
    }

    public ushort ReadWord(long offset)
    {
      return 0;
    }

    public void WriteWord(long offset, ushort value)
    {
    }

    public uint ReadDoubleWord(long offset)
    {
      return 0;
    }

    public void WriteDoubleWord(long offset, uint value)
    {
    }

    public override void Reset()
    {
    }

    public GPIO IRQ { get; }


    private uint HandleDataRead()
    {
      return 0;
    }

    private void HandleDataWrite(uint value)
    {
    }

    private void Update()
    {
    }

    private void RecalculateClockRate()
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
          if (txBuffer.Count < txBuffer.Capacity)
          {
            txBuffer.Enqueue((ushort)value);
          }
        }, name: "SSPDR_DATA");

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

    private byte dataSize;
    private byte frameFormat;
    private bool clockPolarity;
    private bool clockPhase;
    private byte clockRate;
    private bool loopbackMode;
    private bool synchronousSerialPort;
    private bool masterSlaveSelect;
    private bool slaveModeDisabled;

    private DoubleWordRegisterCollection registers;

    private readonly CircularBuffer<ushort> rxBuffer;
    private readonly CircularBuffer<ushort> txBuffer;

    // SPI is executed on thread since it must manage GPIO in realtime.
    // This is crucial for PIO <-> SPI interworking
    private IManagedThread _executionThread;
    private enum Registers
    {
      SSPCR0 = 0x0,
      SSPCR1 = 0x4,
      SSPDR = 0x8,
      SSPSR = 0xC,
      SSPCPSR = 0x10, // SPI_CRCPR
      SSPIMSC = 0x14, // SPI_RXCRCR
      SSPRIS = 0x18, // SPI_TXCRCR
      SSPMIS = 0x1C, // SPI_I2SCFGR
      SSPICR = 0x20, // SPI_I2SPR
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
  }
}
