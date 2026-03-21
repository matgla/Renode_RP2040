/**
 * SSD1306Tests.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using Antmicro.Renode.Peripherals.I2C;

namespace Antmicro.Renode.Tests.Peripherals.I2C
{
    public class SSD1306Tests
    {
        [AttributeUsage(AttributeTargets.Method)]
        public class TestAttribute : Attribute { }

        [Test]
        public void SSD1306_ContinuedCommandStream_WritesFramebufferData()
        {
            var display = new SSD1306();

            display.Write(new byte[]
            {
                0x80, 0x21,
                0x80, 0x00,
                0x80, 0x00,
                0x80, 0x22,
                0x80, 0x00,
                0x80, 0x00,
                0x40, 0xFF,
            });
            display.FinishTransmission();

            var framebuffer = display.GetFramebuffer();
            if (framebuffer[0] != 0xFF)
            {
                throw new Exception($"Expected first framebuffer byte to be 0xFF, got 0x{framebuffer[0]:X2}");
            }
        }
    }
}