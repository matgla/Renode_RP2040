/**
 * MockI2CDevice.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Peripherals.I2C
{
    /// <summary>
    /// Mock I2C device for unit testing I2C controller peripheral.
    /// Records all transactions and provides configurable responses.
    /// </summary>
    public class MockI2CDevice : II2CPeripheral
    {
        private readonly int address;
        private readonly List<byte> receivedData;
        private readonly Queue<byte> responseData;
        private readonly List<I2CTransaction> transactions;
        private int readPosition;
        private bool finishTransmissionCalled;

        public MockI2CDevice(int address)
        {
            this.address = address;
            receivedData = new List<byte>();
            responseData = new Queue<byte>();
            transactions = new List<I2CTransaction>();
            readPosition = 0;
            finishTransmissionCalled = false;
        }

        /// <summary>
        /// Reset the mock device state.
        /// </summary>
        public void Reset()
        {
            receivedData.Clear();
            responseData.Clear();
            transactions.Clear();
            readPosition = 0;
            finishTransmissionCalled = false;
        }

        /// <summary>
        /// Called by I2C controller when writing data to the device.
        /// Records all written data.
        /// </summary>
        public void Write(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            receivedData.AddRange(data);
            transactions.Add(new I2CTransaction
            {
                Type = TransactionType.Write,
                Data = data.ToArray(),
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// Called by I2C controller when reading data from the device.
        /// Returns configured response data.
        /// </summary>
        public byte[] Read(int count = 1)
        {
            var result = new List<byte>();

            for (int i = 0; i < count; i++)
            {
                if (responseData.Count > 0)
                {
                    result.Add(responseData.Dequeue());
                }
                else
                {
                    // Default to 0xFF (pulled high) when no data available
                    result.Add(0xFF);
                }
            }

            transactions.Add(new I2CTransaction
            {
                Type = TransactionType.Read,
                Data = result.ToArray(),
                Timestamp = DateTime.Now
            });

            return result.ToArray();
        }

        /// <summary>
        /// Called by I2C controller when STOP condition or repeated START is detected.
        /// </summary>
        public void FinishTransmission()
        {
            finishTransmissionCalled = true;
            transactions.Add(new I2CTransaction
            {
                Type = TransactionType.Stop,
                Data = new byte[0],
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// Get all data received from the I2C controller.
        /// </summary>
        public byte[] GetReceivedData()
        {
            return receivedData.ToArray();
        }

        /// <summary>
        /// Get all transactions that occurred.
        /// </summary>
        public IReadOnlyList<I2CTransaction> GetTransactions()
        {
            return transactions.AsReadOnly();
        }

        /// <summary>
        /// Clear all recorded data and transactions.
        /// </summary>
        public void ClearReceivedData()
        {
            receivedData.Clear();
        }

        /// <summary>
        /// Clear all transactions.
        /// </summary>
        public void ClearTransactions()
        {
            transactions.Clear();
        }

        /// <summary>
        /// Configure response data to be returned on Read operations.
        /// </summary>
        public void SetResponseData(byte[] data)
        {
            responseData.Clear();
            foreach (var b in data)
            {
                responseData.Enqueue(b);
            }
        }

        /// <summary>
        /// Configure response data from a hex string (e.g., "A5B6C7").
        /// </summary>
        public void SetResponseData(string hexString)
        {
            var data = new List<byte>();
            for (int i = 0; i < hexString.Length; i += 2)
            {
                if (i + 1 < hexString.Length)
                {
                    data.Add(Convert.ToByte(hexString.Substring(i, 2), 16));
                }
            }
            SetResponseData(data.ToArray());
        }

        /// <summary>
        /// Enqueue a single byte to the response queue.
        /// </summary>
        public void EnqueueResponseByte(byte data)
        {
            responseData.Enqueue(data);
        }

        /// <summary>
        /// Check if FinishTransmission was called.
        /// </summary>
        public bool WasFinishTransmissionCalled()
        {
            return finishTransmissionCalled;
        }

        /// <summary>
        /// Get the I2C address of this device.
        /// </summary>
        public int Address => address;

        /// <summary>
        /// Check if any data was received.
        /// </summary>
        public bool HasReceivedData => receivedData.Count > 0;

        /// <summary>
        /// Get the number of bytes received.
        /// </summary>
        public int ReceivedByteCount => receivedData.Count;
    }

    /// <summary>
    /// Represents a single I2C transaction for verification.
    /// </summary>
    public class I2CTransaction
    {
        public TransactionType Type { get; set; }
        public byte[] Data { get; set; }
        public DateTime Timestamp { get; set; }

        public override string ToString()
        {
            var dataHex = Data != null ? string.Join(" ", Data.Select(b => $"0x{b:X2}")) : "empty";
            return $"{Type}: {dataHex}";
        }
    }

    /// <summary>
    /// Type of I2C transaction.
    /// </summary>
    public enum TransactionType
    {
        Write,
        Read,
        Stop
    }
}
