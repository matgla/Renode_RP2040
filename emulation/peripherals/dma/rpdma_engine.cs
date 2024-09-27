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

namespace Antmicro.Renode.Peripherals.DMA
{
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

    public ResponseWithCrc IssueCopy(Request request, CPU.ICPU context = null, ChecksumRequest? checksum = null)
    {
      var response = new Response
      {
        ReadAddress = request.Source.Address,
        WriteAddress = request.Destination.Address,
      };

      var responseWithCrc = new ResponseWithCrc();
      var readLengthInBytes = (int)request.ReadTransferType;
      var writeLengthInBytes = (int)request.WriteTransferType;

      // some sanity checks
      if ((request.Size % readLengthInBytes) != 0 || (request.Size % writeLengthInBytes) != 0)
      {
        throw new ArgumentException("Request size is not aligned properly to given read or write transfer type (or both).");
      }

      var buffer = new byte[request.Size];
      var sourceAddress = request.Source.Address ?? 0;
      var whatIsAtSource = sysbus.WhatIsAt(sourceAddress, context);
      var isSourceContinuousMemory = (whatIsAtSource == null || whatIsAtSource.Peripheral is MappedMemory) // Not a peripheral
                                                  && readLengthInBytes == request.SourceIncrementStep; // Consistent memory region
      if (!request.Source.Address.HasValue)
      {
        // request array based copy
        Array.Copy(request.Source.Array, request.Source.StartIndex.Value, buffer, 0, request.Size);
      }
      else if (isSourceContinuousMemory)
      {
        if (request.IncrementReadAddress)
        {
          // Transfer Units |  1  |  2  |  3  |  4  |
          // Source         |  A  |  B  |  C  |  D  |
          // Copied         |  A  |  B  |  C  |  D  |
          sysbus.ReadBytes(sourceAddress, request.Size, buffer, 0, context: context);
          response.ReadAddress += (ulong)request.Size;
        }
        else
        {
          // When reading from the memory with IncrementReadAddress unset, effectively, only the last unit will be used
          // Transfer Units |  1  |  2  |  3  |  4  |
          // Source         |  A  |  B  |  C  |  D  |
          // Copied         |  D  |     |     |     |
          sysbus.ReadBytes(sourceAddress, readLengthInBytes, buffer, 0, context: context);
        }
      }
      else if (whatIsAtSource != null)
      {
        // Read from peripherals
        var transferred = 0;
        var offset = 0UL;
        while (transferred < request.Size)
        {
          var readAddress = sourceAddress + offset;
          switch (request.ReadTransferType)
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
              throw new ArgumentOutOfRangeException($"Requested read transfer size: {request.ReadTransferType} is not supported by DmaEngine");
          }
          transferred += readLengthInBytes;
          if (request.IncrementReadAddress)
          {
            offset += request.SourceIncrementStep;
            response.ReadAddress += request.SourceIncrementStep;
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

      var destinationAddress = request.Destination.Address ?? 0;
      var whatIsAtDestination = sysbus.WhatIsAt(destinationAddress);
      var isDestinationContinuousMemory = (whatIsAtDestination == null || whatIsAtDestination.Peripheral is MappedMemory) // Not a peripheral
                                                  && readLengthInBytes == request.DestinationIncrementStep;  // Consistent memory region
      if (!request.Destination.Address.HasValue)
      {
        // request array based copy
        Array.Copy(buffer, 0, request.Destination.Array, request.Destination.StartIndex.Value, request.Size);
      }
      else if (isDestinationContinuousMemory)
      {
        if (request.IncrementWriteAddress)
        {
          if (request.IncrementReadAddress || !isSourceContinuousMemory)
          {
            // Transfer Units |  1  |  2  |  3  |  4  |
            // Source         |  A  |  B  |  C  |  D  |
            // Destination    |  A  |  B  |  C  |  D  |
            sysbus.WriteBytes(buffer, destinationAddress, context: context);
          }
          else
          {
            // When writing memory with IncrementReadAddress unset all destination units are written with the first source unit
            // Transfer Units |  1  |  2  |  3  |  4  |
            // Source         |  A  |  B  |  C  |  D  |
            // Destination    |  A  |  A  |  A  |  A  |
            var chunkStartOffset = 0UL;
            var chunk = buffer.Take(writeLengthInBytes).ToArray();
            while (chunkStartOffset < (ulong)request.Size)
            {
              var writeAddress = destinationAddress + chunkStartOffset;
              sysbus.WriteBytes(chunk, writeAddress, context: context);
              chunkStartOffset += (ulong)writeLengthInBytes;
            }
          }
          response.WriteAddress += (ulong)request.Size;
        }
        else
        {
          // When writing to memory with IncrementWriteAddress unset, effectively, only the last unit is written with the last unit of source
          // Transfer Units |  1  |  2  |  3  |  4  |
          // Source         |  A  |  B  |  C  |  D  |
          // Destination    |  D  |     |     |     |
          var skipCount = (request.Size == writeLengthInBytes) ? 0 : request.Size - writeLengthInBytes;
          DebugHelper.Assert((skipCount + request.Size) <= buffer.Length);
          sysbus.WriteBytes(buffer.Skip(skipCount).ToArray(), destinationAddress, context: context);
        }
      }
      else if (whatIsAtDestination != null)
      {
        // Write to peripheral
        var transferred = 0;
        var offset = 0UL;
        while (transferred < request.Size)
        {
          switch (request.WriteTransferType)
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
              throw new ArgumentOutOfRangeException($"Requested write transfer size: {request.WriteTransferType} is not supported by DmaEngine");
          }
          transferred += writeLengthInBytes;
          if (request.IncrementWriteAddress)
          {
            offset += request.DestinationIncrementStep;
            response.WriteAddress += request.DestinationIncrementStep;
          }
        }
      }

      responseWithCrc.response = response;
      return responseWithCrc;
    }

    private readonly IBusController sysbus;
  }
}
