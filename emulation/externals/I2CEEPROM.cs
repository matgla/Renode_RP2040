/**
 * I2CEEPROM.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.I2C
{
    /// <summary>
    /// I2C EEPROM device (e.g., AT24C series).
    /// Supports standard I2C EEPROM addressing modes.
    /// </summary>
    public class I2CEEPROM : II2CPeripheral
    {
        public I2CEEPROM(int size = 256)
        {
            if (size != 256 && size != 512 && size != 1024 && size != 2048 && 
                size != 4096 && size != 8192 && size != 16384 && size != 32768 && size != 65536)
            {
                throw new ArgumentException("Size must be 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, or 65536 bytes");
            }

            this.size = size;
            memory = new byte[size];
            Reset();
        }

        public void Reset()
        {
            // Clear memory to 0xFF (erased state)
            for (int i = 0; i < memory.Length; i++)
            {
                memory[i] = 0xFF;
            }
            currentAddress = 0;
            addressHighByte = 0;
            addressBytesReceived = 0;
            isAddressSet = false;
        }

        public void Write(byte[] data)
        {
            if (data.Length == 0)
            {
                return;
            }

            if (!isAddressSet)
            {
                // First bytes are the memory address
                if (size > 256)
                {
                    // For larger EEPROMs, first 1-2 bytes are the address
                    if (addressBytesReceived == 0)
                    {
                        addressHighByte = data[0];
                        addressBytesReceived = 1;
                        
                        if (data.Length > 1)
                        {
                            currentAddress = (addressHighByte << 8) | data[1];
                            addressBytesReceived = 2;
                            isAddressSet = true;
                            
                            // Write remaining data
                            for (int i = 2; i < data.Length; i++)
                            {
                                WriteByte(data[i]);
                            }
                        }
                    }
                }
                else
                {
                    // Small EEPROM (256 bytes or less), 1 byte address
                    currentAddress = data[0];
                    isAddressSet = true;
                    
                    // Write remaining data
                    for (int i = 1; i < data.Length; i++)
                    {
                        WriteByte(data[i]);
                    }
                }
            }
            else
            {
                // Address already set, write data
                foreach (byte b in data)
                {
                    WriteByte(b);
                }
            }
        }

        private void WriteByte(byte value)
        {
            if (currentAddress < size)
            {
                memory[currentAddress] = value;
                currentAddress = (currentAddress + 1) % size; // Wrap around
            }
        }

        public byte[] Read(int count = 1)
        {
            var result = new byte[count];

            for (int i = 0; i < count; i++)
            {
                if (currentAddress < size)
                {
                    result[i] = memory[currentAddress];
                    currentAddress = (currentAddress + 1) % size; // Auto-increment with wrap
                }
                else
                {
                    result[i] = 0xFF;
                }
            }

            return result;
        }

        public void FinishTransmission()
        {
            // Reset address state for next transaction
            addressBytesReceived = 0;
            isAddressSet = false;
        }

        public void LoadData(byte[] data, int offset = 0)
        {
            if (offset < 0 || offset >= size)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            int length = Math.Min(data.Length, size - offset);
            Array.Copy(data, 0, memory, offset, length);
        }

        public byte[] DumpMemory(int offset = 0, int count = -1)
        {
            if (count < 0)
            {
                count = size - offset;
            }
            
            count = Math.Min(count, size - offset);
            var result = new byte[count];
            Array.Copy(memory, offset, result, 0, count);
            return result;
        }

        private readonly byte[] memory;
        private readonly int size;
        private int currentAddress;
        private byte addressHighByte;
        private int addressBytesReceived;
        private bool isAddressSet;
    }
}
