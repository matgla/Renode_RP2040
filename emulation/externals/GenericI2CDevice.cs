/**
 * GenericI2CDevice.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.I2C
{
    /// <summary>
    /// Generic I2C device that can be configured to respond to any address.
    /// Used for bus scan testing.
    /// </summary>
    public class GenericI2CDevice : II2CPeripheral
    {
        public GenericI2CDevice()
        {
            responseData = new List<byte>();
            receivedData = new List<byte>();
            Reset();
        }

        public void Reset()
        {
            responseData.Clear();
            receivedData.Clear();
            currentPosition = 0;
            // Default response: some dummy data
            responseData.Add(0xAB);
            responseData.Add(0xCD);
            responseData.Add(0xEF);
            responseData.Add(0x12);
        }

        public void Write(byte[] data)
        {
            receivedData.AddRange(data);
        }

        public byte[] Read(int count = 1)
        {
            var result = new List<byte>();

            for (int i = 0; i < count; i++)
            {
                if (currentPosition < responseData.Count)
                {
                    result.Add(responseData[currentPosition]);
                    currentPosition++;
                }
                else
                {
                    // Return 0xFF when no more data (standard I2C pull-up behavior)
                    result.Add(0xFF);
                }
            }

            return result.ToArray();
        }

        public void FinishTransmission()
        {
            currentPosition = 0;
        }

        public void SetResponseData(byte[] data)
        {
            responseData.Clear();
            responseData.AddRange(data);
            currentPosition = 0;
        }

        public byte[] GetReceivedData()
        {
            return receivedData.ToArray();
        }

        public void ClearReceivedData()
        {
            receivedData.Clear();
        }

        private List<byte> responseData;
        private List<byte> receivedData;
        private int currentPosition;
    }
}
