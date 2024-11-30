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
    public class RP2350GPIO : BaseGPIOPort, IRP2040Peripheral, IDoubleWordPeripheral, IGPIOReceiver, IKnownSize
    {
        public RP2040GPIO(IMachine machine, int numberOfPins, int numberOfCores, ulong address) : base(machine, numberOfPins)
        {
            functionSelectCallbacks = new List<Action<int, RpGpioFunction>>();
            NumberOfPins = numberOfPins;
            this.numberOfCores = numberOfCores;
            registers = CreateRegisters();
            functionSelect = new int[NumberOfPins];
            ReevaluatePioActions = new List<Action<uint>>();
            pullDown = new bool[NumberOfPins];
            pullUp = new bool[NumberOfPins];
            outputEnableOverride = new OutputEnableOverride[NumberOfPins];
            forcedOutputDisableMap = new bool[NumberOfPins];
            outputOverride = new OutputOverride[NumberOfPins];
            peripheralDrive = new PeripheralDrive[NumberOfPins];
            
            edgeHighState = new bool[NumberOfPins];
            edgeLowState = new bool[NumberOfPins];
            irqProc = new GpioIrqEnableState[numberOfCores, NumberOfPins];
            irqForced = new GpioIrqEnableState[numberOfCores, NumberOfPins];

            OperationDone = new GPIO();
            IRQ = new GPIO[numberOfCores];
            for (int i = 0; i < numberOfCores; ++i)
            {
                IRQ[i] = new GPIO();
            }
            pinMapping = RpGpioFunctionBuilder.BuildForRp2350();

            machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + xorAliasOffset, aliasSize, "XOR"));
            machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + setAliasOffset, aliasSize, "SET"));
            machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + clearAliasOffset, aliasSize, "CLEAR"));
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
                edgeLowState[i] = false;
                edgeHighState[i] = false;
                
                for (int c = 0; c < numberOfCores; ++c)
                {
                    irqProc[c, i].EdgeHigh = false;
                    irqProc[c, i].EdgeLow = false;
                    irqProc[c, i].LevelHigh = false;
                    irqProc[c, i].LevelLow = false;
                    IRQ[c].Unset();
                }

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

        public void SubscribeOnFunctionChange(Action<int, RpGpioFunction> callback)
        {
            functionSelectCallbacks.Add(callback);
        }

        public IGPIO GetGpio(int id)
        {
            return Connections[id];
        }

        private RpGpioFunction GetFunction(int pin)
        {
            // temporary check for QSPI GPIO, provide this by flag
            if (NumberOfPins == 6)
            {
                return RpGpioFunction.NONE;
            }

            if (functionSelect[pin] == 0 || functionSelect[pin] > 9)
            {
                return RpGpioFunction.NONE;
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
                        valueProviderCallback: _ => IsPinOutput(i),
                        name: "GPIO" + i + "_STATUS_OEFROMPERI")
                    .WithFlag(13, FieldMode.Read,
                        valueProviderCallback: _ => IsPinOutput(i),
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
                        valueProviderCallback: _ => IRQ1.IsSet || IRQ0.IsSet,
                        name: "GPIO" + i + "_STATUS_IRQFROMPAD")
                    .WithReservedBits(25, 1)
                    .WithFlag(26, FieldMode.Read,
                        valueProviderCallback: _ => IRQ1.IsSet || IRQ0.IsSet,
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
                            this.Log(LogLevel.Noisy, "Setting GPIO{0} output override to: {1}", i, value);
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
                            this.Log(LogLevel.Noisy, "Setting GPIO{0} output enable override to: {1}", i, value);
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
                int startingPin = p * 8;
                registersMap[intr0p0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => {
                        return BuildRawInterruptsForCore(0, startingPin);
                    }, writeCallback: (_, value) => {
                        ClearRawInterrupts(startingPin, value);
                        IRQ0.Unset();
                    }, name: "INTR0" + p + "_PROC0");
            }

            int inte0p0 = intr0p0 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                int startingPin = p * 8;
                registersMap[inte0p0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => {
                        return GetEnabledInterruptsForCore(0, startingPin);
                    }, writeCallback: (_, value) => {
                        IRQ0.Unset();
                        ClearRawInterrupts(startingPin, ~value);
                        EnableInterruptsForCore(0, startingPin, value);
                    }, name: "INTE0" + p + "_PROC0");
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
                int startingPin = p * 8;
                registersMap[ints0p0 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => {
                        return BuildRawInterruptsForCore(0, startingPin, true);
                    }, name: "INTS0" + p + "_PROC0");
            }

            int intr0p1 = ints0p0 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                int startingPin = p * 8;
                registersMap[intr0p1 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, value) => {
                        ClearRawInterrupts(startingPin, value);
                        IRQ1.Unset();
                    }, valueProviderCallback: _ => {
                        return BuildRawInterruptsForCore(1, startingPin);
                    }, name: "INTR0" + p + "_PROC1");
            }

            int inte0p1 = intr0p1 + numberOfIntRegisters * 4;
            // 8 pins per register
            for (int p = 0; p < numberOfIntRegisters; ++p)
            {
                int startingPin = p * 8;
                registersMap[inte0p1 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => {
                        return GetEnabledInterruptsForCore(1, startingPin);
                    }, writeCallback: (_, value) => {
                        IRQ0.Unset();
                        ClearRawInterrupts(startingPin, ~value);
                        EnableInterruptsForCore(1, startingPin, value);
                    },
                    name: "INTE0" + p + "_PROC1");
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
                int startingPin = p * 8;
                registersMap[ints0p1 + p * 4] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => {
                        return BuildRawInterruptsForCore(1, startingPin, true);
                    },
                    name: "INTS0" + p + "_PROC1");
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
            if (state == true)
            {
                State[pin] = false;
                Connections[pin].Set(false);
            }
        }

        public void SetPullUp(int pin, bool state)
        {
            pullUp[pin] = state;
            if (state == true)
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

        public void SetGpioBitmap(ulong bitmap, RpGpioFunction peri)
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

        public void SetGpioBitset(ulong bitset, RpGpioFunction peri, ulong bitmask = 0xfffffff)
        {
            lock (State)
            {
                for (int i = 0; i < NumberOfPins; ++i)
                {
                    if (((bitset & bitmask) & (1UL << i)) != 0)
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
                    if ((bitmask & (1UL << i)) == 0) continue;

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

        public void ClearGpioBitset(ulong bitset, RpGpioFunction peri)
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

        public void XorGpioBitset(ulong bitset, RpGpioFunction peri)
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

        public void ClearOutputEnableBitset(ulong bitset, RpGpioFunction peri)
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

        public void XorOutputEnableBitset(ulong bitset, RpGpioFunction peri)
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

        public void SetOutputEnableBitmap(ulong bitmap, RpGpioFunction peri)
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

        public void SetOutputEnableBitset(ulong bitset, RpGpioFunction peri, ulong bitmask = 0xfffffff)
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
            WritePin(number, value, GetFunction(number), true);
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

        public void WritePin(int number, bool value, RpGpioFunction peri, bool forced = false)
        {
            if (State[number] == value)
            {
                return;
            }

            if (!IsPinOutput(number) && !forced)
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

            // we have edge, so mark it
            if (value)
            {
                edgeHighState[number] = true;
                for (int c = 0; c < numberOfCores; ++c)
                {
                    if (irqProc[c, number].EdgeHigh || irqProc[c, number].LevelHigh)
                    {
                        this.Log(LogLevel.Noisy, "Sending IRQ to core: {0}, for pin: {1}", c, number);
                        IRQ[c].Set(true);
                    }
                }
            }
            else 
            {
                edgeLowState[number] = true;
                for (int c = 0; c < numberOfCores; ++c)
                {
                    if (irqProc[c, number].EdgeLow || irqProc[c, number].LevelLow)
                    {
                        this.Log(LogLevel.Noisy, "Sending IRQ to core: {0}, for pin: {1}", c, number);
                        IRQ[c].Set(true);
                    }
                }
            }

            Connections[number].Set(value);
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

        public readonly int NumberOfPins;
        public GPIO[] IRQ;
        public GPIO IRQ0 => IRQ[0];
        public GPIO IRQ1 => IRQ[1];

        // Currently I have no better idea how to retrigger CPU evaluation when GPIO state changes 
        // This is necessary to have synchronized PIO with System Clock
        public List<Action<uint>> ReevaluatePioActions { get; set; }
        public GPIO OperationDone { get; }
        private uint BuildRawInterruptsForCore(int core, int startingPin, bool checkForce = false)
        {
            uint ret = 0;
            for (int i = 0; i < 8; ++i)
            {
                int pin = i + startingPin;
                if (pin >= NumberOfPins) break;
                ret |= ((State[pin] == false) && irqProc[core, pin].LevelLow) ? 1u << (i * 4) : 0;
                ret |= ((State[pin] == true) && irqProc[core, pin].LevelHigh) ? 1u << (i * 4 + 1) : 0;
                ret |= (edgeLowState[pin] && irqProc[core, pin].EdgeLow) == true ? 1u << (i * 4 + 2) : 0;
                ret |= (edgeHighState[pin] && irqProc[core, pin].EdgeHigh) == true ? 1u << (i * 4 + 3) : 0;
            }

            if (checkForce)
            {
                for (int i = 0; i < 8; ++i)
                {
                     int pin = i + startingPin;
                     if (pin >= NumberOfPins) break;
                     ret |= irqForced[core, pin].LevelLow ? 1u << (i * 4) : 0;
                     ret |= irqForced[core, pin].LevelHigh ? 1u << (i * 4 + 1) : 0;
                     ret |= irqForced[core, pin].EdgeLow ? 1u << (i * 4 + 2) : 0;
                     ret |= irqForced[core, pin].EdgeHigh ? 1u << (i * 4 + 3) : 0;
                 }
            }
 
            return ret;
        }

        private void ClearRawInterrupts(int startingPin, ulong value)
        {
            for (int i = 0; i < 8; ++i)
            {
                int pin = startingPin + i;
                if (pin >= NumberOfPins) break;
                if ((value & (1u << (i * 4) + 2)) != 0)
                {
                    edgeLowState[pin] = false;
                } 
                if ((value & (1u << (i * 4) + 3)) != 0)
                {
                    edgeHighState[pin] = false;
                } 
            }
        }

        private void EnableInterruptsForCore(int core, int startingPin, ulong value)
        {
            this.Log(LogLevel.Noisy, "Enabling interrupts for core: {0}, starting pin: {1}, value: 0x{2:x}", core, startingPin, value);
            for (int i = 0; i < 8; ++i)
            {
                int pin = i + startingPin;
                if (pin >= NumberOfPins) break;
                bool levelLow = (value & (1u << (i * 4))) != 0;
                if (levelLow)
                {
                    this.Log(LogLevel.Noisy, "Enabling level low interrupt for pin: {0}, current: {1}", pin, State[pin]);
                    if (!State[pin])
                    {
                        IRQ[core].Set(true);
                    }
                }
                irqProc[core, pin].LevelLow = levelLow;

                bool levelHigh = (value & (1u << (i * 4 + 1))) != 0;
                if (levelHigh)
                {
                    this.Log(LogLevel.Noisy, "Enabling level high interrupt for pin: {0}, current: {1}", pin, State[pin]);
                    if (State[pin])
                    {
                        IRQ[core].Set(true);
                    }
                }
                irqProc[core, pin].LevelHigh = levelHigh;

                bool edgeLow = (value & (1u << (i * 4 + 2))) != 0;
                if (edgeLow)
                {
                    this.Log(LogLevel.Noisy, "Enabling edge low interrupt for pin: {0}", pin);
                }
                irqProc[core, pin].EdgeLow = edgeLow;

                bool edgeHigh = (value & (1u << (i * 4 + 3))) != 0;
                if (edgeHigh)
                {
                    this.Log(LogLevel.Noisy, "Enabling edge high interrupt for pin: {0}", pin);
                }
                irqProc[core, pin].EdgeHigh = edgeHigh;
            }
        }

        private uint GetEnabledInterruptsForCore(int core, int startingPin)
        {
            uint ret = 0;
            for (int i = 0; i < 8; ++i)
            {
                int pin = i + startingPin;
                if (pin >= NumberOfPins) break;
                ret |= irqProc[core, pin].LevelLow ? 1u << (i * 4) : 0;
                ret |= irqProc[core, pin].LevelHigh ? 1u << (i * 4 + 1) : 0;
                ret |= irqProc[core, pin].EdgeLow == true ? 1u << (i * 4 + 2) : 0;
                ret |= irqProc[core, pin].EdgeHigh == true ? 1u << (i * 4 + 3) : 0;
            }
            return ret;
        }

        private readonly int numberOfCores;
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

        private List<Action<int, RpGpioFunction>> functionSelectCallbacks;

        private enum PeripheralDrive
        {
            None,
            Normal,
            Inverse
        };
        private PeripheralDrive[] peripheralDrive;
        private readonly RpGpioFunction[,] pinMapping;

        private struct GpioIrqEnableState 
        {
            public bool EdgeHigh;
            public bool EdgeLow;
            public bool LevelHigh;
            public bool LevelLow;
        };

        private GpioIrqEnableState[,] irqProc;
        private GpioIrqEnableState[,] irqForced;

        private bool[] edgeLowState;
        private bool[] edgeHighState;
    }

}
