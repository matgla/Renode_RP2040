/**
 * rp2040_gpio.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.GPIOPort
{

    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public class RP2040GPIO : BaseGPIOPort, IRP2040Peripheral, IDoubleWordPeripheral, IGPIOReceiver, IKnownSize
    {
        public RP2040GPIO(IMachine machine, int numberOfPins, ulong address) : base(machine, numberOfPins)
        {
            functionSelectCallbacks = new List<Action<int, GpioFunction>>();
            NumberOfPins = numberOfPins;
            registers = CreateRegisters();
            functionSelect = new int[NumberOfPins];
            ReevaluatePioActions = new List<Action<uint>>();
            pullDown = new bool[NumberOfPins];
            pullUp = new bool[NumberOfPins];
            outputEnableOverride = new OutputEnableOverride[NumberOfPins];
            forcedOutputDisableMap = new bool[NumberOfPins];
            outputOverride = new OutputOverride[NumberOfPins];
            peripheralDrive = new PeripheralDrive[NumberOfPins];
            OperationDone = new GPIO();

            machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + xorAliasOffset, aliasSize, "XOR"));
            machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + setAliasOffset, aliasSize, "SET"));
            machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + clearAliasOffset, aliasSize, "CLEAR"));

            pinMapping = new GpioFunction[30, 9];
            pinMapping[0, 0] = GpioFunction.SPI0_RX; 
            pinMapping[0, 1] = GpioFunction.UART0_TX;
            pinMapping[0, 2] = GpioFunction.I2C0_SDA; 
            pinMapping[0, 3] = GpioFunction.PWM0_A; 
            pinMapping[0, 4] = GpioFunction.SIO; 
            pinMapping[0, 5] = GpioFunction.PIO0; 
            pinMapping[0, 6] = GpioFunction.PIO1; 
            pinMapping[0, 7] = GpioFunction.NONE; 
            pinMapping[0, 8] = GpioFunction.USB_OVCUR_DET;

            pinMapping[1, 0] = GpioFunction.SPI0_CSN; 
            pinMapping[1, 1] = GpioFunction.UART0_RX;
            pinMapping[1, 2] = GpioFunction.I2C0_SCL; 
            pinMapping[1, 3] = GpioFunction.PWM0_B; 
            pinMapping[1, 4] = GpioFunction.SIO; 
            pinMapping[1, 5] = GpioFunction.PIO0; 
            pinMapping[1, 6] = GpioFunction.PIO1; 
            pinMapping[1, 7] = GpioFunction.NONE; 
            pinMapping[1, 8] = GpioFunction.USB_VBUS_DET;

            pinMapping[2, 0] = GpioFunction.SPI0_SCK; 
            pinMapping[2, 1] = GpioFunction.UART0_CTS;
            pinMapping[2, 2] = GpioFunction.I2C1_SDA; 
            pinMapping[2, 3] = GpioFunction.PWM1_A; 
            pinMapping[2, 4] = GpioFunction.SIO; 
            pinMapping[2, 5] = GpioFunction.PIO0; 
            pinMapping[2, 6] = GpioFunction.PIO1; 
            pinMapping[2, 7] = GpioFunction.NONE; 
            pinMapping[2, 8] = GpioFunction.USB_VBUS_EN;

            pinMapping[3, 0] = GpioFunction.SPI0_TX; 
            pinMapping[3, 1] = GpioFunction.UART0_RTS;
            pinMapping[3, 2] = GpioFunction.I2C1_SCL; 
            pinMapping[3, 3] = GpioFunction.PWM1_B; 
            pinMapping[3, 4] = GpioFunction.SIO; 
            pinMapping[3, 5] = GpioFunction.PIO0; 
            pinMapping[3, 6] = GpioFunction.PIO1; 
            pinMapping[3, 7] = GpioFunction.NONE; 
            pinMapping[3, 8] = GpioFunction.USB_OVCUR_DET;

            pinMapping[4, 0] = GpioFunction.SPI0_RX; 
            pinMapping[4, 1] = GpioFunction.UART1_TX;
            pinMapping[4, 2] = GpioFunction.I2C0_SDA; 
            pinMapping[4, 3] = GpioFunction.PWM2_A; 
            pinMapping[4, 4] = GpioFunction.SIO; 
            pinMapping[4, 5] = GpioFunction.PIO0; 
            pinMapping[4, 6] = GpioFunction.PIO1; 
            pinMapping[4, 7] = GpioFunction.NONE; 
            pinMapping[4, 8] = GpioFunction.USB_VBUS_DET;

            pinMapping[5, 0] = GpioFunction.SPI0_CSN; 
            pinMapping[5, 1] = GpioFunction.UART1_RX;
            pinMapping[5, 2] = GpioFunction.I2C0_SCL; 
            pinMapping[5, 3] = GpioFunction.PWM2_B; 
            pinMapping[5, 4] = GpioFunction.SIO; 
            pinMapping[5, 5] = GpioFunction.PIO0; 
            pinMapping[5, 6] = GpioFunction.PIO1; 
            pinMapping[5, 7] = GpioFunction.NONE; 
            pinMapping[5, 8] = GpioFunction.USB_VBUS_EN;

            pinMapping[6, 0] = GpioFunction.SPI0_SCK; 
            pinMapping[6, 1] = GpioFunction.UART1_CTS;
            pinMapping[6, 2] = GpioFunction.I2C1_SDA; 
            pinMapping[6, 3] = GpioFunction.PWM3_A; 
            pinMapping[6, 4] = GpioFunction.SIO; 
            pinMapping[6, 5] = GpioFunction.PIO0; 
            pinMapping[6, 6] = GpioFunction.PIO1; 
            pinMapping[6, 7] = GpioFunction.NONE; 
            pinMapping[6, 8] = GpioFunction.USB_OVCUR_DET;

            pinMapping[7, 0] = GpioFunction.SPI0_TX; 
            pinMapping[7, 1] = GpioFunction.UART1_RTS;
            pinMapping[7, 2] = GpioFunction.I2C1_SCL; 
            pinMapping[7, 3] = GpioFunction.PWM3_B; 
            pinMapping[7, 4] = GpioFunction.SIO; 
            pinMapping[7, 5] = GpioFunction.PIO0; 
            pinMapping[7, 6] = GpioFunction.PIO1; 
            pinMapping[7, 7] = GpioFunction.NONE; 
            pinMapping[7, 8] = GpioFunction.USB_VBUS_DET;

            pinMapping[8, 0] = GpioFunction.SPI1_RX; 
            pinMapping[8, 1] = GpioFunction.UART1_TX;
            pinMapping[8, 2] = GpioFunction.I2C0_SDA; 
            pinMapping[8, 3] = GpioFunction.PWM4_A; 
            pinMapping[8, 4] = GpioFunction.SIO; 
            pinMapping[8, 5] = GpioFunction.PIO0; 
            pinMapping[8, 6] = GpioFunction.PIO1; 
            pinMapping[8, 7] = GpioFunction.NONE; 
            pinMapping[8, 8] = GpioFunction.USB_VBUS_EN;

            pinMapping[9, 0] = GpioFunction.SPI1_SCK; 
            pinMapping[9, 1] = GpioFunction.UART1_CTS;
            pinMapping[9, 2] = GpioFunction.I2C0_SCL; 
            pinMapping[9, 3] = GpioFunction.PWM4_B; 
            pinMapping[9, 4] = GpioFunction.SIO; 
            pinMapping[9, 5] = GpioFunction.PIO0; 
            pinMapping[9, 6] = GpioFunction.PIO1; 
            pinMapping[9, 7] = GpioFunction.NONE; 
            pinMapping[9, 8] = GpioFunction.USB_OVCUR_DET;

            pinMapping[10, 0] = GpioFunction.SPI1_TX; 
            pinMapping[10, 1] = GpioFunction.UART1_RTS;
            pinMapping[10, 2] = GpioFunction.I2C1_SDA; 
            pinMapping[10, 3] = GpioFunction.PWM5_A; 
            pinMapping[10, 4] = GpioFunction.SIO; 
            pinMapping[10, 5] = GpioFunction.PIO0; 
            pinMapping[10, 6] = GpioFunction.PIO1; 
            pinMapping[10, 7] = GpioFunction.NONE; 
            pinMapping[10, 8] = GpioFunction.USB_VBUS_DET;

            pinMapping[11, 0] = GpioFunction.SPI1_RX; 
            pinMapping[11, 1] = GpioFunction.UART0_TX;
            pinMapping[11, 2] = GpioFunction.I2C1_SCL; 
            pinMapping[11, 3] = GpioFunction.PWM5_B; 
            pinMapping[11, 4] = GpioFunction.SIO; 
            pinMapping[11, 5] = GpioFunction.PIO0; 
            pinMapping[11, 6] = GpioFunction.PIO1; 
            pinMapping[11, 7] = GpioFunction.NONE; 
            pinMapping[11, 8] = GpioFunction.USB_VBUS_EN;

            pinMapping[12, 0] = GpioFunction.SPI1_CSN; 
            pinMapping[12, 1] = GpioFunction.UART0_RX;
            pinMapping[12, 2] = GpioFunction.I2C0_SDA; 
            pinMapping[12, 3] = GpioFunction.PWM6_A; 
            pinMapping[12, 4] = GpioFunction.SIO; 
            pinMapping[12, 5] = GpioFunction.PIO0; 
            pinMapping[12, 6] = GpioFunction.PIO1; 
            pinMapping[12, 7] = GpioFunction.NONE; 
            pinMapping[12, 8] = GpioFunction.USB_OVCUR_DET;

            pinMapping[13, 0] = GpioFunction.SPI1_SCK; 
            pinMapping[13, 1] = GpioFunction.UART0_CTS;
            pinMapping[13, 2] = GpioFunction.I2C0_SCL; 
            pinMapping[13, 3] = GpioFunction.PWM6_B; 
            pinMapping[13, 4] = GpioFunction.SIO; 
            pinMapping[13, 5] = GpioFunction.PIO0; 
            pinMapping[13, 6] = GpioFunction.PIO1; 
            pinMapping[13, 7] = GpioFunction.NONE; 
            pinMapping[13, 8] = GpioFunction.USB_VBUS_DET;

            pinMapping[14, 0] = GpioFunction.SPI1_TX; 
            pinMapping[14, 1] = GpioFunction.UART0_RTS;
            pinMapping[14, 2] = GpioFunction.I2C1_SDA; 
            pinMapping[14, 3] = GpioFunction.PWM7_A; 
            pinMapping[14, 4] = GpioFunction.SIO; 
            pinMapping[14, 5] = GpioFunction.PIO0; 
            pinMapping[14, 6] = GpioFunction.PIO1; 
            pinMapping[14, 7] = GpioFunction.NONE; 
            pinMapping[14, 8] = GpioFunction.USB_VBUS_EN;

            pinMapping[15, 0] = GpioFunction.SPI0_RX; 
            pinMapping[15, 1] = GpioFunction.UART0_TX;
            pinMapping[15, 2] = GpioFunction.I2C1_SCL; 
            pinMapping[15, 3] = GpioFunction.PWM7_B; 
            pinMapping[15, 4] = GpioFunction.SIO; 
            pinMapping[15, 5] = GpioFunction.PIO0; 
            pinMapping[15, 6] = GpioFunction.PIO1; 
            pinMapping[15, 7] = GpioFunction.NONE; 
            pinMapping[15, 8] = GpioFunction.USB_OVCUR_DET;

            pinMapping[16, 0] = GpioFunction.SPI0_CSN; 
            pinMapping[16, 1] = GpioFunction.UART0_RX;
            pinMapping[16, 2] = GpioFunction.I2C0_SDA; 
            pinMapping[16, 3] = GpioFunction.PWM0_A; 
            pinMapping[16, 4] = GpioFunction.SIO; 
            pinMapping[16, 5] = GpioFunction.PIO0; 
            pinMapping[16, 6] = GpioFunction.PIO1; 
            pinMapping[16, 7] = GpioFunction.NONE; 
            pinMapping[16, 8] = GpioFunction.USB_VBUS_DET;

            pinMapping[17, 0] = GpioFunction.SPI0_SCK; 
            pinMapping[17, 1] = GpioFunction.UART0_CTS;
            pinMapping[17, 2] = GpioFunction.I2C0_SCL; 
            pinMapping[17, 3] = GpioFunction.PWM0_B; 
            pinMapping[17, 4] = GpioFunction.SIO; 
            pinMapping[17, 5] = GpioFunction.PIO0; 
            pinMapping[17, 6] = GpioFunction.PIO1; 
            pinMapping[17, 7] = GpioFunction.NONE; 
            pinMapping[17, 8] = GpioFunction.USB_VBUS_EN;

            pinMapping[18, 0] = GpioFunction.SPI0_TX; 
            pinMapping[18, 1] = GpioFunction.UART0_RTS;
            pinMapping[18, 2] = GpioFunction.I2C1_SDA; 
            pinMapping[18, 3] = GpioFunction.PWM1_A; 
            pinMapping[18, 4] = GpioFunction.SIO; 
            pinMapping[18, 5] = GpioFunction.PIO0; 
            pinMapping[18, 6] = GpioFunction.PIO1; 
            pinMapping[18, 7] = GpioFunction.NONE; 
            pinMapping[18, 8] = GpioFunction.USB_OVCUR_DET;

            pinMapping[19, 0] = GpioFunction.SPI0_RX; 
            pinMapping[19, 1] = GpioFunction.UART1_TX;
            pinMapping[19, 2] = GpioFunction.I2C1_SCL; 
            pinMapping[19, 3] = GpioFunction.PWM1_B; 
            pinMapping[19, 4] = GpioFunction.SIO; 
            pinMapping[19, 5] = GpioFunction.PIO0; 
            pinMapping[19, 6] = GpioFunction.PIO1; 
            pinMapping[19, 7] = GpioFunction.NONE; 
            pinMapping[19, 8] = GpioFunction.USB_VBUS_DET;

            pinMapping[20, 0] = GpioFunction.SPI0_CSN; 
            pinMapping[20, 1] = GpioFunction.UART1_RX;
            pinMapping[20, 2] = GpioFunction.I2C0_SDA; 
            pinMapping[20, 3] = GpioFunction.PWM2_A; 
            pinMapping[20, 4] = GpioFunction.SIO; 
            pinMapping[20, 5] = GpioFunction.PIO0; 
            pinMapping[20, 6] = GpioFunction.PIO1; 
            pinMapping[20, 7] = GpioFunction.CLOCK_GPIN0; 
            pinMapping[20, 8] = GpioFunction.USB_VBUS_EN;

            pinMapping[21, 0] = GpioFunction.SPI0_SCK; 
            pinMapping[21, 1] = GpioFunction.UART1_CTS;
            pinMapping[21, 2] = GpioFunction.I2C0_SCL; 
            pinMapping[21, 3] = GpioFunction.PWM2_B; 
            pinMapping[21, 4] = GpioFunction.SIO; 
            pinMapping[21, 5] = GpioFunction.PIO0; 
            pinMapping[21, 6] = GpioFunction.PIO1; 
            pinMapping[21, 7] = GpioFunction.CLOCK_GPOUT0; 
            pinMapping[21, 8] = GpioFunction.USB_OVCUR_DET;

            pinMapping[22, 0] = GpioFunction.SPI0_TX; 
            pinMapping[22, 1] = GpioFunction.UART1_RTS;
            pinMapping[22, 2] = GpioFunction.I2C1_SDA; 
            pinMapping[22, 3] = GpioFunction.PWM3_A; 
            pinMapping[22, 4] = GpioFunction.SIO; 
            pinMapping[22, 5] = GpioFunction.PIO0; 
            pinMapping[22, 6] = GpioFunction.PIO1; 
            pinMapping[22, 7] = GpioFunction.CLOCK_GPIN1; 
            pinMapping[22, 8] = GpioFunction.USB_VBUS_DET;

            pinMapping[23, 0] = GpioFunction.SPI1_RX; 
            pinMapping[23, 1] = GpioFunction.UART1_TX;
            pinMapping[23, 2] = GpioFunction.I2C1_SCL; 
            pinMapping[23, 3] = GpioFunction.PWM3_B; 
            pinMapping[23, 4] = GpioFunction.SIO; 
            pinMapping[23, 5] = GpioFunction.PIO0; 
            pinMapping[23, 6] = GpioFunction.PIO1; 
            pinMapping[23, 7] = GpioFunction.CLOCK_GPOUT1; 
            pinMapping[23, 8] = GpioFunction.USB_VBUS_EN;

            pinMapping[24, 0] = GpioFunction.SPI1_CSN; 
            pinMapping[24, 1] = GpioFunction.UART1_RX;
            pinMapping[24, 2] = GpioFunction.I2C0_SDA; 
            pinMapping[24, 3] = GpioFunction.PWM4_A; 
            pinMapping[24, 4] = GpioFunction.SIO; 
            pinMapping[24, 5] = GpioFunction.PIO0; 
            pinMapping[24, 6] = GpioFunction.PIO1; 
            pinMapping[24, 7] = GpioFunction.CLOCK_GPOUT2; 
            pinMapping[24, 8] = GpioFunction.USB_OVCUR_DET;

            pinMapping[25, 0] = GpioFunction.SPI1_SCK; 
            pinMapping[25, 1] = GpioFunction.UART1_CTS;
            pinMapping[25, 2] = GpioFunction.I2C0_SCL; 
            pinMapping[25, 3] = GpioFunction.PWM4_B; 
            pinMapping[25, 4] = GpioFunction.SIO; 
            pinMapping[25, 5] = GpioFunction.PIO0; 
            pinMapping[25, 6] = GpioFunction.PIO1; 
            pinMapping[25, 7] = GpioFunction.CLOCK_GPOUT3; 
            pinMapping[25, 8] = GpioFunction.USB_VBUS_DET;

            pinMapping[26, 0] = GpioFunction.SPI1_TX; 
            pinMapping[26, 1] = GpioFunction.UART1_RTS;
            pinMapping[26, 2] = GpioFunction.I2C1_SDA; 
            pinMapping[26, 3] = GpioFunction.PWM5_A; 
            pinMapping[26, 4] = GpioFunction.SIO; 
            pinMapping[26, 5] = GpioFunction.PIO0; 
            pinMapping[26, 6] = GpioFunction.PIO1; 
            pinMapping[26, 7] = GpioFunction.NONE; 
            pinMapping[26, 8] = GpioFunction.USB_VBUS_EN;

            pinMapping[27, 0] = GpioFunction.SPI1_RX; 
            pinMapping[27, 1] = GpioFunction.UART0_TX;
            pinMapping[27, 2] = GpioFunction.I2C1_SCL; 
            pinMapping[27, 3] = GpioFunction.PWM5_B; 
            pinMapping[27, 4] = GpioFunction.SIO; 
            pinMapping[27, 5] = GpioFunction.PIO0; 
            pinMapping[27, 6] = GpioFunction.PIO1; 
            pinMapping[27, 7] = GpioFunction.NONE; 
            pinMapping[27, 8] = GpioFunction.USB_OVCUR_DET;

            pinMapping[28, 0] = GpioFunction.SPI1_CSN; 
            pinMapping[28, 1] = GpioFunction.UART0_RX;
            pinMapping[28, 2] = GpioFunction.I2C0_SDA; 
            pinMapping[28, 3] = GpioFunction.PWM6_A; 
            pinMapping[28, 4] = GpioFunction.SIO; 
            pinMapping[28, 5] = GpioFunction.PIO0; 
            pinMapping[28, 6] = GpioFunction.PIO1; 
            pinMapping[28, 7] = GpioFunction.NONE; 
            pinMapping[28, 8] = GpioFunction.USB_VBUS_DET;

            pinMapping[29, 0] = GpioFunction.SPI1_CSN; 
            pinMapping[29, 1] = GpioFunction.UART0_RX;
            pinMapping[29, 2] = GpioFunction.I2C0_SCL; 
            pinMapping[29, 3] = GpioFunction.PWM6_B; 
            pinMapping[29, 4] = GpioFunction.SIO; 
            pinMapping[29, 5] = GpioFunction.PIO0; 
            pinMapping[29, 6] = GpioFunction.PIO1; 
            pinMapping[29, 7] = GpioFunction.NONE; 
            pinMapping[29, 8] = GpioFunction.USB_VBUS_EN;

            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            for (int i = 0; i < NumberOfPins; ++i)
            {
                functionSelect[i] = 0;
                pullDown[i] = true;
                pullUp[i] = false;
                outputEnableOverride[i] = OutputEnableOverride.Peripheral;
                forcedOutputDisableMap[i] = false;
                outputOverride[i] = OutputOverride.Peripheral;
                peripheralDrive[i] = PeripheralDrive.None;
            }
        }

        [ConnectionRegion("XOR")]
        public virtual void WriteDoubleWordXor(long offset, uint value)
        {
            registers.Write(offset, registers.Read(offset) ^ value);
        }

        [ConnectionRegion("SET")]
        public virtual void WriteDoubleWordSet(long offset, uint value)
        {
            registers.Write(offset, registers.Read(offset) | value);
        }

        [ConnectionRegion("CLEAR")]
        public virtual void WriteDoubleWordClear(long offset, uint value)
        {
            registers.Write(offset, registers.Read(offset) & (~value));
        }

        [ConnectionRegion("XOR")]
        public virtual uint ReadDoubleWordXor(long offset)
        {
            return registers.Read(offset);
        }

        [ConnectionRegion("SET")]
        public virtual uint ReadDoubleWordSet(long offset)
        {
            return registers.Read(offset);
        }

        [ConnectionRegion("CLEAR")]
        public virtual uint ReadDoubleWordClear(long offset)
        {
            return registers.Read(offset);
        }

        private bool IsPinOutput(int pin)
        {
            return forcedOutputDisableMap[pin] == false && outputEnableOverride[pin] != OutputEnableOverride.Disable;
        }

        public void SubscribeOnFunctionChange(Action<int, GpioFunction> callback)
        {
            functionSelectCallbacks.Add(callback);
        }

        public IGPIO GetGpio(int id)
        {
            return Connections[id];
        }

        public enum GpioFunction
        {
            SPI0_RX,
            SPI0_CSN,
            SPI0_SCK,
            SPI0_TX,
            SPI1_RX,
            SPI1_CSN,
            SPI1_SCK,
            SPI1_TX,
            UART0_TX,
            UART0_RX,
            UART0_CTS,
            UART0_RTS,
            UART1_TX,
            UART1_RX,
            UART1_CTS,
            UART1_RTS,
            I2C0_SDA,
            I2C0_SCL,
            I2C1_SDA,
            I2C1_SCL,
            PWM0_A,
            PWM0_B,
            PWM1_A,
            PWM1_B,
            PWM2_A,
            PWM2_B,
            PWM3_A,
            PWM3_B,
            PWM4_A,
            PWM4_B,
            PWM5_A,
            PWM5_B,
            PWM6_A,
            PWM6_B,
            PWM7_A,
            PWM7_B,
            SIO,
            PIO0,
            PIO1,
            CLOCK_GPIN0,
            CLOCK_GPIN1,
            CLOCK_GPOUT0,
            CLOCK_GPOUT1,
            CLOCK_GPOUT2,
            CLOCK_GPOUT3,
            USB_OVCUR_DET,
            USB_VBUS_DET,
            USB_VBUS_EN,
            NONE
        };

        private GpioFunction GetFunction(int pin)
        {
            // temporary check for QSPI GPIO, provide this by flag
            if (NumberOfPins == 6)
            {
                return GpioFunction.NONE;
            }

            if (functionSelect[pin] == 0 || functionSelect[pin] > 9)
            {
                return GpioFunction.NONE;
            }

            return pinMapping[pin, functionSelect[pin] - 1];
        }

        private void EvaluatePinInterconnections(int[] previous)
        {
            for (int i = 0; i < NumberOfPins; ++i)
            {
                if (previous[i] != functionSelect[i])
                {
                    this.Log(LogLevel.Noisy, "GPIO" + i + ": has function: " + GetFunction(i));
                    foreach (var action in functionSelectCallbacks)
                    {
                        action(i, GetFunction(i));
                    }
                }
            }
        }

        private void SetPinAccordingToPulls(int pin)
        {
            if (pullDown[pin] == true)
            {
                State[pin] = false;
                Connections[pin].Set(false);
            }
            if (pullUp[pin] == true)
            {
                State[pin] = true;
                Connections[pin].Set(true);
            }
        }

        private DoubleWordRegisterCollection CreateRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>();
            // That's true for both GPIO and QSPI GPIO  
            // 0x00 + 0x8 * i = STATUSES
            // 0x04 + 0x8 * i = CTRL
            // NumberOfPins * 0x8 = INTR0

            for (int p = 0; p < NumberOfPins; ++p)
            {
                int i = p;

                registersMap[i * 8] = new DoubleWordRegister(this)
                    .WithReservedBits(0, 8)
                    .WithFlag(8, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_OUTFROMPERI")
                    .WithFlag(9, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_OUTTOPAD")
                    .WithReservedBits(10, 2)
                    .WithFlag(12, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_OEFROMPERI")
                    .WithFlag(13, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_OETOPAD")
                    .WithReservedBits(14, 3)
                    .WithFlag(17, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_INFROMPAD")
                    .WithReservedBits(18, 1)
                    .WithFlag(19, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_INTOPERI")
                    .WithReservedBits(20, 4)
                    .WithFlag(24, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_IRQFROMPAD")
                    .WithReservedBits(25, 1)
                    .WithFlag(26, FieldMode.Read,
                        valueProviderCallback: _ => State[i],
                        name: "GPIO" + i + "_STATUS_IRQTOPROC")
                    .WithReservedBits(27, 5);

                registersMap[i * 8 + 0x04] = new DoubleWordRegister(this)
                    .WithValueField(0, 5, FieldMode.Read | FieldMode.Write,
                        valueProviderCallback: _ =>
                        {
                            return (ulong)functionSelect[i];
                        },
                        writeCallback: (_, value) =>
                        {
                            int[] oldFunctions = new int[NumberOfConnections];
                            functionSelect.CopyTo(oldFunctions, 0);
                            functionSelect[i] = (int)value;
                            EvaluatePinInterconnections(oldFunctions);
                        },
                        name: "GPIO" + i + "_CTRL_FUNCSEL")
                    .WithReservedBits(5, 3)
                    .WithValueField(8, 2,
                        valueProviderCallback: _ =>
                        {
                            return (ulong)outputOverride[i];
                        },
                        writeCallback: (_, value) =>
                        {
                            outputOverride[i] = (OutputOverride)value;
                            if (outputOverride[i] == OutputOverride.Peripheral || outputOverride[i] == OutputOverride.InversePeripheral)
                            {
                                // left the job for a peripheral
                                return;
                            }

                            if (IsPinOutput(i))
                            {
                                if (outputOverride[i] == OutputOverride.Low)
                                {
                                    WritePin(i, false, GetFunction(i));
                                }
                                else if (outputOverride[i] == OutputOverride.High)
                                {
                                    WritePin(i, true, GetFunction(i));
                                }
                            }
                            else
                            {
                                this.Log(LogLevel.Error, "GPIO " + i + ": Set output but not output");
                                SetPinAccordingToPulls(i);
                            }
                        },
                        name: "GPIO" + i + "_CTRL_OUTOVER")
                    .WithReservedBits(10, 2)
                    .WithValueField(12, 2,
                        valueProviderCallback: _ =>
                        {
                            return (ulong)outputEnableOverride[i];
                        },
                        writeCallback: (_, value) =>
                        {
                            outputEnableOverride[i] = (OutputEnableOverride)value;
                            switch (value)
                            {
                                case 0:
                                case 1:
                                    {
                                        // let a peripheral enable output
                                        break;
                                    }
                                case 2:
                                    {
                                        SetPinAccordingToPulls(i);
                                        break;
                                    }
                                case 3:
                                    {
                                        WritePin(i, outputOverride[i] == OutputOverride.High, GetFunction(i));
                                        break;
                                    }
                            }
                        },
                        name: "GPIO" + i + "_CTRL_OEOVER")
                    .WithReservedBits(14, 2)
                    .WithValueField(16, 2, name: "GPIO" + i + "_CTRL_INOVER")
                    .WithReservedBits(18, 10)
                    .WithValueField(28, 2, name: "GPIO" + i + "_CTRL_IRQOVER")
                    .WithReservedBits(30, 2);
            }

            int intr0p0 = NumberOfPins * 8;
            int numberOfIntRegisters = (int)Math.Ceiling((decimal)NumberOfPins / 8);
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[intr0p0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTR0" + p + "_PROC0");
            }

            int inte0p0 = intr0p0 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[inte0p0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTE0" + p + "_PROC0");
            }


            int intf0p0 = inte0p0 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[intf0p0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTF0" + p + "_PROC0");
            }


            int ints0p0 = intf0p0 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[ints0p0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTS0" + p + "_PROC0");
            }

            int intr0p1 = ints0p0 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[intr0p1 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTR0" + p + "_PROC1");
            }

            int inte0p1 = intr0p1 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[inte0p1 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTE0" + p + "_PROC1");
            }

            int intf0p1 = inte0p1 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[intf0p1 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTF0" + p + "_PROC1");
            }

            int ints0p1 = intf0p1 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[ints0p1 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "INTS0" + p + "_PROC1");
            }

            int dormantWakeInte0 = ints0p1 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[dormantWakeInte0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "DORMANT_WAKE_INTE" + p);
            }

            int dormantWakeIntf0 = dormantWakeInte0 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[dormantWakeIntf0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "DORMANT_WAKE_INTF" + p);
            }

            int dormantWakeInts0 = dormantWakeIntf0 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                registersMap[dormantWakeInts0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "DORMANT_WAKE_INTS" + p);
            }

            return new DoubleWordRegisterCollection(this, registersMap);
        }

        public bool GetPullDown(int pin)
        {
            return pullDown[pin];
        }

        public bool GetPullUp(int pin)
        {
            return pullUp[pin];
        }

        public void SetPullDown(int pin, bool state)
        {
            pullDown[pin] = state;
            if (!IsPinOutput(pin) && state == true)
            {
                State[pin] = false;
                Connections[pin].Set(false);
            }
        }

        public void SetPullUp(int pin, bool state)
        {
            pullUp[pin] = state;
            if (!IsPinOutput(pin) && state == true)
            {
                State[pin] = true;
                Connections[pin].Set(true);
            }
        }

        public void SetPinOutput(int pin, bool state)
        {
            outputEnableOverride[pin] = state ? OutputEnableOverride.Enable : OutputEnableOverride.Disable;
        }

        // Disable from PADS has greater priority than from GPIO
        public bool IsPinOutputForcedDisabled(int pin)
        {
            return forcedOutputDisableMap[pin];
        }

        public void ForcePinOutputDisable(int pin, bool state)
        {
            forcedOutputDisableMap[pin] = state;
        }

        public bool GetGpioState(uint number)
        {
            return State[number];
        }

        public uint GetGpioStateBitmap()
        {
            lock (State)
            {
                uint output = 0;
                for (int i = 0; i < NumberOfPins; ++i)
                {
                    output |= Convert.ToUInt32(State[i]) << i;
                }
                return output;
            }
        }

        public void SetGpioBitmap(ulong bitmap, GpioFunction peri)
        {
            lock (State)
            {
                for (int i = 0; i < NumberOfPins; ++i)
                {
                    if ((bitmap & (1UL << i)) != 0)
                    {
                        WritePin(i, true, peri);
                    }
                    else 
                    {
                        WritePin(i, false, peri);
                    }
                }
                OperationDone.Toggle();
            }
        }

        public void SetGpioBitset(ulong bitset, GpioFunction peri, ulong bitmask = 0xfffffff)
        {
            lock (State)
            {
                for (int i = 0; i < NumberOfPins; ++i)
                {
                    if ((bitmask & (1UL << i)) == 0)
                    {
                        continue;
                    }

                    if ((bitset & (1UL << i)) != 0)
                    {
                        WritePin(i, true, peri);
                    }
                }
                OperationDone.Toggle();
            }
        }

        public void SetPinDirectionBitset(ulong bitset, ulong bitmask = 0xffffffff)
        {
            lock (State)
            {
                for (int i = 0; i < NumberOfPins; ++i)
                {
                    if ((bitmask & (1UL << i)) == 0)
                    {
                        continue;
                    }

                    if ((bitset & (1UL << i)) != 0)
                    {
                        outputEnableOverride[i] = OutputEnableOverride.Enable;
                    }
                    else
                    {
                        outputEnableOverride[i] = OutputEnableOverride.Disable;
                    }
                }
            }
        }

        public void ClearGpioBitset(ulong bitset, GpioFunction peri)
        {
            lock (State)
            {
                for (int i = 0; i < NumberOfPins; ++i)
                {
                    if ((bitset & (1UL << i)) != 0)
                    {
                        WritePin(i, false, peri);
                    }
                }
            }
        }

        public void XorGpioBitset(ulong bitset, GpioFunction peri)
        {
            lock (State)
            {
                for (int i = 0; i < NumberOfPins; ++i)
                {
                    bool state = State[i];
                    if ((bitset & (1UL << i)) != 0)
                    {
                        state = state ^ true;
                    }
                    else
                    {
                        state = state ^ false;
                    }
                    WritePin(i, state, peri);
                }
            }
        }

        public void ClearOutputEnableBitset(ulong bitset, GpioFunction peri)
        {
            lock (outputEnableOverride)
            {
                for (int i = 0; i < NumberOfPins; ++i)
                {
                    bool enable = (bitset & (1UL << i)) != 0;
                    if (outputEnableOverride[i] == OutputEnableOverride.Peripheral || outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                    {
                        if (GetFunction(i) != peri)
                        {
                            continue;
                        }

                        if (outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                        {
                            enable = !enable;
                        }
                    }


                    if (enable)
                    {
                        outputEnableOverride[i] = OutputEnableOverride.Disable;
                    }
                }
            }
        }

        public void XorOutputEnableBitset(ulong bitset, GpioFunction peri)
        {
            lock (outputEnableOverride)
            {
                for (int i = 0; i < NumberOfPins; ++i)
                {
                    bool state;

                    bool enable = (bitset & (1UL << i)) != 0;
                    if (outputEnableOverride[i] == OutputEnableOverride.Peripheral || outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                    {
                        if (GetFunction(i) != peri)
                        {
                            continue;
                        }

                        if (outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                        {
                            enable = !enable;
                        }
                    }


                    if (enable)
                    {
                        state = IsPinOutput(i) ^ true;
                    }
                    else
                    {
                        state = IsPinOutput(i) ^ false;
                    }
                    outputEnableOverride[i] = state ? OutputEnableOverride.Enable : OutputEnableOverride.Disable;
                }
            }
        }

        public uint GetOutputEnableBitmap()
        {
            lock (outputEnableOverride)
            {
                uint output = 0;
                for (int i = 0; i < NumberOfPins; ++i)
                {
                    output |= Convert.ToUInt32(outputEnableOverride[i] == OutputEnableOverride.Enable) << i;
                }
                return output;
            }
        }

        public void SetOutputEnableBitmap(ulong bitmap, GpioFunction peri)
        {
            lock (outputEnableOverride)
            {
                for (int i = 0; i < NumberOfPins; ++i)
                {
                    bool enable = (bitmap & (1UL << i)) != 0;
                    if (outputEnableOverride[i] == OutputEnableOverride.Peripheral || outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                    {
                        if (GetFunction(i) != peri)
                        {
                            continue;
                        }

                        if (outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                        {
                            enable = !enable;
                        }
                    }

                    if (enable)
                    {
                        outputEnableOverride[i] = OutputEnableOverride.Enable;
                    }
                    else
                    {
                        outputEnableOverride[i] = OutputEnableOverride.Disable;
                    }
                }
            }
        }

        public void SetOutputEnableBitset(ulong bitset, GpioFunction peri, ulong bitmask = 0xfffffff)
        {
            lock (outputEnableOverride)
            {
                for (int i = 0; i < NumberOfPins; ++i)
                {
                    if ((bitmask & (1UL << i)) == 0)
                    {
                        continue;
                    }

                    bool enable = (bitset & (1UL << i)) != 0;
                    if (outputEnableOverride[i] == OutputEnableOverride.Peripheral || outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                    {
                        if (GetFunction(i) != peri)
                        {
                            continue;
                        }

                        if (outputEnableOverride[i] == OutputEnableOverride.InversePeripheral)
                        {
                            enable = !enable;
                        }
                    }

                    if (enable)
                    {
                        outputEnableOverride[i] = OutputEnableOverride.Enable;
                    }
                    else
                    {
                        outputEnableOverride[i] = OutputEnableOverride.Disable;
                    }
                }
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void OnGPIO(int number, bool value)
        {
            WritePin(number, value, GetFunction(number));
            base.OnGPIO(number, value);
        }

        // most probably may hide some bugs, but full emulation of gpio function interconnection may not be necessary in most cases
        public void WritePin(int number, bool value)
        {
            WritePin(number, value, GetFunction(number));
        }

        public void TogglePin(int number)
        {
            WritePin(number, !State[number], GetFunction(number));
        }

        public void WritePin(int number, bool value, GpioFunction peri)
        {
            if (State[number] == value)
            {
                return;
            }
            this.Log(LogLevel.Noisy, "Setting GPIO" + number + " to: " + value + ", time: " + machine.ElapsedVirtualTime.TimeElapsed + ", from: " + peri);

            if (peripheralDrive[number] != PeripheralDrive.None && GetFunction(number) != peri)
            {
                this.Log(LogLevel.Error, "Driving GPIO from not selected peripheral, gpio configured with: " + GetFunction(number) + ". Request received from: " + peri);
            }

            if (peripheralDrive[number] == PeripheralDrive.Inverse)
            {
                value = !value;
            }
            State[number] = value;
            // Connections[number].Set(value);
        }

        public void ReevaluatePio(uint steps)
        {
            foreach (var a in ReevaluatePioActions)
            {
                a(steps);
            }
        }

        public long Size { get { return 0x1000; } }

        public const ulong aliasSize = 0x1000;
        public const ulong xorAliasOffset = 0x1000;
        public const ulong setAliasOffset = 0x2000;
        public const ulong clearAliasOffset = 0x3000;
        public int[] functionSelect;

        public int NumberOfPins;

        // Currently I have no better idea how to retrigger CPU evaluation when GPIO state changes 
        // This is necessary to have synchronized PIO with System Clock
        public List<Action<uint>> ReevaluatePioActions { get; set; }
        public GPIO OperationDone { get; }

        private readonly DoubleWordRegisterCollection registers;
        private bool[] pullDown;
        private bool[] pullUp;
        private enum OutputOverride
        {
            Peripheral = 0,
            InversePeripheral = 1,
            Low = 2,
            High = 3
        };

        private OutputOverride[] outputOverride;
        private enum OutputEnableOverride
        {
            Peripheral = 0,
            InversePeripheral = 1,
            Disable = 2,
            Enable = 3
        };
        private OutputEnableOverride[] outputEnableOverride;
        private bool[] forcedOutputDisableMap;

        private List<Action<int, GpioFunction>> functionSelectCallbacks;

        private enum PeripheralDrive
        {
            None,
            Normal,
            Inverse
        };
        private PeripheralDrive[] peripheralDrive;
        private readonly GpioFunction[,] pinMapping;
    }

}
