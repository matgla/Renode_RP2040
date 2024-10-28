//
// Copyright (c) 2024 Mateusz Stadnik
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.DMA
{

  public struct RPXXXXDmaRequest 
  {
    public RPXXXXDmaRequest(Request request, int ringSize, bool ringWrite, int offset)
      : this()
    {
      this.request = request;
      this.ringSize = ringSize;
      this.ringWrite = ringWrite;
      this.offset = offset;
    }
    public Request request;
    public int ringSize;
    public bool ringWrite;
    public int offset;
  }

  public struct ChecksumRequest
  {
    public enum Type
    {
      Crc32,
      Crc32Reversed,
      Crc16CCITT,
      Crc16CCITTReversed,
      XORReduction,
      Sum
    };
    public Type type { get; set; }
    public uint init { get; set; }
  }

  public struct ResponseWithCrc
  {
    public Response response { get; set; }
    public uint crc { get; set; }
  }

  public sealed class RPDmaEngine
  {
    public RPDmaEngine(IBusController systemBus)
    {
      sysbus = systemBus;
    }

    public ResponseWithCrc IssueCopy(RPXXXXDmaRequest request, CPU.ICPU context = null, ChecksumRequest? checksum = null)
    {
      var response = new Response
      {
        ReadAddress = request.request.Source.Address,
        WriteAddress = request.request.Destination.Address,
      };

      var responseWithCrc = new ResponseWithCrc();
      var readLengthInBytes = (int)request.request.ReadTransferType;
      var writeLengthInBytes = (int)request.request.WriteTransferType;

      ulong readOffset = 0;
      ulong writeOffset = 0;
      if (request.request.IncrementReadAddress)
      {
        readOffset = (ulong)request.offset;
        if (!request.ringWrite && request.ringSize != 0)
        {
          readOffset = readOffset % (ulong)request.ringSize;
        }
      }

      if (request.request.IncrementWriteAddress)
      {
        writeOffset = (ulong)request.offset;
        if (request.ringWrite && request.ringSize != 0)
        {
          writeOffset = writeOffset % (ulong)request.ringSize;
        }
      }
      writeOffset *= (ulong)writeLengthInBytes;
      readOffset *= (ulong)readLengthInBytes;

      // some sanity checks
      if ((request.request.Size % readLengthInBytes) != 0 || (request.request.Size % writeLengthInBytes) != 0)
      {
        throw new ArgumentException("Request size is not aligned properly to given read or write transfer type (or both).");
      }

      var buffer = new byte[request.request.Size];
      var sourceAddress = request.request.Source.Address ?? 0;
      var whatIsAtSource = sysbus.WhatIsAt(sourceAddress, context);
      var isSourceContinuousMemory = (whatIsAtSource == null || whatIsAtSource.Peripheral is MappedMemory) // Not a peripheral
                                                  && readLengthInBytes == request.request.SourceIncrementStep; // Consistent memory region
      if (!request.request.Source.Address.HasValue)
      {
        // request array based copy
        Array.Copy(request.request.Source.Array, request.request.Source.StartIndex.Value, buffer, 0, request.request.Size);
      }
      else if (isSourceContinuousMemory)
      {
        if (request.request.IncrementReadAddress)
        {
          // Transfer Units |  1  |  2  |  3  |  4  |
          // Source         |  A  |  B  |  C  |  D  |
          // Copied         |  A  |  B  |  C  |  D  |
          response.ReadAddress += (ulong)ReadFromMemory(sourceAddress + readOffset, buffer, request.request.Size, context: context, request.ringWrite == false ? request.ringSize : 0);
        }
        else
        {
          // When reading from the memory with IncrementReadAddress unset, effectively, only the last unit will be used
          // Transfer Units |  1  |  2  |  3  |  4  |
          // Source         |  A  |  B  |  C  |  D  |
          // Copied         |  D  |     |     |     |
          sysbus.ReadBytes(sourceAddress + readOffset, readLengthInBytes, buffer, 0, context: context);
        }
      }
      else if (whatIsAtSource != null)
      {
        // Read from peripherals
        var transferred = 0;
        var offset = 0UL;
        while (transferred < request.request.Size)
        {
          var readAddress = sourceAddress + offset + readOffset;
          switch (request.request.ReadTransferType)
          {
            case TransferType.Byte:
              buffer[transferred] = sysbus.ReadByte(readAddress, context);
              break;
            case TransferType.Word:
              BitConverter.GetBytes(sysbus.ReadWord(readAddress, context)).CopyTo(buffer, transferred);
              break;
            case TransferType.DoubleWord:
              BitConverter.GetBytes(sysbus.ReadDoubleWord(readAddress, context)).CopyTo(buffer, transferred);
              break;
            case TransferType.QuadWord:
              BitConverter.GetBytes(sysbus.ReadQuadWord(readAddress, context)).CopyTo(buffer, transferred);
              break;
            default:
              throw new ArgumentOutOfRangeException($"Requested read transfer size: {request.request.ReadTransferType} is not supported by DmaEngine");
          }
          transferred += readLengthInBytes;
          if (request.request.IncrementReadAddress)
          {
            offset += request.request.SourceIncrementStep;
            response.ReadAddress += request.request.SourceIncrementStep;
            if (request.ringSize != 0 && request.ringWrite == false && offset == (ulong)request.ringSize)
            {
              offset = 0;
              response.ReadAddress = request.request.Source.Address;
            }
          }
        }
      }

      if (checksum != null)
      {
        switch (checksum.Value.type)
        {
          case ChecksumRequest.Type.Crc32Reversed:
            {
              var crcEngine = new CRCEngine(CRCPolynomial.CRC32, true, true, checksum.Value.init);
              responseWithCrc.crc = crcEngine.Calculate(buffer);
              break;
            }
          case ChecksumRequest.Type.Crc32:
            {
              var crcEngine = new CRCEngine(CRCPolynomial.CRC32, false, false, checksum.Value.init);
              responseWithCrc.crc = crcEngine.Calculate(buffer);
              break;
            }
          case ChecksumRequest.Type.Crc16CCITT:
            {
              var crcEngine = new CRCEngine(CRCPolynomial.CRC16_CCITT, false, false, checksum.Value.init);
              responseWithCrc.crc = crcEngine.Calculate(buffer);
              break;
            }
          case ChecksumRequest.Type.Crc16CCITTReversed:
            {
              var crcEngine = new CRCEngine(CRCPolynomial.CRC16_CCITT, true, false, checksum.Value.init);
              responseWithCrc.crc = crcEngine.Calculate(buffer);
              break;
            }
          case ChecksumRequest.Type.XORReduction:
            {
              UInt32 calculated = checksum.Value.init;
              foreach(var b in buffer) {
                calculated ^= b;
              }
              uint bitCount = 0;
              while (calculated > 0)
              {
                bitCount += calculated & 1;
                calculated >>= 1;
              } 
              responseWithCrc.crc = Convert.ToUInt32(bitCount % 2 == 1);
              break;
            }
          case ChecksumRequest.Type.Sum:
            {
              uint sum = 0;
              foreach (var b in buffer) {
                sum += b;
              } 
              responseWithCrc.crc = sum;
              break;
            }
        }
      }

      var destinationAddress = request.request.Destination.Address ?? 0;
      var whatIsAtDestination = sysbus.WhatIsAt(destinationAddress);
      var isDestinationContinuousMemory = (whatIsAtDestination == null || whatIsAtDestination.Peripheral is MappedMemory) // Not a peripheral
                                                  && readLengthInBytes == request.request.DestinationIncrementStep;  // Consistent memory region
      if (!request.request.Destination.Address.HasValue)
      {
        // request array based copy
        Array.Copy(buffer, 0, request.request.Destination.Array, request.request.Destination.StartIndex.Value, request.request.Size);
      }
      else if (isDestinationContinuousMemory)
      {
        if (request.request.IncrementWriteAddress)
        {
          if (request.request.IncrementReadAddress || !isSourceContinuousMemory)
          {
            // Transfer Units |  1  |  2  |  3  |  4  |
            // Source         |  A  |  B  |  C  |  D  |
            // Destination    |  A  |  B  |  C  |  D  |
            WriteToMemory(destinationAddress + writeOffset, buffer, context, request.ringWrite == true ? request.ringSize : 0);
          }
          else
          {
            // When writing memory with IncrementReadAddress unset all destination units are written with the first source unit
            // Transfer Units |  1  |  2  |  3  |  4  |
            // Source         |  A  |  B  |  C  |  D  |
            // Destination    |  A  |  A  |  A  |  A  |
            var chunkStartOffset = 0UL;
            var chunk = buffer.Take(writeLengthInBytes).ToArray();
            while (chunkStartOffset < (ulong)request.request.Size)
            {
              var writeAddress = destinationAddress + chunkStartOffset;
              ulong size = (ulong)chunk.Length; 
              if (request.ringSize != 0 && request.ringWrite == true && chunkStartOffset % (ulong)request.ringSize != 0)
              {
                // write just what left till full ring
                size = (ulong)request.ringSize - chunkStartOffset % (ulong)request.ringSize;
              }
              sysbus.WriteBytes(chunk, writeAddress + writeOffset, (long)size, false, context: context);
              chunkStartOffset += size;
            }
          }
          if (request.ringSize != 0 && request.ringWrite == true) 
          {
            response.WriteAddress += (ulong)request.request.Size % (ulong)request.ringSize;
          }
          else 
          {
            response.WriteAddress += (ulong)request.request.Size;
          }
        }
        else
        {
          // When writing to memory with IncrementWriteAddress unset, effectively, only the last unit is written with the last unit of source
          // Transfer Units |  1  |  2  |  3  |  4  |
          // Source         |  A  |  B  |  C  |  D  |
          // Destination    |  D  |     |     |     |
          var skipCount = (request.request.Size == writeLengthInBytes) ? 0 : request.request.Size - writeLengthInBytes;
          DebugHelper.Assert((skipCount + request.request.Size) <= buffer.Length);
          sysbus.WriteBytes(buffer.Skip(skipCount).ToArray(), destinationAddress + writeOffset, context: context);
        }
      }
      else if (whatIsAtDestination != null)
      {
        // Write to peripheral
        var transferred = 0;
        var offset = 0UL + writeOffset;
        while (transferred < request.request.Size)
        {
          switch (request.request.WriteTransferType)
          {
            case TransferType.Byte:
              sysbus.WriteByte(destinationAddress + offset, buffer[transferred], context);
              break;
            case TransferType.Word:
              sysbus.WriteWord(destinationAddress + offset, BitConverter.ToUInt16(buffer, transferred), context);
              break;
            case TransferType.DoubleWord:
              sysbus.WriteDoubleWord(destinationAddress + offset, BitConverter.ToUInt32(buffer, transferred), context);
              break;
            case TransferType.QuadWord:
              sysbus.WriteQuadWord(destinationAddress + offset, BitConverter.ToUInt64(buffer, transferred), context);
              break;
            default:
              throw new ArgumentOutOfRangeException($"Requested write transfer size: {request.request.WriteTransferType} is not supported by DmaEngine");
          }
          transferred += writeLengthInBytes;
          if (request.request.IncrementWriteAddress)
          {
            offset += request.request.DestinationIncrementStep;
            response.WriteAddress += request.request.DestinationIncrementStep;
            // since starting address must be aligned to ring size, when offset is ring size, we can just substract it from address 
            if (request.ringSize != 0 && request.ringWrite == true && (offset % (ulong)request.ringSize == 0))
            {
              response.WriteAddress -= (ulong)request.ringSize;
            }
          }
        }
      }

      responseWithCrc.response = response;
      return responseWithCrc;
    }

    private int ReadFromMemory(ulong sourceAddress, byte[] buffer, int size, CPU.ICPU context, int ringSize)
    {
      if (ringSize == 0)
      {
        sysbus.ReadBytes(sourceAddress, size, buffer, 0, context: context);
        return size;
      }

      int transferred = 0;
      while (transferred < size)
      {
        int chunkSize = size - transferred > ringSize ? ringSize : size - transferred; 
        var chunk = new byte[chunkSize];
        sysbus.ReadBytes(sourceAddress, chunkSize, chunk, 0, context: context);
        Array.Copy(chunk, 0, buffer, transferred, chunkSize); 
        transferred += chunkSize;
      }
      return size % ringSize; 
    }
    private int WriteToMemory(ulong destinationAddress, byte[] buffer, CPU.ICPU context, int ringSize)
    {
      int size = buffer.Length;
      if (ringSize == 0)
      {
        sysbus.WriteBytes(buffer, destinationAddress, context: context);
        return size;
      }

      int transferred = 0;
      while (transferred < size)
      {
        int chunkSize = size - transferred > ringSize ? ringSize : size - transferred; 
        var chunk = new byte[chunkSize];
        Array.Copy(buffer, transferred, chunk, 0, chunkSize); 
        sysbus.WriteBytes(chunk, destinationAddress, context: context);
        transferred += chunkSize;
      }
      return size % ringSize; 
    }


    private readonly IBusController sysbus;
  }
}
