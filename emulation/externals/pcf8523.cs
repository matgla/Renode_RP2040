/**
 * pcf8523.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class PCF8523 : II2CPeripheral
    {
        public PCF8523()
        {
            Reset();
        }

        public void Reset()
        {

        }

        public void Write(byte[] data)
        {

        }

        public byte[] Read(int count = 0)
        {
            return new byte[0];
        }

        public void FinishTransmission()
        {

        }
    }
}