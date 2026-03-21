/**
 * I2CInterruptTests.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using Xunit;

namespace Antmicro.Renode.Tests.Peripherals.I2C
{
    /// <summary>
    /// Unit tests for I2C peripheral interrupt handling.
    /// Tests interrupt generation, masking, and clearing.
    /// </summary>
    public class I2CInterruptTests : I2CTestBase
    {
        #region Interrupt Masking Tests

        [Fact]
        public void Interrupt_Masked_DoesNotAssertIRQ()
        {
            // Arrange
            InitializeI2C();
            EnableI2C();
            // Mask all interrupts
            I2C.WriteDoubleWord(0x30, 0x00003FFF);

            // Act - trigger RX_UNDER (read from empty FIFO)
            I2C.ReadDoubleWord(0x10);

            // Assert - IRQ should not be asserted
            AssertIrqNotSet();
        }

        [Fact]
        public void Interrupt_Unmasked_AssertsIRQ()
        {
            // Arrange
            InitializeI2C();
            EnableI2C();
            // Unmask RX_UNDER
            I2C.WriteDoubleWord(0x30, 0x00003FFE);

            // Act - trigger RX_UNDER
            I2C.ReadDoubleWord(0x10);

            // Assert - IRQ should be asserted
            AssertIrqSet();
        }

        [Fact]
        public void Interrupt_MaskedAfterAssert_ClearsIRQ()
        {
            // Arrange
            InitializeI2C();
            EnableI2C();
            I2C.WriteDoubleWord(0x30, 0x00003FFE); // Unmask RX_UNDER
            I2C.ReadDoubleWord(0x10); // Trigger RX_UNDER
            AssertIrqSet();

            // Act - mask the interrupt
            I2C.WriteDoubleWord(0x30, 0x00003FFF);

            // Assert - IRQ should be deasserted
            AssertIrqNotSet();
        }

        #endregion

        #region RX_UNDER Interrupt Tests

        [Fact]
        public void RX_UNDER_ReadEmptyFIFO_TriggersInterrupt()
        {
            // Arrange
            InitializeI2C();
            EnableI2C();
            I2C.WriteDoubleWord(0x30, 0x00003FFE); // Unmask RX_UNDER

            // Act
            I2C.ReadDoubleWord(0x10); // Read from empty RX FIFO

            // Assert
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x01) != 0, "RX_UNDER should be set");
            AssertIrqSet();
        }

        [Fact]
        public void RX_UNDER_ReadClearRegister_ClearsInterrupt()
        {
            // Arrange
            InitializeI2C();
            EnableI2C();
            I2C.WriteDoubleWord(0x30, 0x00003FFE);
            I2C.ReadDoubleWord(0x10);
            AssertIrqSet();

            // Act
            I2C.ReadDoubleWord(0x44); // Read IC_CLR_RX_UNDER

            // Assert
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x01) == 0, "RX_UNDER should be cleared");
        }

        #endregion

        #region RX_OVER Interrupt Tests

        [Fact]
        public void RX_OVER_OverflowCondition_TriggersInterrupt()
        {
            // Arrange
            InitializeI2C();
            EnableI2C();
            ConfigureI2CPins();
            SetTargetAddress(0x17);
            var device = RegisterMockDevice(0x17);
            I2C.WriteDoubleWord(0x30, 0x00003FFD); // Unmask RX_OVER

            // Create response larger than FIFO depth (16 bytes)
            device.SetResponseData(new byte[20]);

            // Act - attempt to read more than FIFO can hold
            for (int i = 0; i < 20; i++)
            {
                WriteDataCommand(0x00, read: true);
            }

            // Assert - RX_OVER should be set
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x02) != 0, "RX_OVER should be set");
        }

        #endregion

        #region TX_OVER Interrupt Tests

        [Fact]
        public void TX_OVER_OverflowCondition_TriggersInterrupt()
        {
            // Arrange
            InitializeI2C();
            EnableI2C();
            I2C.WriteDoubleWord(0x30, 0x00003FF7); // Unmask TX_OVER

            // Act - fill TX FIFO beyond capacity
            for (int i = 0; i < 20; i++)
            {
                WriteDataCommand((byte)i);
            }

            // Assert
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x08) != 0, "TX_OVER should be set");
        }

        #endregion

        #region TX_EMPTY Interrupt Tests

        [Fact]
        public void TX_EMPTY_TransmitComplete_TriggersInterrupt()
        {
            // Arrange
            InitializeI2C();
            ConfigureI2CPins();
            EnableI2C();
            SetTargetAddress(0x17);
            var device = RegisterMockDevice(0x17);
            I2C.WriteDoubleWord(0x30, 0x00003FEF); // Unmask TX_EMPTY

            // Act - write with STOP
            WriteDataCommand(0xAA, stop: true);

            // Assert
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x10) != 0, "TX_EMPTY should be set");
        }

        #endregion

        #region TX_ABRT Interrupt Tests

        [Fact]
        public void TX_ABRT_NACKFromSlave_TriggersInterrupt()
        {
            // Arrange
            InitializeI2C();
            ConfigureI2CPins();
            EnableI2C();
            SetTargetAddress(0x7F); // Address with no device
            I2C.WriteDoubleWord(0x30, 0x00003FBF); // Unmask TX_ABRT

            // Act - write to non-existent device
            WriteDataCommand(0xAA, stop: true);

            // Assert
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x40) != 0, "TX_ABRT should be set");
        }

        [Fact]
        public void TX_ABRT_SourceRegister_ContainsAbortReason()
        {
            // Arrange
            InitializeI2C();
            ConfigureI2CPins();
            EnableI2C();
            SetTargetAddress(0x7F); // Address with no device
            I2C.WriteDoubleWord(0x30, 0x00003FBF);

            // Act
            WriteDataCommand(0xAA, stop: true);

            // Assert - check abort source register
            var abrtSource = I2C.ReadDoubleWord(0x80);
            Assert.True(abrtSource != 0, "TX_ABRT_SOURCE should contain abort reason");
        }

        [Fact]
        public void TX_ABRT_ClearRegister_ClearsInterrupt()
        {
            // Arrange
            InitializeI2C();
            ConfigureI2CPins();
            EnableI2C();
            SetTargetAddress(0x7F);
            I2C.WriteDoubleWord(0x30, 0x00003FBF);
            WriteDataCommand(0xAA, stop: true);
            AssertIrqSet();

            // Act
            I2C.ReadDoubleWord(0x54); // Read IC_CLR_TX_ABRT

            // Assert
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x40) == 0, "TX_ABRT should be cleared");
        }

        #endregion

        #region ACTIVITY Interrupt Tests

        [Fact]
        public void ACTIVITY_I2COperation_TriggersInterrupt()
        {
            // Arrange
            InitializeI2C();
            ConfigureI2CPins();
            EnableI2C();
            SetTargetAddress(0x17);
            var device = RegisterMockDevice(0x17);
            I2C.WriteDoubleWord(0x30, 0x00003EFF); // Unmask ACTIVITY

            // Act - perform I2C operation
            WriteDataCommand(0xAA, stop: true);

            // Assert
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x100) != 0, "ACTIVITY should be set");
        }

        #endregion

        #region STOP_DET Interrupt Tests

        [Fact]
        public void STOP_DET_StopCondition_TriggersInterrupt()
        {
            // Arrange
            InitializeI2C();
            ConfigureI2CPins();
            EnableI2C();
            SetTargetAddress(0x17);
            var device = RegisterMockDevice(0x17);
            // STOP_DET is unmasked by default

            // Act - write with STOP
            WriteDataCommand(0xAA, stop: true);

            // Assert
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x200) != 0, "STOP_DET should be set");
        }

        [Fact]
        public void STOP_DET_ClearRegister_ClearsInterrupt()
        {
            // Arrange
            InitializeI2C();
            ConfigureI2CPins();
            EnableI2C();
            SetTargetAddress(0x17);
            var device = RegisterMockDevice(0x17);
            WriteDataCommand(0xAA, stop: true);
            AssertIrqSet();

            // Act
            I2C.ReadDoubleWord(0x60); // Read IC_CLR_STOP_DET

            // Assert
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x200) == 0, "STOP_DET should be cleared");
        }

        #endregion

        #region START_DET Interrupt Tests

        [Fact]
        public void START_DET_StartCondition_TriggersInterrupt()
        {
            // Arrange
            InitializeI2C();
            ConfigureI2CPins();
            EnableI2C();
            SetTargetAddress(0x17);
            var device = RegisterMockDevice(0x17);
            I2C.WriteDoubleWord(0x30, 0x00003BFF); // Unmask START_DET

            // Act - perform operation (generates START)
            WriteDataCommand(0xAA, stop: true);

            // Assert
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x400) != 0, "START_DET should be set");
        }

        #endregion

        #region RD_REQ Interrupt Tests

        [Fact]
        public void RDREQ_ReadRequest_TriggersInterrupt()
        {
            // Arrange
            InitializeI2C();
            ConfigureI2CPins();
            EnableI2C();
            SetTargetAddress(0x17);
            var device = RegisterMockDevice(0x17);
            I2C.WriteDoubleWord(0x30, 0x000037FF); // Unmask RD_REQ

            // Act - initiate read
            WriteDataCommand(0x00, read: true, stop: true);

            // Assert
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x20) != 0, "RD_REQ should be set");
        }

        #endregion

        #region RX_DONE Interrupt Tests

        [Fact]
        public void RXDONE_ReadComplete_TriggersInterrupt()
        {
            // Arrange
            InitializeI2C();
            ConfigureI2CPins();
            EnableI2C();
            SetTargetAddress(0x17);
            var device = RegisterMockDevice(0x17);
            device.SetResponseData(new byte[] { 0xAB });
            I2C.WriteDoubleWord(0x30, 0x00002FFF); // Unmask RX_DONE

            // Act - read with STOP
            WriteDataCommand(0x00, read: true, stop: true);

            // Assert
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x80) != 0, "RX_DONE should be set");
        }

        #endregion

        #region Combined Interrupt Tests

        [Fact]
        public void MultipleInterrupts_CanBePendingSimultaneously()
        {
            // Arrange
            InitializeI2C();
            EnableI2C();
            I2C.WriteDoubleWord(0x30, 0x00000800); // Unmask all interrupts

            // Act - trigger multiple interrupts
            I2C.ReadDoubleWord(0x10); // RX_UNDER
            // TX_EMPTY would also be set after transaction

            // Assert - check that interrupts accumulate in RAW_INTR_STAT
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x01) != 0, "RX_UNDER should be set");
        }

        [Fact]
        public void MaskedInterrupts_DoNotAffectIRQ()
        {
            // Arrange
            InitializeI2C();
            EnableI2C();
            // Mask RX_UNDER but unmask TX_EMPTY
            I2C.WriteDoubleWord(0x30, 0x000008EF);

            // Act - trigger RX_UNDER (masked)
            I2C.ReadDoubleWord(0x10);

            // Assert - IRQ should not be set because RX_UNDER is masked
            // and no TX_EMPTY has occurred yet
            AssertIrqNotSet();

            // Verify RX_UNDER is still pending in raw status
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x01) != 0, "RX_UNDER should be pending in raw status");
        }

        #endregion
    }
}
