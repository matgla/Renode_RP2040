using System;

using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities.Collections;
using Antmicro.Renode.Logging;
using IronPython.Modules;
using Antmicro.Renode.PlatformDescription.Syntax;
using System.Xml.Serialization;

namespace Antmicro.Renode.Peripherals.SPI
{
  // Slave mode is not yet supported
  public class RP2040XIPSSI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
  {
    public long Size { get { return 0x1000; } }

    public const ulong aliasSize = 0x1000;
    public const ulong xorAliasOffset = 0x1000;
    public const ulong setAliasOffset = 0x2000;
    public const ulong clearAliasOffset = 0x3000;
    public RP2040XIPSSI(IMachine machine, ulong address) : base(machine)
    {
      registers = new DoubleWordRegisterCollection(this);
      receiveBuffer = new CircularBuffer<UInt32>(36);
      machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + xorAliasOffset, aliasSize, "XOR"));
      machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + setAliasOffset, aliasSize, "SET"));
      machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + clearAliasOffset, aliasSize, "CLEAR"));
      DmaTransmitDreq = new GPIO();
      DmaStreamDreq = new GPIO();
      dreqThread = machine.ObtainManagedThread(ProcessDreq, 1);
      dreqThread.Stop(); 
      dreqThread.Frequency = 1000000; // just for now, maybe I should connect that to real clock frequency
      rxDmaEnabled = false;
      txDmaEnabled = false; 
      DefineRegisters();
    }

    void ProcessDreq()
    {
      DmaTransmitDreq.Toggle();
      DmaStreamDreq.Toggle();
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

    private ulong PeripheralDataRead()
    {
      lock (receiveBuffer)
      {
        if (receiveBuffer.TryDequeue(out var value))
        {
          return value;
        }
        return 0;
      }
    }

    private void PeripheralDataWrite(ulong data)
    {
      lock (receiveBuffer)
      {
        var peripheral = RegisteredPeripheral;
        if (peripheral != null)
        {
          byte readed = peripheral.Transmit((byte)data);
          receiveBuffer.Enqueue(readed);
        }
      }
    }

    private void DefineRegisters()
    {
      Registers.CTRLR0.Define(registers)
        .WithValueField(0, 4, name: "DFS")
        .WithValueField(4, 2, name: "FRF")
        .WithTaggedFlag("SCPH", 6)
        .WithTaggedFlag("SCPOL", 7)
        .WithValueField(8, 2, name: "TMOD")
        .WithTaggedFlag("SLV_OE", 10)
        .WithTaggedFlag("SRL", 11)
        .WithValueField(12, 4, name: "CFS")
        .WithValueField(16, 5, name: "DFS_32")
        .WithValueField(21, 2, name: "SPI_FRF")
        .WithReservedBits(23, 1)
        .WithTaggedFlag("SSTE", 24)
        .WithValueField(25, 7);

      Registers.CTRLR1.Define(registers)
        .WithValueField(0, 16, name: "NDF")
        .WithReservedBits(16, 16);

      Registers.SSIENR.Define(registers)
        .WithFlag(0, out ssiEnabled, name: "ENABLED")
        .WithReservedBits(1, 31);

      Registers.MWCR.Define(registers)
        .WithTaggedFlag("MWMOD", 0)
        .WithTaggedFlag("MDD", 1)
        .WithTaggedFlag("MHS", 2)
        .WithReservedBits(3, 29);

      Registers.SER.Define(registers)
        .WithFlag(0, out slaveEnabled, name: "CS")
        .WithReservedBits(1, 31);

      Registers.BAUDR.Define(registers)
        .WithValueField(0, 16, name: "SCKDV")
        .WithReservedBits(16, 16);

      Registers.TXFTLR.Define(registers)
        .WithValueField(0, 8, name: "TFT")
        .WithReservedBits(8, 24);

      Registers.RXFTLR.Define(registers)
        .WithValueField(0, 8, valueProviderCallback: _ => (ulong)receiveBuffer.Count,
            name: "RFT")
        .WithReservedBits(8, 24);

      Registers.TXFLR.Define(registers)
        .WithValueField(0, 8, valueProviderCallback: _ => 0, name: "TXFTL")
        .WithReservedBits(8, 24);

      Registers.RXFLR.Define(registers)
        .WithValueField(0, 8, valueProviderCallback: _ => 1, name: "RXFTL")
        .WithReservedBits(8, 24);

      Registers.SR.Define(registers)
        .WithTaggedFlag("BUSY", 0)
        .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => true,
            name: "TFNF")
        .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => true,
            name: "TFE")
        .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => true,
            name: "RFNE")
        .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => false,
            name: "RFF")
        .WithTaggedFlag("TXE", 5)
        .WithTaggedFlag("DCOL", 6)
        .WithReservedBits(7, 25);

      Registers.IMR.Define(registers)
              .WithTaggedFlag("TXEIM", 0)
              .WithTaggedFlag("TXOIM", 1)
              .WithTaggedFlag("RXUIM", 2)
              .WithTaggedFlag("RXOIM", 3)
              .WithTaggedFlag("RXFIM", 4)
              .WithTaggedFlag("MSTIM", 5)
              .WithReservedBits(6, 26);

      Registers.ISR.Define(registers)
        .WithTaggedFlag("TXEIS", 0)
        .WithTaggedFlag("TXOIS", 1)
        .WithTaggedFlag("RXUIS", 2)
        .WithTaggedFlag("RXOIS", 3)
        .WithTaggedFlag("RXFIS", 4)
        .WithTaggedFlag("MSTIS", 5)
        .WithReservedBits(6, 26);

      Registers.RISR.Define(registers)
        .WithTaggedFlag("TXEIR", 0)
        .WithTaggedFlag("TXOIR", 1)
        .WithTaggedFlag("RXUIR", 2)
        .WithTaggedFlag("RXOIR", 3)
        .WithTaggedFlag("RXFIR", 4)
        .WithTaggedFlag("MSTIR", 5)
        .WithReservedBits(6, 26);

      Registers.TXOICR.Define(registers)
        .WithTaggedFlag("TXOICR", 0)
        .WithReservedBits(1, 31);

      Registers.RXOICR.Define(registers)
        .WithTaggedFlag("RXOICR", 0)
        .WithReservedBits(1, 31);

      Registers.RXUICR.Define(registers)
        .WithTaggedFlag("RXUICR", 0)
        .WithReservedBits(1, 31);

      Registers.MSTICR.Define(registers)
        .WithTaggedFlag("MSTICR", 0)
        .WithReservedBits(1, 31);

      Registers.ICR.Define(registers)
        .WithTaggedFlag("ICR", 0)
        .WithReservedBits(1, 31);

      Registers.DMACR.Define(registers)
        .WithFlag(0, valueProviderCallback: _ => rxDmaEnabled, 
          writeCallback: (_, value) => {
            rxDmaEnabled = value;
          }, name: "RDMAE")
        .WithFlag(1, valueProviderCallback: _ => txDmaEnabled, 
          writeCallback: (_, value) => {
            if (value && !txDmaEnabled)
            {
              txDmaEnabled = false;
              dreqThread.Start();
            }
            if (!value && txDmaEnabled)
            {
              txDmaEnabled = true;
              dreqThread.Stop();
            }
          }, name: "TDMAE")
        .WithReservedBits(2, 30);

      Registers.DMATDLR.Define(registers)
        .WithValueField(0, 8, name: "DMATDLR")
        .WithReservedBits(8, 24);

      Registers.DMARDLR.Define(registers)
        .WithValueField(0, 8, name: "DMARDL")
        .WithReservedBits(8, 24);

      Registers.IDR.Define(registers)
        .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => idcode, name: "IDCODE");

      Registers.SSI_VERSION_ID.Define(registers)
        .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => ssi_comp_version, name: "SSI_COMP_VERSION");

      Registers.DR0.Define(registers)
        .WithValueField(0, 32, valueProviderCallback: _ => PeripheralDataRead(),
            writeCallback: (_, value) => PeripheralDataWrite(value),
            name: "DR0"
        );

      int dr = 1;
      foreach (Registers r in Enum.GetValues(typeof(Registers)))
      {
        if (r >= Registers.DR1 && r <= Registers.DR35)
        {
          int i = dr;
          r.Define(registers)
            .WithValueField(0, 32, name: "DR" + i);
          ++dr;
        }
      }

      Registers.RX_SAMPLE_DLY.Define(registers)
        .WithValueField(0, 8, name: "RSD")
        .WithReservedBits(8, 24);

      Registers.SPI_CTRLR0.Define(registers)
        .WithValueField(0, 2, name: "TRANS_TYPE")
        .WithValueField(2, 4, name: "ADDR_L")
        .WithReservedBits(6, 2)
        .WithValueField(8, 2, name: "INST_L")
        .WithReservedBits(10, 1)
        .WithValueField(11, 5, name: "WAIT_CYCLES")
        .WithTaggedFlag("SPI_DDR_EN", 16)
        .WithTaggedFlag("INST_DDR_EN", 17)
        .WithTaggedFlag("SPI_RXDS_EN", 18)
        .WithReservedBits(19, 5)
        .WithValueField(24, 8, name: "XIP_CMD");
    }
    private DoubleWordRegisterCollection registers;

    private uint idcode = 0x51535049;
    private uint ssi_comp_version = 0x3430412a;

    private CircularBuffer<UInt32> receiveBuffer;

    private IFlagRegisterField ssiEnabled;
    private IFlagRegisterField slaveEnabled;

    // I don't really have SSI simulation, so I can stub it with just timer right now
    private IManagedThread dreqThread;
    private bool rxDmaEnabled;
    private bool txDmaEnabled;
    public GPIO DmaTransmitDreq { get; }
    public GPIO DmaStreamDreq { get; }


    private enum Registers
    {
      CTRLR0 = 0x0,
      CTRLR1 = 0x4,
      SSIENR = 0x8,
      MWCR = 0xC,
      SER = 0x10,
      BAUDR = 0x14,
      TXFTLR = 0x18,
      RXFTLR = 0x1C,
      TXFLR = 0x20,
      RXFLR = 0x24,
      SR = 0x28,
      IMR = 0x2c,
      ISR = 0x30,
      RISR = 0x34,
      TXOICR = 0x38,
      RXOICR = 0x3c,
      RXUICR = 0x40,
      MSTICR = 0x44,
      ICR = 0x48,
      DMACR = 0x4c,
      DMATDLR = 0x50,
      DMARDLR = 0x54,
      IDR = 0x58,
      SSI_VERSION_ID = 0x5c,
      DR0 = 0x60,
      DR1 = 0x64,
      DR35 = 0xec,
      RX_SAMPLE_DLY = 0xf0,
      SPI_CTRLR0 = 0xf4,
      TXD_DRIVE_EDGE = 0xf8
    }
  }
}
