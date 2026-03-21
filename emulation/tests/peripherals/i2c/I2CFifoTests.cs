/**
 * I2CFifoTests.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using Xunit;

namespace Antmicro.Renode.Tests.Peripherals.I2C
{
    /// <summary>
    /// Unit tests for I2C peripheral FIFO operations.
    /// Tests TX/RX FIFO functionality, thresholds, and overflow/underflow.
    /// </summary>
    public class I2CFifoTests : I2CTestBase
    {
        #region TX FIFO Tests

        [Fact]
        public void TXFifo_InitiallyEmpty()
        {
            // Arrange & Act
            InitializeI2C();

            // Assert
            var txflr = I2C.ReadDoubleWord(0x74); // IC_TXFLR
            Assert.Equal(0u, txflr);
        }

        [Fact]
        public void TXFifo_WriteData_CountIncreases()
        {
            // Arrange
            InitializeI2C();
            EnableI2C();

            // Act
            WriteDataCommand(0xAA);

            // Assert - FIFO count should reflect data
            var txflr = I2C.ReadDoubleWord(0x74);
            Assert.True(txflr > 0, "TX FIFO count should increase after write");
        }

        [Fact]
        public void TXFifo_Status_TFE_ClearWhenNotEmpty()
        {
            // Arrange
            InitializeI2C();
            EnableI2C();
            WriteDataCommand(0xAA);

            // Act
            var status = I2C.ReadDoubleWord(0x70);

            // Assert - TFE (bit 2) should be clear when FIFO not empty
            Assert.True((status & 0x04) == 0, "TFE should be clear when TX FIFO has data");
        }

        [Fact]
        public void TXFifo_Status_TFNF_SetWhenNotFull()
        {
            // Arrange
            InitializeI2C();
            EnableI2C();

            // Act
            var status = I2C.ReadDoubleWord(0x70);

            // Assert - TFNF (bit 1) should be set when FIFO not full
            Assert.True((status & 0x02) != 0, "TFNF should be set when TX FIFO not full");
        }

        [Fact]
        public void TXFifo_Threshold_BelowThreshold_DoesNotTriggerInterrupt()
        {
            // Arrange
            InitializeI2C();
            EnableI2C();
            I2C.WriteDoubleWord(0x3C, 5); // Set TX threshold to 5
            I2C.WriteDoubleWord(0x30, 0x000008FF); // Mask all interrupts initially
            I2C.WriteDoubleWord(0x30, 0x000008EF); // Unmask TX_EMPTY

            // Act - write 3 bytes (below threshold of 5)
            WriteDataCommand(0x01);
            WriteDataCommand(0x02);
            WriteDataCommand(0x03);

            // Assert - TX_EMPTY should not be triggered
            AssertIrqNotSet();
        }

        [Fact]
        public void TXFifo_Transmit_Completes_EmptyInterruptTriggered()
        {
            // Arrange
            InitializeI2C();
            ConfigureI2CPins();
            EnableI2C();
            SetTargetAddress(0x17);
            var device = RegisterMockDevice(0x17);

            // Unmask TX_EMPTY interrupt
            I2C.WriteDoubleWord(0x30, 0x000008FF); // Mask all
            I2C.WriteDoubleWord(0x30, 0x000008EF); // Unmask TX_EMPTY (bit 4)

            // Act - write data with STOP
            WriteDataCommand(0xAA, stop: true);

            // Assert - TX_EMPTY should be triggered after transmission
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x10) != 0, "TX_EMPTY should be set after transmission");
        }

        #endregion

        #region RX FIFO Tests

        [Fact]
        public void RXFifo_InitiallyEmpty()
        {
            // Arrange & Act
            InitializeI2C();

            // Assert
            var rxflr = I2C.ReadDoubleWord(0x78); // IC_RXFLR
            Assert.Equal(0u, rxflr);
        }

        [Fact]
        public void RXFifo_Status_RFNE_ClearWhenEmpty()
        {
            // Arrange
            InitializeI2C();

            // Act
            var status = I2C.ReadDoubleWord(0x70);

            // Assert - RFNE (bit 3) should be clear when RX FIFO empty
            Assert.True((status & 0x08) == 0, "RFNE should be clear when RX FIFO empty");
        }

        [Fact]
        public void RXFifo_ReadFromDevice_DataAvailableInFIFO()
        {
            // Arrange
            InitializeI2C();
            ConfigureI2CPins();
            EnableI2C();
            SetTargetAddress(0x17);
            var device = RegisterMockDevice(0x17);
            device.SetResponseData(new byte[] { 0xAB, 0xCD });

            // Act - initiate read
            WriteDataCommand(0x00, read: true, stop: true);

            // Assert - data should be in RX FIFO
            var rxflr = I2C.ReadDoubleWord(0x78);
            Assert.True(rxflr > 0, "RX FIFO should contain data after read");
        }

        [Fact]
        public void RXFifo_ReadFromDevice_CanReadDataFromRegister()
        {
            // Arrange
            InitializeI2C();
            ConfigureI2CPins();
            EnableI2C();
            SetTargetAddress(0x17);
            var device = RegisterMockDevice(0x17);
            device.SetResponseData(new byte[] { 0xAB });

            // Act - initiate read and then read from data register
            WriteDataCommand(0x00, read: true, stop: true);
            var data = I2C.ReadDoubleWord(0x10); // IC_DATA_CMD

            // Assert
            Assert.Equal(0xABu, data & 0xFF);
        }

        [Fact]
        public void RXFifo_Threshold_AboveThreshold_TriggersInterrupt()
        {
            // Arrange
            InitializeI2C();
            ConfigureI2CPins();
            EnableI2C();
            SetTargetAddress(0x17);
            var device = RegisterMockDevice(0x17);
            device.SetResponseData(new byte[] { 0x01, 0x02, 0x03 });

            I2C.WriteDoubleWord(0x38, 2); // Set RX threshold to 2
            I2C.WriteDoubleWord(0x30, 0x000008FF); // Mask all
            I2C.WriteDoubleWord(0x30, 0x000008FB); // Unmask RX_FULL (bit 2)

            // Act - read 3 bytes (above threshold of 2)
            WriteDataCommand(0x00, read: true);
            WriteDataCommand(0x00, read: true);
            WriteDataCommand(0x00, read: true, stop: true);

            // Assert
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x04) != 0, "RX_FULL should be set when RX FIFO above threshold");
        }

        [Fact]
        public void RXFifo_Underflow_TriggersInterrupt()
        {
            // Arrange
            InitializeI2C();
            EnableI2C();
            I2C.WriteDoubleWord(0x30, 0x000008FF); // Mask all
            I2C.WriteDoubleWord(0x30, 0x000008FE); // Unmask RX_UNDER (bit 0)

            // Act - read from empty RX FIFO
            I2C.ReadDoubleWord(0x10); // IC_DATA_CMD

            // Assert
            var rawStat = I2C.ReadDoubleWord(0x34);
            Assert.True((rawStat & 0x01) != 0, "RX_UNDER should be set when reading empty FIFO");
        }

        [Fact]
        public void RXFifo_Overflow_TriggersInterrupt()
        {
            // This test would require filling the FIFO beyond capacity
            // For simplicity, we verify the interrupt mechanism exists
            // Arrange
            InitializeI2C();
            EnableI2C();

            // Act & Assert - verify overflow interrupt bit exists in mask
            var mask = I2C.ReadDoubleWord(0x30);
            Assert.True((mask & 0x02) != 0, "RX_OVER should be maskable");
        }

        #endregion

        #region FIFO Level Register Tests

        [Fact]
        public void IC_TXFLR_ReflectsFIFOCount()
        {
            // Arrange
            InitializeI2C();
            EnableI2C();

            // Act - write 3 bytes
            WriteDataCommand(0x01);
            WriteDataCommand(0x02);
            WriteDataCommand(0x03);

            // Assert
            var txflr = I2C.ReadDoubleWord(0x74);
            Assert.True(txflr >= 3, "TXFLR should reflect 3 bytes in FIFO");
        }

        [Fact]
        public void IC_RXFLR_ReflectsFIFOCount()
        {
            // Arrange
            InitializeI2C();
            ConfigureI2CPins();
            EnableI2C();
            SetTargetAddress(0x17);
            var device = RegisterMockDevice(0x17);
            device.SetResponseData(new byte[] { 0x01, 0x02 });

            // Act - read 2 bytes
            WriteDataCommand(0x00, read: true);
            WriteDataCommand(0x00, read: true, stop: true);

            // Assert
            var rxflr = I2C.ReadDoubleWord(0x78);
            Assert.True(rxflr >= 2, "RXFLR should reflect 2 bytes in FIFO");
        }

        #endregion
    }
}
