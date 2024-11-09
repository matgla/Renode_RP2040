/**
 * rp2xxx_dma.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;
using System;
using System.Collections.ObjectModel;

namespace Antmicro.Renode.Peripherals.DMA
{

  // RPXXXX requires custom DMA engine to process CRC calculation of transfers 
  // data for CRC may be lost after DMA peripheral to peripheral operation 
  // 
  // basically is copy of DmaEngine with CRC calculation injected 

  public class RPDMA : RP2040PeripheralBase, IGPIOReceiver, IKnownSize, INumberedGPIOOutput
  {
    private enum DREQ
    {
      PIO0_TX0 = 0,
      PIO0_TX1 = 1,
      PIO0_TX2 = 2,
      PIO0_TX3 = 3,
      PIO0_RX0 = 4,
      PIO0_RX1 = 5,
      PIO0_RX2 = 6,
      PIO0_RX3 = 7,
      PIO1_TX0 = 8,
      PIO1_TX1 = 9,
      PIO1_TX2 = 10,
      PIO1_TX3 = 11,
      PIO1_RX0 = 12,
      PIO1_RX1 = 13,
      PIO1_RX2 = 14,
      PIO1_RX3 = 15,
      SPI0_TX = 16,
      SPI0_RX = 17,
      SPI1_TX = 18,
      SPI1_RX = 19,
      UART0_TX = 20,
      UART0_RX = 21,
      UART1_TX = 22,
      UART1_RX = 23,
      PWM_WRAP0 = 24,
      PWM_WRAP1 = 25,
      PWM_WRAP2 = 26,
      PWM_WRAP3 = 27,
      PWM_WRAP4 = 28,
      PWM_WRAP5 = 29,
      PWM_WRAP6 = 30,
      PWM_WRAP7 = 31,
      I2C0_TX = 32,
      I2C0_RX = 33,
      I2C1_TX = 34,
      I2C1_RX = 35,
      ADC = 36,
      XIP_STREAM = 37,
      XIP_SSITX = 38,
      XIP_SSIRX = 39
    };
    public RPDMA(int numberOfChannels, IMachine machine, ulong address) : base(machine, address)
    {
      this.channels = new Channel[numberOfChannels];
      for (int i = 0; i < channels.Length; ++i)
      {
        this.channels[i] = new Channel(this, i);
      }
      engine = new RPDmaEngine(machine.GetSystemBus(this));
      this.numberOfChannels = numberOfChannels;
      DefineRegisters();
      this.numberOfDREQ = Enum.GetNames(typeof(DREQ)).Length;
      var irqs = new Dictionary<int, IGPIO>();
      //this.ExternalRequest = new GPIO[numberOfDREQ];
      for (int i = 0; i < numberOfDREQ; ++i)
      {
        irqs[i] = new GPIO();
      }
      Connections = new ReadOnlyDictionary<int, IGPIO>(irqs);
      Reset();
    }
    public override void Reset()
    {
      base.Reset();
      channelFinished = Enumerable.Repeat(true, numberOfChannels).ToArray();
      sniffData = 0;
      sniffEnable.Value = false;
      sniffChannel = 0;
      sniffCalcType.Value = CalculateType.Crc32;
      sniffOutInversed.Value = false;
      sniffByteSwap.Value = false;
      sniffOutReversed.Value = false;
      sniffData = 0;
      for (int i = 0; i < channels.Length; ++i)
      {
        channels[i].Reset();
      }
    }

    public void Trigger(int channelNumber)
    {
      channels[channelNumber].TriggerTransfer();
    }

    public override uint ReadDoubleWord(long offset)
    {
      if (offset < 0x400)
      {
        long id = offset / Channel.Size;
        return channels[id].ReadDoubleWord(offset % Channel.Size);
      }

      return RegistersCollection.Read(offset);
    }

    public override void WriteDoubleWord(long offset, uint value)
    {
      if (offset < 0x400)
      {
        long id = offset / Channel.Size;
        channels[id].WriteDoubleWord(offset % Channel.Size, value);
        return;
      }
      RegistersCollection.Write(offset, value);
    }

    public void OnGPIO(int number, bool value)
    {
      // find channel for DREQ
      foreach (var channel in channels)
      {
        if (channel.transferRequestSignal == number)
        {
          channel.PacedTransfer(number);
        }
      }
    }

    public IReadOnlyDictionary<int, IGPIO> Connections
    {
      get; private set;
    }

    private void DefineRegisters()
    {
      Registers.SNIFF_CTRL.Define(this)
        .WithFlag(0, out sniffEnable, name: "EN")
        .WithValueField(1, 4, valueProviderCallback: _ => sniffChannel,
          writeCallback: (_, value) => sniffChannel = (byte)value, name: "DMACH")
        .WithEnumField(5, 4, out sniffCalcType)
        .WithFlag(9, out sniffByteSwap, name: "BSWAP")
        .WithFlag(10, out sniffOutReversed, name: "OUT_REV")
        .WithFlag(11, out sniffOutInversed, name: "OUT_INV")
        .WithReservedBits(12, 20);

      Registers.SNIFF_DATA.Define(this)
        .WithValueField(0, 32, valueProviderCallback: _ =>
        {
          if (sniffCalcType.Value == CalculateType.Crc16CCITT
              || sniffCalcType.Value == CalculateType.Crc16CCITTReversed)
          {
            ushort crcout = (ushort)sniffData;
            if (sniffOutReversed.Value)
            {
              crcout = BitHelper.ReverseBits((ushort)sniffData);
            }
            if (sniffOutInversed.Value)
            {
              crcout = (ushort)~crcout;
            }
            if (sniffByteSwap.Value)
            {
              crcout = BitHelper.ReverseBytes(crcout);
            }
            return crcout;
          }

          uint crc = sniffData;
          if (sniffOutReversed.Value)
          {
            crc = BitHelper.ReverseBits(sniffData);
          };
          if (sniffOutInversed.Value)
          {
            crc = ~crc;
          }
          if (sniffByteSwap.Value)
          {
            crc = BitHelper.ReverseBytes(crc);
          }
          return crc;
        },
        writeCallback: (_, value) => sniffData = (uint)value, name: "SNIFF_DATA");

      Registers.MULTI_CHAN_TRIGGER.Define(this)
        .WithValueField(0, 16, valueProviderCallback: (_) => 0,
          writeCallback: (_, value) =>
          {
            for (int i = 0; i < 16; ++i)
            {
              if ((value & (1u << i)) == 1)
              {
                if (channels[i].Enabled)
                {
                  channels[i].TriggerTransfer();
                }
              }
            }
          }, name: "MULTI_CHAN_TRIGGER")
        .WithReservedBits(16, 16);

      Registers.INTR.Define(this)
        .WithValueField(0, 16, FieldMode.Read | FieldMode.Write, valueProviderCallback: (_) =>
        {
          uint irqs = 0;
          for (int i = 0; i < channels.Length; ++i)
          {
            irqs |= (channels[i].InterruptRaised == true ? 1u : 0u) << i;
          }
          return irqs;
        }, writeCallback: (_, value) =>
        {
          for (int i = 0; i < channels.Length; ++i)
          {
            if (((value >> i) & 0x01) == 1)
            {
              channels[i].InterruptRaised = false;
            }
          }
        }, name: "INTR");

      Registers.INTS0.Define(this)
        .WithValueField(0, 16, FieldMode.Read | FieldMode.Write, valueProviderCallback: (_) =>
        {
          uint irqs = 0;
          return irqs;
        }, writeCallback: (_, value) =>
        {
          for (int i = 0; i < channels.Length; ++i)
          {
            if (((value >> i) & 0x01) == 1)
            {
              channels[i].InterruptRaised = false;
            }
          }
        }, name: "INTS0");

      Registers.INTS1.Define(this)
        .WithValueField(0, 16, FieldMode.Read | FieldMode.Write, valueProviderCallback: (_) =>
        {
          uint irqs = 0;
          return irqs;
        }, writeCallback: (_, value) =>
        {
          for (int i = 0; i < channels.Length; ++i)
          {
            if (((value >> i) & 0x01) == 1)
            {
              channels[i].InterruptRaised = false;
            }
          }
        }, name: "INTS1");
    }


    private enum Registers
    {
      INTR = 0x400,
      INTE0 = 0x404,
      INTF0 = 0x408,
      INTS0 = 0x40c,
      INTE1 = 0x414,
      INTF1 = 0x418,
      INTS1 = 0x41c,
      TIMER0 = 0x420,
      TIMER1 = 0x424,
      TIMER2 = 0x428,
      TIMER3 = 0x42c,
      MULTI_CHAN_TRIGGER = 0x430,
      SNIFF_CTRL = 0x434,
      SNIFF_DATA = 0x438,
      FIFO_LEVELS = 0x440,
      CHAN_ABORT = 0x444,
      N_CHANNELS = 0x448,
    }

    private class Channel : BasicDoubleWordPeripheral
    {
      public Channel(RPDMA parent, int channelNumber) : base(parent.machine)
      {
        aliases = new Dictionary<long, Registers>();
        this.parent = parent;
        this.channelNumber = channelNumber;
        this.chainTo = channelNumber;
        DefineAliases();
        DefineRegisters();

        Reset();
      }

      public static long Size { get { return 0x40; } }


      private void DefineAliases()
      {
        aliases[0x00] = Registers.READ_ADDR;
        aliases[0x04] = Registers.WRITE_ADDR;
        aliases[0x08] = Registers.TRANS_COUNT;
        aliases[0x0c] = Registers.CTRL;
        aliases[0x10] = Registers.CTRL;
        aliases[0x14] = Registers.READ_ADDR;
        aliases[0x18] = Registers.WRITE_ADDR;
        aliases[0x1c] = Registers.TRANS_COUNT;
        aliases[0x20] = Registers.CTRL;
        aliases[0x24] = Registers.TRANS_COUNT;
        aliases[0x28] = Registers.READ_ADDR;
        aliases[0x2c] = Registers.WRITE_ADDR;
        aliases[0x30] = Registers.CTRL;
        aliases[0x34] = Registers.WRITE_ADDR;
        aliases[0x38] = Registers.TRANS_COUNT;
        aliases[0x3c] = Registers.READ_ADDR;
      }

      public override void Reset()
      {
        base.Reset();
        Enabled = false;
        readAddress = 0;
        writeAddress = 0;
        transferCount = 0;
        highPriority.Value = false;
        dataSize.Value = DataSize.Byte;
        incrementRead.Value = false;
        incrementWrite.Value = false;
        ringSize = 0;
        ringSelect.Value = false;
        chainTo = 0;
        transferRequestSignal = 0;
        irqQuiet.Value = false;
        byteSwap.Value = false;
        sniffEnable.Value = false;
        writeError.Value = false;
        readError.Value = false;
        ahbError.Value = false;
        InterruptRaised = false;
        transferCounter = 0;
      }

      private enum Registers
      {
        READ_ADDR = 0x00,
        WRITE_ADDR = 0x04,
        TRANS_COUNT = 0x08,
        CTRL = 0x0c,
        AL3_READ_ADDR_TRIG = 0x3c,
      }


      public override void WriteDoubleWord(long address, uint value)
      {
        RegistersCollection.Write((long)aliases[address], value);
        if ((int)(address & 0xf) == 0xc)
        {
          if (value == 0)
          {
            if (irqQuiet.Value == true)
            {
              InterruptRaised = true;
            }
          }
          else
          {
            TriggerTransfer();
          }
        }
      }

      public override uint ReadDoubleWord(long address)
      {
        return RegistersCollection.Read((long)aliases[address]);
      }

      public void TriggerTransfer()
      {
        if (!Enabled)
        {
          this.Log(LogLevel.Debug, "Transfer rejected, channel: " + channelNumber + " not enabled!");
          return;
        }
        if (transferRequestSignal != 0x3f)
        {
          this.Log(LogLevel.Debug, "Transfer waiting for trigger " + transferRequestSignal + " on channel " + channelNumber);
          transferCounter = 0;
          parent.channelFinished[channelNumber] = false;
          return;
        }
        if (transferRequestSignal >= 0x3b && transferRequestSignal <= 0x3e)
        {
          this.Log(LogLevel.Error, "TODO: DMA Timer triggers are not yet implemented!");
        }
        ProcessTransfer(false);
      }

      public void PacedTransfer(int dreq)
      {
        if (!Enabled)
        {
          this.Log(LogLevel.Debug, "Transfer rejected, channel: " + channelNumber + " not enabled!");
          return;
        }
        if (dreq != transferRequestSignal || transferCounter >= transferCount)
        {
          return;
        }
        ProcessTransfer(true);
      }

      private void ProcessTransfer(bool paced)
      {
        lock (parent.channelFinished)
        {
          RPXXXXDmaRequest request = CreateRequest(paced);
          parent.channelFinished[channelNumber] = false;
          if (sniffEnable.Value)
          {
            ChecksumRequest.Type checksumType = ChecksumRequest.Type.Crc32;
            switch (this.parent.sniffCalcType.Value)
            {
              case CalculateType.Crc32Reversed:
                {
                  checksumType = ChecksumRequest.Type.Crc32Reversed;
                  break;
                }
              case CalculateType.Sum:
                {
                  checksumType = ChecksumRequest.Type.Sum;
                  break;
                }
              case CalculateType.Crc16CCITT:
                {
                  checksumType = ChecksumRequest.Type.Crc16CCITT;
                  break;
                }
              case CalculateType.Crc16CCITTReversed:
                {
                  checksumType = ChecksumRequest.Type.Crc16CCITTReversed;
                  break;
                }
              case CalculateType.XORReduction:
                {
                  checksumType = ChecksumRequest.Type.XORReduction;
                  break;
                }
            }
            ChecksumRequest req = new ChecksumRequest()
            {
              type = checksumType,
              init = this.parent.sniffData,
            };
            var response = parent.engine.IssueCopy(request, null, req);
            this.parent.sniffData = response.crc;
          }
          else
          {
            var response = parent.engine.IssueCopy(request);
            if (!paced)
            {
              readAddress = (uint)response.response.ReadAddress.Value;
              writeAddress = (uint)response.response.WriteAddress.Value;
            }
          }

          if (paced)
          {
            transferCounter++;
            if (transferCounter == transferCount)
            {
              parent.channelFinished[channelNumber] = true;
            }
          }
          else
          {
            parent.channelFinished[channelNumber] = true;
          }
          if (!irqQuiet.Value)
          {
            if (parent.channelFinished[channelNumber])
            {
              parent.machine.LocalTimeSource.ExecuteInNearestSyncedState(_ => InterruptRaised = true);
            }
          }
          if (chainTo != channelNumber && parent.channelFinished[channelNumber])
          {
            parent.machine.LocalTimeSource.ExecuteInNearestSyncedState(_ => this.parent.Trigger(chainTo));
          }
        }
      }
      private RPXXXXDmaRequest CreateRequest(bool paced = false)
      {
        TransferType transferType = TransferType.Byte;
        switch (dataSize.Value)
        {
          case DataSize.HalfWord:
            {
              transferType = TransferType.Word;
              break;
            }

          case DataSize.Word:
            {
              transferType = TransferType.DoubleWord;
              break;
            }
        }
        int size = (int)transferType;
        if (!paced)
        {
          size *= (int)transferCount;
          transferCounter = 0;
        }
        var request = new Request(readAddress, writeAddress, size, transferType, transferType, incrementRead.Value, incrementWrite.Value);
        return new RPXXXXDmaRequest(request, ringSize == 0 ? 0 : 1 << ringSize, ringSelect.Value, transferCounter);
      }

      private void DefineRegisters()
      {
        Registers.READ_ADDR.Define(this)
          .WithValueField(0, 32, valueProviderCallback: _ => readAddress,
              writeCallback: (_, value) => readAddress = (uint)value);

        Registers.WRITE_ADDR.Define(this)
          .WithValueField(0, 32, valueProviderCallback: _ => writeAddress,
              writeCallback: (_, value) => writeAddress = (uint)value);

        Registers.TRANS_COUNT.Define(this)
          .WithValueField(0, 32, valueProviderCallback: _ => transferCount,
              writeCallback: (_, value) =>
              {
                transferCount = (uint)value;
                transferCounter = 0;
              });

        Registers.CTRL.Define(this)
          .WithFlag(0, valueProviderCallback: _ => Enabled,
            writeCallback: (_, value) => Enabled = value, name: "EN")
          .WithFlag(1, out highPriority, name: "HIGH_PRIORITY")
          .WithEnumField(2, 2, out dataSize, name: "DATA_SIZE")
          .WithFlag(4, out incrementRead, name: "INCR_READ")
          .WithFlag(5, out incrementWrite, name: "INCR_WRITE")
          .WithValueField(6, 4, valueProviderCallback: _ => (ulong)ringSize,
            writeCallback: (_, value) => ringSize = (int)value, name: "RING_SIZE")
          .WithFlag(10, out ringSelect, name: "RING_SEL")
          .WithValueField(11, 4, valueProviderCallback: _ => (ulong)chainTo,
            writeCallback: (_, value) => chainTo = (int)value, name: "CHAIN_TO")
          .WithValueField(15, 6, valueProviderCallback: _ => (ulong)transferRequestSignal,
            writeCallback: (_, value) =>
            {
              transferRequestSignal = (int)value;
            }, name: "TREQ_SEL")
          .WithFlag(21, out irqQuiet, name: "IRQ_QUIET")
          .WithFlag(22, out byteSwap, name: "BSWAP")
          .WithFlag(23, out sniffEnable, name: "SNIFF_EN")
          .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => !parent.channelFinished[channelNumber])
          .WithReservedBits(25, 4)
          .WithFlag(29, out writeError, FieldMode.Read | FieldMode.WriteOneToClear, name: "WRITE_ERROR")
          .WithFlag(30, out readError, FieldMode.Read | FieldMode.WriteOneToClear, name: "READ_ERROR")
          .WithFlag(31, out ahbError, FieldMode.Read | FieldMode.WriteOneToClear, name: "AHB_ERROR");
      }

      //public GPIO IRQ { get; private set; }
      public bool Enabled { get; private set; }

      private uint readAddress;
      private uint writeAddress;
      private uint transferCount;
      private IFlagRegisterField highPriority;
      private enum DataSize
      {
        Byte = 0,
        HalfWord = 1,
        Word = 2
      };
      private IEnumRegisterField<DataSize> dataSize;
      private IFlagRegisterField incrementRead;
      private IFlagRegisterField incrementWrite;
      private int ringSize;
      private IFlagRegisterField ringSelect;
      private int chainTo;
      public int transferRequestSignal;
      private IFlagRegisterField irqQuiet;
      private IFlagRegisterField byteSwap;
      private IFlagRegisterField sniffEnable;
      private IFlagRegisterField writeError;
      private IFlagRegisterField readError;
      private IFlagRegisterField ahbError;
      public bool InterruptRaised;
      private Dictionary<long, Registers> aliases;
      private RPDMA parent;
      private int channelNumber;

      private int transferCounter;

    }

    private readonly Channel[] channels;
    private int numberOfDREQ;
    private RPDmaEngine engine;
    private int numberOfChannels;
    private bool[] channelFinished;
    private IFlagRegisterField sniffEnable;
    private byte sniffChannel;
    private enum CalculateType
    {
      Crc32 = 0,
      Crc32Reversed = 1,
      Crc16CCITT = 2,
      Crc16CCITTReversed = 3,
      XORReduction = 0xe,
      Sum = 0xf
    };
    private IEnumRegisterField<CalculateType> sniffCalcType;
    private IFlagRegisterField sniffOutInversed;
    private IFlagRegisterField sniffByteSwap;
    private IFlagRegisterField sniffOutReversed;
    private uint sniffData;
  }

}
