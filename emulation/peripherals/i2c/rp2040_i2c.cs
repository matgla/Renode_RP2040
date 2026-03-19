/**
 * rp2040_i2c.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.GPIOPort;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class RP2040I2C : SimpleContainer<II2CPeripheral>, II2CPeripheral, IDoubleWordPeripheral,
        IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize, IGPIOReceiver
    {
        public RP2040I2C(IMachine machine, RP2040GPIO gpio, int id, RP2040Clocks clocks) : base(machine)
        {
            this.gpio = gpio;
            this.id = id;
            this.clocks = clocks;

            sdaPins = new List<int>();
            sclPins = new List<int>();

            // Initialize FIFOs
            txFifo = new Queue<I2CDataCommand>(FIFO_DEPTH);
            rxFifo = new Queue<byte>(FIFO_DEPTH);

            // Initialize IRQ and DMA signals
            IRQ = new GPIO();
            DmaTransmitRequest = new GPIO();
            DmaReceiveRequest = new GPIO();

            // Subscribe to GPIO function changes
            gpio.SubscribeOnFunctionChange(OnGpioFunctionChange);

            // Keep the managed thread available for optional future bit-level emulation,
            // but handle normal master-mode traffic synchronously from IC_DATA_CMD writes.
            i2cThread = machine.ObtainManagedThread(I2CStep, 1000000);
            i2cThread.Stop();

            registers = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            // Reset register fields to default values
            masterMode.Value = true;
            speed.Value = 0x2; // Fast mode (400kHz)
            address10BitsSlave.Value = false;
            address10BitsMaster.Value = false;
            restartEnabled.Value = true;
            slaveDisable.Value = true;
            stopDetectionIfAddressed.Value = false;
            txEmptyControl.Value = false;
            rxFifoFullHoldControl.Value = false;
            stopDetectionIfMasterActive.Value = false;

            icTar.Value = 0x055;
            gcOrStart.Value = false;
            special.Value = false;

            icSar.Value = 0x055;

            icSsSclHcnt.Value = 0x0028;
            icSsSclLcnt.Value = 0x002f;
            icFsSclHcnt.Value = 0x0006;
            icFsSclLcnt.Value = 0x000d;

            // Reset enable and status
            i2cEnabled.Value = false;
            abort.Value = false;

            // Reset FIFO thresholds
            rxFifoThreshold.Value = 0;
            txFifoThreshold.Value = 0;

            // Reset interrupt mask (all masked by default except STOP_DET)
            intrMask.Value = 0x000008FF;

            // Reset DMA control
            dmaTxEnable.Value = false;
            dmaRxEnable.Value = false;
            dmaTxThreshold.Value = 0;
            dmaRxThreshold.Value = 0;

            // Reset SDA hold
            sdaHoldTx.Value = 0x01;
            sdaHoldRx.Value = 0x00;

            // Clear FIFOs
            txFifo.Clear();
            rxFifo.Clear();

            // Reset interrupt status
            rawIntrStat = 0;

            // Reset state machine
            currentState = I2CState.Idle;
            bitCounter = 0;
            currentByte = 0;
            currentReadByte = 0;
            awaitingAck = false;
            isReadOperation = false;
            currentTargetAddress = 0;
            stateBeforeAck = I2CState.Idle;

            // Reset device communication
            currentDevice = null;
            writeBuffer.Clear();

            // Reset line control
            sdaDrivenLow = false;
            sclDrivenLow = false;

            // Stop thread
            i2cThread.Stop();

            // Reset IRQ and DMA signals
            IRQ.Unset();
            DmaTransmitRequest.Unset();
            DmaReceiveRequest.Unset();
            irqState = false;
            dmaTransmitRequestState = false;
            dmaReceiveRequestState = false;

            // Release bus lines
            ReleaseBusLines();

            UpdateInterrupts();
            UpdateDreqSignals();
        }

        #region II2CPeripheral Implementation

        public byte ReadByte(long offset)
        {
            // Not used for I2C controller - actual I2C communication is through registered devices
            return 0;
        }

        public void WriteByte(long offset, byte value)
        {
            // Not used for I2C controller
        }

        public void Write(byte[] data)
        {
            // This is called by connected slave devices to send data back to master
            // In read operations, data is placed in RX FIFO
            foreach (byte b in data)
            {
                if (rxFifo.Count < FIFO_DEPTH)
                {
                    rxFifo.Enqueue(b);
                }
                else
                {
                    // RX overflow
                    rawIntrStat |= (uint)InterruptBits.RX_OVER;
                    UpdateInterrupts();
                    break;
                }
            }
            UpdateDreqSignals();
            UpdateInterrupts();
        }

        public byte[] Read(int count = 1)
        {
            // Not typically used for I2C controller
            return new byte[0];
        }

        public void FinishTransmission()
        {
            // Called when STOP condition or repeated START detected
        }

        #endregion

        #region Register Access

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
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

        public long Size
        {
            get { return 0x1000; }
        }

        public DoubleWordRegisterCollection RegistersCollection => registers;

        #endregion

        #region GPIO Interface

        public void OnGPIO(int number, bool value)
        {
            // Handle GPIO changes if needed for multi-master scenarios
        }

        private void OnGpioFunctionChange(int pin, RP2040GPIO.GpioFunction function)
        {
            if (id == 0)
            {
                switch (function)
                {
                    case RP2040GPIO.GpioFunction.I2C0_SDA:
                        if (!sdaPins.Contains(pin)) sdaPins.Add(pin);
                        return;
                    case RP2040GPIO.GpioFunction.I2C0_SCL:
                        if (!sclPins.Contains(pin)) sclPins.Add(pin);
                        return;
                    case RP2040GPIO.GpioFunction.NONE:
                        sdaPins.Remove(pin);
                        sclPins.Remove(pin);
                        return;
                }
            }
            else if (id == 1)
            {
                switch (function)
                {
                    case RP2040GPIO.GpioFunction.I2C1_SDA:
                        if (!sdaPins.Contains(pin)) sdaPins.Add(pin);
                        return;
                    case RP2040GPIO.GpioFunction.I2C1_SCL:
                        if (!sclPins.Contains(pin)) sclPins.Add(pin);
                        return;
                    case RP2040GPIO.GpioFunction.NONE:
                        sdaPins.Remove(pin);
                        sclPins.Remove(pin);
                        return;
                }
            }
        }

        #endregion

        #region I2C Protocol Implementation

        private void I2CStep()
        {
            if (!i2cEnabled.Value)
            {
                return;
            }

            switch (currentState)
            {
                case I2CState.Idle:
                    HandleIdleState();
                    break;
                case I2CState.GeneratingStart:
                    HandleStartState();
                    break;
                case I2CState.SendingAddress:
                    HandleAddressState();
                    break;
                case I2CState.DataWrite:
                    HandleDataWriteState();
                    break;
                case I2CState.DataRead:
                    HandleDataReadState();
                    break;
                case I2CState.WaitForAck:
                    HandleWaitForAckState();
                    break;
                case I2CState.GeneratingStop:
                    HandleStopState();
                    break;
            }

            UpdateStatus();
            UpdateInterrupts();
            UpdateDreqSignals();
        }

        private void HandleIdleState()
        {
            if (txFifo.Count == 0)
            {
                i2cThread.Stop();
                return;
            }

            if (txFifo.Count > 0 && i2cEnabled.Value)
            {
                var cmd = txFifo.Peek();
                currentCommand = cmd;

                // Generate START condition: SDA goes low while SCL is high
                SetScl(true);   // Ensure SCL is high
                SetSda(false);  // SDA goes low (START)

                currentState = I2CState.GeneratingStart;
                bitCounter = 0;

                // Set activity status
                rawIntrStat |= (uint)InterruptBits.ACTIVITY;
                rawIntrStat |= (uint)InterruptBits.START_DET;
            }
        }

        private void HandleStartState()
        {
            if (currentDevice != null && writeBuffer.Count > 0)
            {
                currentDevice.Write(writeBuffer.ToArray());
                writeBuffer.Clear();
            }

            // After START, SCL goes low to prepare for sending address
            SetScl(false);
            SetSda(true);   // Release SDA

            // Load target address
            currentTargetAddress = (ushort)icTar.Value;
            isReadOperation = currentCommand.IsRead;

            // Prepare address byte (7-bit address + R/W bit)
            currentByte = (byte)((currentTargetAddress << 1) | (isReadOperation ? 1u : 0u));
            bitCounter = 0;

            currentState = I2CState.SendingAddress;
        }

        private void HandleAddressState()
        {
            // Shift out bits MSB first
            bool bit = ((currentByte >> (7 - bitCounter)) & 1) == 1;
            SetSda(bit);

            // Clock pulse
            SetScl(true);
            SetScl(false);

            bitCounter++;

            if (bitCounter >= 8)
            {
                // Address byte sent, wait for ACK
                bitCounter = 0;
                stateBeforeAck = I2CState.SendingAddress;
                awaitingAck = true;
                SetSda(true); // Release SDA for ACK
                currentState = I2CState.WaitForAck;
            }
        }

        private void HandleDataWriteState()
        {
            if (bitCounter == 0)
            {
                // Get next byte from TX FIFO
                if (txFifo.Count > 0)
                {
                    var cmd = txFifo.Dequeue();
                    currentByte = cmd.Data;

                    // Check if STOP should be generated after this byte
                    if (cmd.Stop)
                    {
                        pendingStop = true;
                    }

                    // Check if RESTART should be generated
                    if (cmd.Restart)
                    {
                        pendingRestart = true;
                    }
                }
                else
                {
                    // No more data, go to stop or idle
                    if (pendingStop)
                    {
                        currentState = I2CState.GeneratingStop;
                    }
                    else
                    {
                        currentState = I2CState.Idle;
                    }
                    return;
                }
            }

            // Shift out bits MSB first
            bool bit = ((currentByte >> (7 - bitCounter)) & 1) == 1;
            SetSda(bit);

            // Clock pulse
            SetScl(true);
            SetScl(false);

            bitCounter++;

            if (bitCounter >= 8)
            {
                // Byte sent, wait for ACK
                bitCounter = 0;
                stateBeforeAck = I2CState.DataWrite;
                awaitingAck = true;
                SetSda(true); // Release SDA for ACK
                currentState = I2CState.WaitForAck;
            }
        }

        private void HandleDataReadState()
        {
            if (bitCounter < 8)
            {
                // For read operations, we need to output data bits to SDA
                // Get the byte from somewhere - either from pre-loaded data or use 0xFF
                byte dataByte = currentReadByte;

                if (bitCounter == 0)
                {
                    dataByte = 0xFF; // Default (pulled high)
                }

                // Check if we have data ready from the device
                if (currentDevice != null)
                {
                    // The device should have provided data during address ACK phase
                    // which we stored in rxFifo. For the I2C protocol visualization,
                    // we output the bits from the first byte in rxFifo if available.
                    if (rxFifo.Count > 0 && bitCounter == 0)
                    {
                        // Get the first byte to output
                        dataByte = rxFifo.Peek();
                    }
                }

                currentReadByte = dataByte;

                // Output the bit to SDA (MSB first)
                bool bit = ((dataByte >> (7 - bitCounter)) & 1) == 1;
                SetSda(bit);

                // Clock pulse
                SetScl(true);
                SetScl(false);

                bitCounter++;
            }
            else
            {
                // Read ACK/NACK from master (master drives SDA during ACK)
                SetSda(true); // Release SDA
                SetScl(true);
                bool nack = ReadSda(); // NACK = 1, ACK = 0
                SetScl(false);

                bitCounter = 0;

                if (nack || pendingStop || currentCommand.Stop)
                {
                    // Master sent NACK or wants to stop
                    currentState = I2CState.GeneratingStop;
                }
                else
                {
                    // Continue reading next byte
                    currentByte = 0;
                }
            }
        }

        private void HandleWaitForAckState()
        {
            // In I2C, the slave drives ACK during the 9th clock cycle
            // ACK = SDA low, NACK = SDA high

            bool ackFromDevice = false;

            if (stateBeforeAck == I2CState.SendingAddress)
            {
                // Address phase - look for device
                currentDevice = FindDevice((byte)currentTargetAddress);

                if (currentDevice != null)
                {
                    // Device found - it will drive ACK (SDA low)
                    ackFromDevice = true;
                    // Drive SDA low to simulate device ACK
                    SetSda(false);
                }
                else
                {
                    // No device - NACK (SDA stays high/pulled up)
                    ackFromDevice = false;
                    SetSda(true); // Release SDA to let it be pulled high
                }
            }
            else
            {
                // Data phase - if we have a device, it ACKs
                if (currentDevice != null)
                {
                    ackFromDevice = true;
                    SetSda(false); // Drive ACK
                }
                else
                {
                    ackFromDevice = false;
                    SetSda(true); // NACK
                }
            }

            // Pulse SCL to latch the ACK bit
            SetScl(true);
            SetScl(false);

            awaitingAck = false;

            if (!ackFromDevice && stateBeforeAck == I2CState.SendingAddress)
            {
                // NACK on address - no device at this address
                rawIntrStat |= (uint)InterruptBits.TX_ABRT;
                txAbrtSource.Value = 0x07; // Slave Address Not Acknowledged
                pendingStop = true;
                currentState = I2CState.GeneratingStop;
            }
            else if (!ackFromDevice)
            {
                // NACK during data transfer
                rawIntrStat |= (uint)InterruptBits.TX_ABRT;
                txAbrtSource.Value = 0x01; // NACK from slave
                pendingStop = true;
                currentState = I2CState.GeneratingStop;
            }
            else
            {
                // ACK received, continue with data
                if (stateBeforeAck == I2CState.SendingAddress && isReadOperation)
                {
                    // For read operation, consume queued read commands and preload the RX FIFO.
                    if (currentDevice != null)
                    {
                        var requestedBytes = 0;
                        var stopAfterRead = false;

                        while (txFifo.Count > 0 && txFifo.Peek().IsRead && requestedBytes < FIFO_DEPTH)
                        {
                            var readCommand = txFifo.Dequeue();
                            requestedBytes++;
                            stopAfterRead |= readCommand.Stop;

                            if (readCommand.Restart)
                            {
                                pendingRestart = true;
                                break;
                            }
                        }

                        var data = currentDevice.Read(requestedBytes == 0 ? 1 : requestedBytes);
                        foreach (byte b in data)
                        {
                            if (rxFifo.Count < FIFO_DEPTH)
                            {
                                rxFifo.Enqueue(b);
                            }
                        }

                        pendingStop |= stopAfterRead;
                    }

                    currentReadByte = 0;
                    currentByte = 0;
                    bitCounter = 0;

                    if (pendingRestart)
                    {
                        pendingRestart = false;
                        rawIntrStat |= (uint)InterruptBits.RESTART_DET;
                    }

                    currentState = pendingStop ? I2CState.GeneratingStop : I2CState.Idle;
                }
                else if (stateBeforeAck == I2CState.SendingAddress && !isReadOperation)
                {
                    currentState = I2CState.DataWrite;
                    bitCounter = 0;
                }
                else if (stateBeforeAck == I2CState.DataWrite)
                {
                    writeBuffer.Add(currentByte);

                    if (pendingRestart)
                    {
                        pendingRestart = false;
                        rawIntrStat |= (uint)InterruptBits.RESTART_DET;
                        currentState = I2CState.Idle;
                    }

                    // Continue writing next byte
                    bitCounter = 0;
                }
            }
        }

        private void HandleStopState()
        {
            // Notify current device that transmission is finished
            if (currentDevice != null)
            {
                if (writeBuffer.Count > 0)
                {
                    currentDevice.Write(writeBuffer.ToArray());
                    writeBuffer.Clear();
                }
                currentDevice.FinishTransmission();
                currentDevice = null;
            }

            // Generate STOP condition: SDA goes from low to high while SCL is high
            SetSda(false);  // SDA low
            SetScl(true);   // SCL high
            SetSda(true);   // SDA goes high (STOP)

            // Set interrupts
            rawIntrStat |= (uint)InterruptBits.STOP_DET;
            rawIntrStat |= (uint)InterruptBits.TX_EMPTY;  // TX FIFO is now empty

            // Clear TX FIFO
            txFifo.Clear();

            // Reset state
            pendingStop = false;
            pendingRestart = false;
            currentState = I2CState.Idle;
            i2cThread.Stop();
        }

        #endregion

        #region Device Management

        private bool TryProcessDataCommandSynchronously(ulong value)
        {
            if (!i2cEnabled.Value || !masterMode.Value)
            {
                return false;
            }

            var command = new I2CDataCommand
            {
                Data = (byte)(value & 0xFF),
                IsRead = (value & 0x100) != 0,
                Stop = (value & 0x200) != 0,
                Restart = (value & 0x400) != 0,
            };

            var targetAddress = (byte)icTar.Value;

            if (command.Restart && currentDevice != null)
            {
                FlushPendingWriteBuffer();
                currentDevice.FinishTransmission();
                currentDevice = null;
            }

            if (currentDevice != null && currentTargetAddress != targetAddress)
            {
                FlushPendingWriteBuffer();
                currentDevice.FinishTransmission();
                currentDevice = null;
            }

            currentTargetAddress = targetAddress;

            var targetDevice = currentDevice ?? FindDevice(targetAddress);

            currentDevice = targetDevice;
            rawIntrStat |= (uint)InterruptBits.ACTIVITY;

            if (command.Restart)
            {
                FlushPendingWriteBuffer();
                rawIntrStat |= (uint)InterruptBits.RESTART_DET;
            }

            if (targetDevice == null)
            {
                writeBuffer.Clear();
                currentDevice = null;
                rawIntrStat |= (uint)InterruptBits.TX_ABRT;
                rawIntrStat |= (uint)InterruptBits.STOP_DET;
                rawIntrStat |= (uint)InterruptBits.TX_EMPTY;
                txAbrtSource.Value = 0x07;
                UpdateInterrupts();
                UpdateDreqSignals();
                return true;
            }

            if (command.IsRead)
            {
                FlushPendingWriteBuffer();

                var data = currentDevice.Read(1);
                foreach (var b in data)
                {
                    if (rxFifo.Count < FIFO_DEPTH)
                    {
                        rxFifo.Enqueue(b);
                    }
                    else
                    {
                        rawIntrStat |= (uint)InterruptBits.RX_OVER;
                        break;
                    }
                }
            }
            else
            {
                writeBuffer.Add(command.Data);
            }

            if (command.Stop)
            {
                FlushPendingWriteBuffer();
                currentDevice.FinishTransmission();
                currentDevice = null;
                rawIntrStat |= (uint)InterruptBits.STOP_DET;
            }

            rawIntrStat |= (uint)InterruptBits.TX_EMPTY;
            UpdateInterrupts();
            UpdateDreqSignals();
            return true;
        }

        private void FlushPendingWriteBuffer()
        {
            if (currentDevice == null || writeBuffer.Count == 0)
            {
                return;
            }

            currentDevice.Write(writeBuffer.ToArray());
            writeBuffer.Clear();
        }

        private II2CPeripheral FindDevice(byte address)
        {
            // SimpleContainer ChildCollection returns KeyValuePair<int, II2CPeripheral>
            // where the key is the I2C address
            foreach (var deviceEntry in ChildCollection)
            {
                if (deviceEntry.Key == address)
                {
                    return deviceEntry.Value;
                }
            }

            return null;
        }

        #endregion

        #region GPIO Helpers

        private void SetSda(bool value)
        {
            sdaDrivenLow = !value;
            foreach (var pin in sdaPins)
            {
                if (value)
                {
                    // Set as input (high-impedance, pulled up externally)
                    gpio.SetPinOutput(pin, false);
                }
                else
                {
                    // Drive low
                    gpio.SetPinOutput(pin, true);
                    gpio.WritePin(pin, false);
                }
            }
        }

        private void SetScl(bool value)
        {
            sclDrivenLow = !value;
            foreach (var pin in sclPins)
            {
                if (value)
                {
                    // Set as input (high-impedance, pulled up externally)
                    gpio.SetPinOutput(pin, false);
                }
                else
                {
                    // Drive low
                    gpio.SetPinOutput(pin, true);
                    gpio.WritePin(pin, false);
                }
            }
        }

        private bool ReadSda()
        {
            if (sdaPins.Count == 0) return true; // Pull-up default

            // If we're driving SDA low, return low
            if (sdaDrivenLow)
            {
                // Check if any external device is also driving
                bool externalDrive = GetExternalSdaDrive();
                // Wired-AND: return low if either is driving low
                return externalDrive;
            }

            // We're not driving, read the actual pin state
            return gpio.GetGpioState((uint)sdaPins[0]);
        }

        private bool ReadScl()
        {
            if (sclPins.Count == 0) return true;
            return gpio.GetGpioState((uint)sclPins[0]);
        }

        private bool GetExternalSdaDrive()
        {
            // Check if any registered device is driving SDA low (ACK)
            // This simulates the device pulling SDA low
            if (currentDevice != null)
            {
                // Device exists and would drive ACK
                return false; // SDA low
            }
            return true; // SDA high (pulled up)
        }

        private void ReleaseBusLines()
        {
            sdaDrivenLow = false;
            sclDrivenLow = false;
            foreach (var pin in sdaPins)
            {
                gpio.SetPinOutput(pin, false);
            }
            foreach (var pin in sclPins)
            {
                gpio.SetPinOutput(pin, false);
            }
        }

        #endregion

        #region Interrupt and DMA Handling

        private void UpdateInterrupts()
        {
            // Calculate masked interrupt status
            uint maskedStatus = rawIntrStat & (uint)~intrMask.Value;
            SetSignal(IRQ, ref irqState, maskedStatus != 0);
        }

        private void UpdateDreqSignals()
        {
            // Transmit DREQ: Trigger when TX FIFO level below threshold
            SetSignal(DmaTransmitRequest, ref dmaTransmitRequestState,
                dmaTxEnable.Value && (ulong)txFifo.Count <= dmaTxThreshold.Value);

            // Receive DREQ: Trigger when RX FIFO level above threshold
            SetSignal(DmaReceiveRequest, ref dmaReceiveRequestState,
                dmaRxEnable.Value && (ulong)rxFifo.Count > dmaRxThreshold.Value);
        }

        private void SetSignal(GPIO signal, ref bool currentState, bool newState)
        {
            if (currentState == newState)
            {
                return;
            }

            currentState = newState;
            if (newState)
            {
                signal.Set();
            }
            else
            {
                signal.Unset();
            }
        }

        private void UpdateStatus()
        {
            // Status is updated on register read
        }

        #endregion

        #region Register Definitions

        private void DefineRegisters()
        {
            // IC_CON - Control Register
            Registers.IC_CON.Define(registers)
                .WithFlag(0, out masterMode, name: "MASTER_MODE")
                .WithValueField(1, 2, out speed, name: "SPEED",
                    writeCallback: (_, val) => UpdateClockFrequency())
                .WithFlag(3, out address10BitsSlave, name: "IC_10BITADDR_SLAVE")
                .WithFlag(4, out address10BitsMaster, name: "IC_10BITADDR_MASTER")
                .WithFlag(5, out restartEnabled, name: "IC_RESTART_EN")
                .WithFlag(6, out slaveDisable, name: "IC_SLAVE_DISABLE")
                .WithFlag(7, out stopDetectionIfAddressed, name: "STOP_DET_IFADDRESSED")
                .WithFlag(8, out txEmptyControl, name: "TX_EMPTY_CTRL")
                .WithFlag(9, out rxFifoFullHoldControl, name: "RX_FIFO_FULL_HLD_CTRL")
                .WithFlag(10, out stopDetectionIfMasterActive, FieldMode.Read, name: "STOP_DET_IF_MASTER_ACTIVE")
                .WithReservedBits(11, 21);

            // IC_TAR - Target Address Register
            Registers.IC_TAR.Define(registers)
                .WithValueField(0, 10, out icTar, name: "IC_TAR")
                .WithFlag(10, out gcOrStart, name: "GC_OR_START")
                .WithFlag(11, out special, name: "SPECIAL")
                .WithReservedBits(12, 20);

            // IC_SAR - Slave Address Register
            Registers.IC_SAR.Define(registers)
                .WithValueField(0, 10, out icSar, name: "IC_SAR")
                .WithReservedBits(10, 22);

            // IC_DATA_CMD - Data Buffer and Command Register
            Registers.IC_DATA_CMD.Define(registers)
                .WithValueField(0, 8, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        // Read from RX FIFO
                        if (rxFifo.Count > 0)
                        {
                            byte data = rxFifo.Dequeue();
                            return (ulong)data;
                        }
                        // RX underflow
                        return 0;
                    },
                    name: "DAT")
                .WithFlag(8, out cmd, name: "CMD")
                .WithFlag(9, out stop, name: "STOP")
                .WithFlag(10, out restart, name: "RESTART")
                .WithFlag(11, out firstDataByte, FieldMode.Read, name: "FIRST_DATA_BYTE")
                .WithReservedBits(12, 20)
                .WithWriteCallback((_, val) =>
                {
                    if (TryProcessDataCommandSynchronously(val))
                    {
                        return;
                    }

                    this.Log(LogLevel.Noisy, "Ignoring IC_DATA_CMD write while I2C{0} is disabled or not in master mode", id);
                });

            // IC_SS_SCL_HCNT - Standard Speed SCL High Count
            Registers.IC_SS_SCL_HCNT.Define(registers)
                .WithValueField(0, 16, out icSsSclHcnt, name: "IC_SS_SCL_HCNT",
                    writeCallback: (_, val) => UpdateClockFrequency())
                .WithReservedBits(16, 16);

            // IC_SS_SCL_LCNT - Standard Speed SCL Low Count
            Registers.IC_SS_SCL_LCNT.Define(registers)
                    .WithValueField(0, 16, out icSsSclLcnt, name: "IC_SS_SCL_LCNT",
                        writeCallback: (_, val) => UpdateClockFrequency())
                    .WithReservedBits(16, 16);

            // IC_FS_SCL_HCNT - Fast Speed SCL High Count
            Registers.IC_FS_SCL_HCNT.Define(registers)
                    .WithValueField(0, 16, out icFsSclHcnt, name: "IC_FS_SCL_HCNT",
                        writeCallback: (_, val) => UpdateClockFrequency())
                    .WithReservedBits(16, 16);

            // IC_FS_SCL_LCNT - Fast Speed SCL Low Count
            Registers.IC_FS_SCL_LCNT.Define(registers)
                    .WithValueField(0, 16, out icFsSclLcnt, name: "IC_FS_SCL_LCNT",
                        writeCallback: (_, val) => UpdateClockFrequency())
                    .WithReservedBits(16, 16);

            // IC_INTR_STAT - Interrupt Status Register (read-only, masked)
            Registers.IC_INTR_STAT.Define(registers)
                    .WithValueField(0, 14, FieldMode.Read,
                        valueProviderCallback: _ => rawIntrStat & ~intrMask.Value,
                        name: "INTR_STAT")
                    .WithReservedBits(14, 18);

            // IC_INTR_MASK - Interrupt Mask Register
            Registers.IC_INTR_MASK.Define(registers)
                    .WithValueField(0, 14, out intrMask, name: "INTR_MASK")
                    .WithReservedBits(14, 18);

            // IC_RAW_INTR_STAT - Raw Interrupt Status Register
            Registers.IC_RAW_INTR_STAT.Define(registers)
                    .WithValueField(0, 14, FieldMode.Read,
                        valueProviderCallback: _ => rawIntrStat,
                        name: "RAW_INTR_STAT")
                    .WithReservedBits(14, 18);

            // IC_RX_TL - Receive FIFO Threshold
            Registers.IC_RX_TL.Define(registers)
                    .WithValueField(0, 8, out rxFifoThreshold, name: "RX_TL")
                    .WithReservedBits(8, 24);

            // IC_TX_TL - Transmit FIFO Threshold
            Registers.IC_TX_TL.Define(registers)
                    .WithValueField(0, 8, out txFifoThreshold, name: "TX_TL")
                    .WithReservedBits(8, 24);

            // IC_CLR_INTR - Clear Combined and Individual Interrupts
            Registers.IC_CLR_INTR.Define(registers)
                    .WithValueField(0, 14, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            // Reading clears all interrupts
                            rawIntrStat = 0;
                            UpdateInterrupts();
                            return 0;
                        },
                        name: "CLR_INTR")
                    .WithReservedBits(14, 18);

            // IC_CLR_RX_UNDER - Clear RX_UNDER Interrupt
            Registers.IC_CLR_RX_UNDER.Define(registers)
                    .WithValueField(0, 1, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            rawIntrStat &= ~(uint)InterruptBits.RX_UNDER;
                            UpdateInterrupts();
                            return 0;
                        },
                        name: "CLR_RX_UNDER")
                    .WithReservedBits(1, 31);

            // IC_CLR_RX_OVER - Clear RX_OVER Interrupt
            Registers.IC_CLR_RX_OVER.Define(registers)
                    .WithValueField(0, 1, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            rawIntrStat &= ~(uint)InterruptBits.RX_OVER;
                            UpdateInterrupts();
                            return 0;
                        },
                        name: "CLR_RX_OVER")
                    .WithReservedBits(1, 31);

            // IC_CLR_TX_OVER - Clear TX_OVER Interrupt
            Registers.IC_CLR_TX_OVER.Define(registers)
                    .WithValueField(0, 1, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            rawIntrStat &= ~(uint)InterruptBits.TX_OVER;
                            UpdateInterrupts();
                            return 0;
                        },
                        name: "CLR_TX_OVER")
                    .WithReservedBits(1, 31);

            // IC_CLR_RD_REQ - Clear RD_REQ Interrupt
            Registers.IC_CLR_RD_REQ.Define(registers)
                    .WithValueField(0, 1, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            rawIntrStat &= ~(uint)InterruptBits.RD_REQ;
                            UpdateInterrupts();
                            return 0;
                        },
                        name: "CLR_RD_REQ")
                    .WithReservedBits(1, 31);

            // IC_CLR_TX_ABRT - Clear TX_ABRT Interrupt
            Registers.IC_CLR_TX_ABRT.Define(registers)
                    .WithValueField(0, 1, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            rawIntrStat &= ~(uint)InterruptBits.TX_ABRT;
                            UpdateInterrupts();
                            return 0;
                        },
                        name: "CLR_TX_ABRT")
                    .WithReservedBits(1, 31);

            // IC_CLR_RX_DONE - Clear RX_DONE Interrupt
            Registers.IC_CLR_RX_DONE.Define(registers)
                    .WithValueField(0, 1, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            rawIntrStat &= ~(uint)InterruptBits.RX_DONE;
                            UpdateInterrupts();
                            return 0;
                        },
                        name: "CLR_RX_DONE")
                    .WithReservedBits(1, 31);

            // IC_CLR_ACTIVITY - Clear ACTIVITY Interrupt
            Registers.IC_CLR_ACTIVITY.Define(registers)
                    .WithValueField(0, 1, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            rawIntrStat &= ~(uint)InterruptBits.ACTIVITY;
                            UpdateInterrupts();
                            return 0;
                        },
                        name: "CLR_ACTIVITY")
                    .WithReservedBits(1, 31);

            // IC_CLR_STOP_DET - Clear STOP_DET Interrupt
            Registers.IC_CLR_STOP_DET.Define(registers)
                    .WithValueField(0, 1, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            rawIntrStat &= ~(uint)InterruptBits.STOP_DET;
                            UpdateInterrupts();
                            return 0;
                        },
                        name: "CLR_STOP_DET")
                    .WithReservedBits(1, 31);

            // IC_CLR_START_DET - Clear START_DET Interrupt
            Registers.IC_CLR_START_DET.Define(registers)
                    .WithValueField(0, 1, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            rawIntrStat &= ~(uint)InterruptBits.START_DET;
                            UpdateInterrupts();
                            return 0;
                        },
                        name: "CLR_START_DET")
                    .WithReservedBits(1, 31);

            // IC_CLR_GEN_CALL - Clear GEN_CALL Interrupt
            Registers.IC_CLR_GEN_CALL.Define(registers)
                    .WithValueField(0, 1, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            rawIntrStat &= ~(uint)InterruptBits.GEN_CALL;
                            UpdateInterrupts();
                            return 0;
                        },
                        name: "CLR_GEN_CALL")
                    .WithReservedBits(1, 31);

            // IC_ENABLE - Enable Register
            Registers.IC_ENABLE.Define(registers)
                    .WithFlag(0, out i2cEnabled, name: "ENABLE",
                        writeCallback: (_, val) =>
                        {
                            if (val)
                            {
                                UpdateClockFrequency();
                            }
                            else
                            {
                                i2cThread.Stop();
                                currentState = I2CState.Idle;
                            }
                        })
                    .WithFlag(1, out abort, name: "ABORT",
                        writeCallback: (_, val) =>
                        {
                            if (val)
                            {
                                // Abort current transfer
                                currentState = I2CState.Idle;
                                txFifo.Clear();
                                rawIntrStat |= (uint)InterruptBits.TX_ABRT;
                                UpdateInterrupts();
                            }
                        })
                    .WithReservedBits(2, 30);

            // IC_STATUS - Status Register
            Registers.IC_STATUS.Define(registers)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => currentState != I2CState.Idle, name: "ACTIVITY")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => txFifo.Count < FIFO_DEPTH, name: "TFNF")
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => txFifo.Count == 0, name: "TFE")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => rxFifo.Count > 0, name: "RFNE")
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => rxFifo.Count >= FIFO_DEPTH, name: "RFF")
                    .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => currentState != I2CState.Idle && masterMode.Value, name: "MST_ACTIVITY")
                    .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => false, name: "SLV_ACTIVITY") // Slave mode not implemented
                    .WithReservedBits(7, 25);

            // IC_TXFLR - Transmit FIFO Level
            Registers.IC_TXFLR.Define(registers)
                    .WithValueField(0, 5, FieldMode.Read, valueProviderCallback: _ => (ulong)txFifo.Count, name: "TXFLR")
                    .WithReservedBits(5, 27);

            // IC_RXFLR - Receive FIFO Level
            Registers.IC_RXFLR.Define(registers)
                    .WithValueField(0, 5, FieldMode.Read, valueProviderCallback: _ => (ulong)rxFifo.Count, name: "RXFLR")
                    .WithReservedBits(5, 27);

            // IC_SDA_HOLD - SDA Hold Time
            Registers.IC_SDA_HOLD.Define(registers)
                    .WithValueField(0, 16, out sdaHoldTx, name: "SDA_HOLD_TX")
                    .WithValueField(16, 8, out sdaHoldRx, name: "SDA_HOLD_RX")
                    .WithReservedBits(24, 8);

            // IC_TX_ABRT_SOURCE - Transmit Abort Source
            Registers.IC_TX_ABRT_SOURCE.Define(registers)
                    .WithValueField(0, 23, out txAbrtSource, FieldMode.Read, name: "TX_ABRT_SOURCE")
                    .WithReservedBits(23, 9);

            // IC_FS_SPKLEN - Fast Mode Plus Spike Length
            Registers.IC_FS_SPKLEN.Define(registers)
                    .WithTag("IC_FS_SPKLEN", 0, 8)
                    .WithReservedBits(8, 24);

            // IC_ENABLE_STATUS - Enable Status Register
            Registers.IC_ENABLE_STATUS.Define(registers)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => i2cEnabled.Value, name: "IC_EN")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "SLV_DISABLED_WHILE_BUSY")
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => false, name: "SLV_RX_DATA_LOST")
                    .WithReservedBits(3, 29);

            // IC_DMA_CR - DMA Control
            Registers.IC_DMA_CR.Define(registers)
                    .WithFlag(0, out dmaRxEnable, name: "RDMAE")
                    .WithFlag(1, out dmaTxEnable, name: "TDMAE")
                    .WithReservedBits(2, 30);

            // IC_DMA_TDLR - DMA Transmit Data Level
            Registers.IC_DMA_TDLR.Define(registers)
                    .WithValueField(0, 5, out dmaTxThreshold, name: "DMATDL")
                    .WithReservedBits(5, 27);

            // IC_DMA_RDLR - DMA Receive Data Level
            Registers.IC_DMA_RDLR.Define(registers)
                    .WithValueField(0, 5, out dmaRxThreshold, name: "DMARDL")
                    .WithReservedBits(5, 27);

            // IC_COMP_PARAM_1 - Component Parameter 1
            Registers.IC_COMP_PARAM_1.Define(registers)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => 0x00030206, name: "COMP_PARAM_1") // FIFO depth = 16
                    .WithReservedBits(16, 16);

            // IC_COMP_VERSION - Component Version
            Registers.IC_COMP_VERSION.Define(registers)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 0x00000000, name: "COMP_VERSION")
                    .WithReservedBits(0, 0);

            // IC_COMP_TYPE - Component Type
            Registers.IC_COMP_TYPE.Define(registers)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 0x44570140, name: "COMP_TYPE") // "DW I2C"
                    .WithReservedBits(0, 0);
        }

        private void UpdateClockFrequency()
        {
            if (!i2cEnabled.Value || clocks == null)
            {
                return;
            }

            ulong periFreq = clocks.PeripheralClockFrequency;
            ulong hcnt, lcnt;

            // Select speed mode
            switch (speed.Value)
            {
                case 1: // Standard mode (100kHz)
                    hcnt = icSsSclHcnt.Value;
                    lcnt = icSsSclLcnt.Value;
                    break;
                case 2: // Fast mode (400kHz)
                default:
                    hcnt = icFsSclHcnt.Value;
                    lcnt = icFsSclLcnt.Value;
                    break;
            }

            // Calculate I2C frequency: F = Fperi / (HCNT + LCNT + 2)
            ulong divisor = hcnt + lcnt + 2;
            if (divisor == 0) divisor = 1;

            ulong newFreq = periFreq / divisor;

            // Ensure minimum frequency of 100kHz for reasonable simulation speed
            if (newFreq < 100000) newFreq = 100000;
            if (newFreq > 1000000) newFreq = 1000000; // Cap at 1MHz

            i2cThread.Frequency = (uint)newFreq;
            this.Log(LogLevel.Debug, $"I2C{id}: Clock frequency updated to {newFreq} Hz");
        }

        #endregion

        #region Types and Constants

        private enum Registers : long
        {
            IC_CON = 0x00,
            IC_TAR = 0x04,
            IC_SAR = 0x08,
            IC_DATA_CMD = 0x10,
            IC_SS_SCL_HCNT = 0x14,
            IC_SS_SCL_LCNT = 0x18,
            IC_FS_SCL_HCNT = 0x1c,
            IC_FS_SCL_LCNT = 0x20,
            IC_INTR_STAT = 0x2c,
            IC_INTR_MASK = 0x30,
            IC_RAW_INTR_STAT = 0x34,
            IC_RX_TL = 0x38,
            IC_TX_TL = 0x3c,
            IC_CLR_INTR = 0x40,
            IC_CLR_RX_UNDER = 0x44,
            IC_CLR_RX_OVER = 0x48,
            IC_CLR_TX_OVER = 0x4c,
            IC_CLR_RD_REQ = 0x50,
            IC_CLR_TX_ABRT = 0x54,
            IC_CLR_RX_DONE = 0x58,
            IC_CLR_ACTIVITY = 0x5c,
            IC_CLR_STOP_DET = 0x60,
            IC_CLR_START_DET = 0x64,
            IC_CLR_GEN_CALL = 0x68,
            IC_ENABLE = 0x6c,
            IC_ENABLE_STATUS = 0x9c,
            IC_FS_SPKLEN = 0xa0,
            IC_CLR_RESTART_DET = 0xa8,
            IC_STATUS = 0x70,
            IC_TXFLR = 0x74,
            IC_RXFLR = 0x78,
            IC_SDA_HOLD = 0x7c,
            IC_TX_ABRT_SOURCE = 0x80,
            IC_SLV_DATA_NACK_ONLY = 0x84,
            IC_DMA_CR = 0x88,
            IC_DMA_TDLR = 0x8c,
            IC_DMA_RDLR = 0x90,
            IC_SDA_SETUP = 0x94,
            IC_ACK_GENERAL_ACK = 0x98,
            IC_COMP_PARAM_1 = 0xf4,
            IC_COMP_VERSION = 0xf8,
            IC_COMP_TYPE = 0xfc
        }

        [Flags]
        private enum InterruptBits : uint
        {
            RX_UNDER = 1u << 0,
            RX_OVER = 1u << 1,
            RX_FULL = 1u << 2,
            TX_OVER = 1u << 3,
            TX_EMPTY = 1u << 4,
            RD_REQ = 1u << 5,
            TX_ABRT = 1u << 6,
            RX_DONE = 1u << 7,
            ACTIVITY = 1u << 8,
            STOP_DET = 1u << 9,
            START_DET = 1u << 10,
            GEN_CALL = 1u << 11,
            RESTART_DET = 1u << 12,
            MST_ON_HOLD = 1u << 13
        }

        private enum I2CState
        {
            Idle,
            GeneratingStart,
            SendingAddress,
            DataWrite,
            DataRead,
            WaitForAck,
            GeneratingStop
        }

        private class I2CDataCommand
        {
            public byte Data { get; set; }
            public bool IsRead { get; set; }
            public bool Stop { get; set; }
            public bool Restart { get; set; }
        }

        #endregion

        #region Fields

        // Dependencies
        private readonly RP2040GPIO gpio;
        private readonly int id;
        private readonly RP2040Clocks clocks;

        // GPIO pins
        private readonly List<int> sdaPins;
        private readonly List<int> sclPins;

        // FIFOs
        private readonly Queue<I2CDataCommand> txFifo;
        private readonly Queue<byte> rxFifo;
        private const int FIFO_DEPTH = 16;

        // Thread
        private readonly IManagedThread i2cThread;

        // State machine
        private I2CState currentState;
        private int bitCounter;
        private byte currentByte;
        private byte currentReadByte;
        private bool awaitingAck;
        private bool isReadOperation;
        private ushort currentTargetAddress;
        private I2CDataCommand currentCommand;
        private bool pendingStop;
        private bool pendingRestart;
        private I2CState stateBeforeAck;

        // Device communication
        private II2CPeripheral currentDevice;
        private List<byte> writeBuffer = new List<byte>();

        // Bus line control state
        private bool sdaDrivenLow;
        private bool sclDrivenLow;
        private bool irqState;
        private bool dmaTransmitRequestState;
        private bool dmaReceiveRequestState;

        // Interrupts
        private uint rawIntrStat;

        // Registers
        private readonly DoubleWordRegisterCollection registers;

        // Register fields
        private IFlagRegisterField masterMode;
        private IValueRegisterField speed;
        private IFlagRegisterField address10BitsSlave;
        private IFlagRegisterField address10BitsMaster;
        private IFlagRegisterField restartEnabled;
        private IFlagRegisterField slaveDisable;
        private IFlagRegisterField stopDetectionIfAddressed;
        private IFlagRegisterField txEmptyControl;
        private IFlagRegisterField rxFifoFullHoldControl;
        private IFlagRegisterField stopDetectionIfMasterActive;

        private IValueRegisterField icTar;
        private IFlagRegisterField gcOrStart;
        private IFlagRegisterField special;

        private IValueRegisterField icSar;

        private IValueRegisterField dat;
        private IFlagRegisterField cmd;
        private IFlagRegisterField stop;
        private IFlagRegisterField restart;
        private IFlagRegisterField firstDataByte;

        private IValueRegisterField icSsSclHcnt;
        private IValueRegisterField icSsSclLcnt;
        private IValueRegisterField icFsSclHcnt;
        private IValueRegisterField icFsSclLcnt;

        private IValueRegisterField intrMask;
        private IValueRegisterField rxFifoThreshold;
        private IValueRegisterField txFifoThreshold;

        private IFlagRegisterField i2cEnabled;
        private IFlagRegisterField abort;

        private IValueRegisterField sdaHoldTx;
        private IValueRegisterField sdaHoldRx;

        private IValueRegisterField txAbrtSource;

        private IFlagRegisterField dmaRxEnable;
        private IFlagRegisterField dmaTxEnable;
        private IValueRegisterField dmaTxThreshold;
        private IValueRegisterField dmaRxThreshold;

        #endregion

        #region Public Properties

        public GPIO IRQ { get; }
        public GPIO DmaTransmitRequest { get; }
        public GPIO DmaReceiveRequest { get; }

        #endregion
    }
}
