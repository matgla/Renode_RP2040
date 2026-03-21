/**
 * MockSPIPeripheral.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.SPI
{
    /// <summary>
    /// Mock SPI peripheral for unit testing SPI controller.
    /// Records all transactions and provides configurable responses.
    /// </summary>
    public class MockSPIPeripheral
    {
        private readonly List<byte> transmittedBytes;
        private readonly Queue<byte> responseBytes;
        private readonly List<SPITransaction> transactions;
        private bool chipSelectActive;

        public MockSPIPeripheral()
        {
            transmittedBytes = new List<byte>();
            responseBytes = new Queue<byte>();
            transactions = new List<SPITransaction>();
            chipSelectActive = false;
        }

        public void Reset()
        {
            transmittedBytes.Clear();
            responseBytes.Clear();
            transactions.Clear();
            chipSelectActive = false;
        }

        /// <summary>
        /// Called when SPI master transmits data to this device.
        /// </summary>
        public byte Transmit(byte data)
        {
            transmittedBytes.Add(data);
            
            byte response = 0xFF; // Default (pulled high)
            if (responseBytes.Count > 0)
            {
                response = responseBytes.Dequeue();
            }

            transactions.Add(new SPITransaction
            {
                Type = TransactionType.Data,
                Transmitted = data,
                Received = response,
                Timestamp = DateTime.Now
            });

            return response;
        }

        /// <summary>
        /// Called when chip select state changes.
        /// </summary>
        public void OnGPIO(int number, bool value)
        {
            // CS is typically active low
            chipSelectActive = !value;
            
            if (!chipSelectActive)
            {
                transactions.Add(new SPITransaction
                {
                    Type = TransactionType.Deselect,
                    Timestamp = DateTime.Now
                });
            }
            else
            {
                transactions.Add(new SPITransaction
                {
                    Type = TransactionType.Select,
                    Timestamp = DateTime.Now
                });
            }
        }

        public void FinishTransmission()
        {
            transactions.Add(new SPITransaction
            {
                Type = TransactionType.Finish,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// Set response bytes to be returned on subsequent Transmit calls.
        /// </summary>
        public void SetResponseBytes(byte[] data)
        {
            responseBytes.Clear();
            foreach (var b in data)
            {
                responseBytes.Enqueue(b);
            }
        }

        /// <summary>
        /// Get all bytes transmitted by the master.
        /// </summary>
        public byte[] GetTransmittedBytes()
        {
            return transmittedBytes.ToArray();
        }

        /// <summary>
        /// Get all recorded transactions.
        /// </summary>
        public IReadOnlyList<SPITransaction> GetTransactions()
        {
            return transactions.AsReadOnly();
        }

        /// <summary>
        /// Check if chip select is currently active.
        /// </summary>
        public bool IsChipSelectActive => chipSelectActive;

        /// <summary>
        /// Check if any data was transmitted.
        /// </summary>
        public bool HasTransmittedData => transmittedBytes.Count > 0;
    }

    /// <summary>
    /// Represents a single SPI transaction for verification.
    /// </summary>
    public class SPITransaction
    {
        public TransactionType Type { get; set; }
        public byte Transmitted { get; set; }
        public byte Received { get; set; }
        public DateTime Timestamp { get; set; }

        public override string ToString()
        {
            if (Type == TransactionType.Data)
                return $"Data: TX=0x{Transmitted:X2}, RX=0x{Received:X2}";
            return Type.ToString();
        }
    }

    public enum TransactionType
    {
        Data,
        Select,
        Deselect,
        Finish
    }
}
