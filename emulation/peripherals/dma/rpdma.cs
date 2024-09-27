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


namespace Antmicro.Renode.Peripherals.DMA
{

  // RPXXXX requires custom DMA engine to process CRC calculation of transfers 
  // data for CRC may be lost after DMA peripheral to peripheral operation 
  // 
  // basically is copy of DmaEngine with CRC calculation injected 

  public class RPDMA : IDoubleWordPeripheral, IGPIOReceiver, IKnownSize, INumberedGPIOOutput
  {
    public RPDMA(int numberOfChannels, IMachine machine)
    {
      this.channels = new Channel[numberOfChannels];
      for (int i = 0; i < channels.Length; ++i)
      {
        this.channels[i] = new Channel(this, i);
      }
      this.machine = machine;
      engine = new RPDmaEngine(machine.GetSystemBus(this));
      this.numberOfChannels = numberOfChannels;
      registers = CreateRegisters();
      Reset();
    }

    public void Reset()
    {
      channelFinished = Enumerable.Repeat<bool>(true, numberOfChannels).ToArray();
      sniffData = 0;
    }

    public long Size { get { return 0x1000; } }

    public uint ReadDoubleWord(long offset)
    {
      if (offset < 0x400)
      {
        long id = offset / Channel.Size;
        return channels[id].ReadDoubleWord(offset % Channel.Size);
      }

      return registers.Read(offset);
    }

    public void WriteDoubleWord(long offset, uint value)
    {
      if (offset < 0x400)
      {
        long id = offset / Channel.Size;
        this.Log(LogLevel.Error, "Reading id: " + id);
        channels[id].WriteDoubleWord(offset % Channel.Size, value);
        return;
      }
      registers.Write(offset, value);
    }

    public void OnGPIO(int number, bool value)
    {
      if (number < 0 || number >= channels.Length)
      {
        this.Log(LogLevel.Error, "Triggered non-existing DMA channel number: {0}. Maximum value is {1}", number, channels.Length);
        return;
      }

      if (value)
      {
        this.Log(LogLevel.Debug, "DMA peripheral request on stream {0} {1}", number, value);
        if (channels[number].Enabled)
        {
          channels[number].TriggerTransfer();
        }
        else
        {
          this.Log(LogLevel.Warning, "DMA peripheral request on stream {0} ignored", number);
        }
      }
    }

    public IReadOnlyDictionary<int, IGPIO> Connections
    {
      get
      {
        var i = 0;
        return channels.ToDictionary(x => i++, y => (IGPIO)y.IRQ);
      }
    }

    private DoubleWordRegisterCollection CreateRegisters()
    {
      var registersMap = new Dictionary<long, DoubleWordRegister>();

      registersMap[(long)Registers.SNIFF_CTRL] = new DoubleWordRegister(this)
        .WithFlag(0, out sniffEnable, name: "EN")
        .WithValueField(1, 4, valueProviderCallback: _ => sniffChannel,
          writeCallback: (_, value) => sniffChannel = (byte)value, name: "DMACH")
        .WithEnumField(5, 4, out sniffCalcType)
        .WithFlag(9, out sniffByteSwap, name: "BSWAP")
        .WithFlag(10, out sniffOutReversed, name: "OUT_REV")
        .WithFlag(11, out sniffOutInversed, name: "OUT_INV")
        .WithReservedBits(12, 20);

      registersMap[(long)Registers.SNIFF_DATA] = new DoubleWordRegister(this)
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

      return new DoubleWordRegisterCollection(this, registersMap);
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

    private class Channel : IPeripheral
    {
      public Channel(RPDMA parent, int channelNumber)
      {
        aliases = new Dictionary<long, Registers>();
        this.parent = parent;
        this.channelNumber = channelNumber;
        IRQ = new GPIO();
        DefineAliases();
        registers = CreateRegisters();
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

      public void Reset()
      {
      }

      private enum Registers
      {
        READ_ADDR = 0x00,
        WRITE_ADDR = 0x04,
        TRANS_COUNT = 0x08,
        CTRL = 0x0c,
        AL3_READ_ADDR_TRIG = 0x3c,
      }


      public void WriteDoubleWord(long address, uint value)
      {
        registers.Write((long)aliases[address], value);
        this.Log(LogLevel.Error, "Writing at: " + address.ToString("X") + " -> " + value.ToString("x"));
        if ((int)(address & 0xf) == 0xc)
        {
          TriggerTransfer();
        }
      }

      public uint ReadDoubleWord(long address)
      {
        return registers.Read((long)aliases[address]);
      }

      public void TriggerTransfer()
      {
        this.Log(LogLevel.Error, "Starting");
        if (!Enabled)
        {
          this.Log(LogLevel.Debug, "Transfer rejected, channel: " + channelNumber + " not enabled!");
          return;
        }
        Request request = CreateRequest();
        if (request.Size > 0)
        {
          lock (parent.channelFinished)
          {
            parent.channelFinished[channelNumber] = false;
            this.Log(LogLevel.Error, "Trigger copy");
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
              parent.engine.IssueCopy(request);
            }
            parent.channelFinished[channelNumber] = true;
            if (!irqQuiet.Value)
            {
              parent.machine.LocalTimeSource.ExecuteInNearestSyncedState(_ => IRQ.Set());
            }
          }
        }
      }

      private Request CreateRequest()
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
              transferType = TransferType.QuadWord;
              break;
            }
        }
        return new Request(readAddress, writeAddress, (int)transferCount, transferType, transferType, incrementRead.Value, incrementWrite.Value);
      }

      private DoubleWordRegisterCollection CreateRegisters()
      {
        var registersMap = new Dictionary<long, DoubleWordRegister>();

        registersMap[(long)Registers.READ_ADDR] = new DoubleWordRegister(this)
          .WithValueField(0, 32, valueProviderCallback: _ => readAddress,
              writeCallback: (_, value) => readAddress = (uint)value);

        registersMap[(long)Registers.WRITE_ADDR] = new DoubleWordRegister(this)
          .WithValueField(0, 32, valueProviderCallback: _ => writeAddress,
              writeCallback: (_, value) => writeAddress = (uint)value);

        registersMap[(long)Registers.TRANS_COUNT] = new DoubleWordRegister(this)
          .WithValueField(0, 32, valueProviderCallback: _ => transferCount,
              writeCallback: (_, value) => transferCount = (uint)value);

        registersMap[(long)Registers.CTRL] = new DoubleWordRegister(this)
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
          .WithValueField(15, 5, valueProviderCallback: _ => (ulong)transferRequestSignal,
            writeCallback: (_, value) => transferRequestSignal = (int)value, name: "TREQ_SEL")
          .WithFlag(21, out irqQuiet, name: "IRQ_QUIET")
          .WithFlag(22, out byteSwap, name: "BSWAP")
          .WithFlag(23, out sniffEnable, name: "SNIFF_EN")
          .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => !parent.channelFinished[channelNumber])
          .WithReservedBits(25, 4)
          .WithFlag(29, out writeError, FieldMode.Read | FieldMode.WriteOneToClear, name: "WRITE_ERROR")
          .WithFlag(30, out readError, FieldMode.Read | FieldMode.WriteOneToClear, name: "READ_ERROR")
          .WithFlag(31, out ahbError, FieldMode.Read | FieldMode.WriteOneToClear, name: "AHB_ERROR");

        return new DoubleWordRegisterCollection(this, registersMap);
      }

      public GPIO IRQ { get; private set; }
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
      private int transferRequestSignal;
      private IFlagRegisterField irqQuiet;
      private IFlagRegisterField byteSwap;
      private IFlagRegisterField sniffEnable;
      private IFlagRegisterField writeError;
      private IFlagRegisterField readError;
      private IFlagRegisterField ahbError;

      private DoubleWordRegisterCollection registers;
      private Dictionary<long, Registers> aliases;
      private RPDMA parent;
      private int channelNumber;
    }

    private readonly Channel[] channels;
    private IMachine machine;
    private RPDmaEngine engine;
    private int numberOfChannels;
    private bool[] channelFinished;
    private DoubleWordRegisterCollection registers;
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
