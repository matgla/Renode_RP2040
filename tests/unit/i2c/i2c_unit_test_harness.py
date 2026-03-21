"""
I2C Unit Test Harness for Renode

This module provides infrastructure for running unit tests on the I2C peripheral
inside the Renode emulation environment using IronPython.
"""

import clr
import sys
import time

clr.AddReference("Infrastructure")
clr.AddReference("Emulator")
clr.AddReference("cores-arm-m")

from Antmicro.Renode.Core import EmulationManager
from Antmicro.Renode.Peripherals.I2C import RP2040I2C, GenericI2CDevice, I2CEEPROM
from Antmicro.Renode.Peripherals.GPIOPort import RP2040GPIO
from Antmicro.Renode.Peripherals.Clocks import RP2040Clocks


class I2CUnitTestHarness:
    """Test harness for I2C peripheral unit testing in Renode."""
    
    def __init__(self):
        self.emulation = EmulationManager.Instance.CurrentEmulation
        self.machine = None
        self.i2c = None
        self.gpio = None
        self.clocks = None
        self.mock_devices = {}
        
    def setup_minimal_machine(self):
        """Create a minimal machine with just the I2C peripheral."""
        # Get or create machine
        if self.emulation.Machines.Count == 0:
            self.machine = self.emulation.CreateMachine("test_machine")
        else:
            self.machine = self.emulation.Machines[0]
            
        # Try to find existing I2C or create minimal setup
        self.i2c = self._find_peripheral("i2c0")
        self.gpio = self._find_peripheral("gpio")
        self.clocks = self._find_peripheral("clocks")
        
        if not all([self.i2c, self.gpio, self.clocks]):
            print("WARNING: Could not find all required peripherals in machine")
            print("Some tests may fail. Ensure machine has I2C, GPIO, and Clocks.")
            
        return self.i2c is not None
        
    def _find_peripheral(self, name):
        """Find a peripheral by partial name match."""
        for peri in self.machine.GetRegisteredPeripherals():
            peri_name = self.machine.GetLocalName(peri)
            if peri_name and name in peri_name.lower():
                return peri
        return None
        
    def register_mock_device(self, address, name=None):
        """Register a mock I2C device at the specified address."""
        if self.i2c is None:
            raise RuntimeError("I2C peripheral not initialized")
            
        device = GenericI2CDevice()
        self.i2c.RegisterChild(device, address)
        
        if name is None:
            name = "device_0x{:02X}".format(address)
        self.mock_devices[name] = device
        
        return device
        
    def register_eeprom_device(self, address, size=256):
        """Register an I2C EEPROM device at the specified address."""
        if self.i2c is None:
            raise RuntimeError("I2C peripheral not initialized")
            
        device = I2CEEPROM(size)
        self.i2c.RegisterChild(device, address)
        self.mock_devices["eeprom_0x{:02X}".format(address)] = device
        
        return device
        
    def write_i2c_register(self, offset, value):
        """Write to an I2C peripheral register."""
        if self.i2c is None:
            raise RuntimeError("I2C peripheral not initialized")
        self.i2c.WriteDoubleWord(offset, value)
        
    def read_i2c_register(self, offset):
        """Read from an I2C peripheral register."""
        if self.i2c is None:
            raise RuntimeError("I2C peripheral not initialized")
        return self.i2c.ReadDoubleWord(offset)
        
    def enable_i2c(self, target_address=0x55):
        """Enable I2C in master mode."""
        # IC_CON: Master mode (0x1), Fast speed (0x2), Slave disable (0x40)
        self.write_i2c_register(0x00, 0x65)
        # IC_TAR: Set target address
        self.write_i2c_register(0x04, target_address)
        # IC_ENABLE: Enable
        self.write_i2c_register(0x6C, 0x01)
        
    def write_data_command(self, data, stop=False, read=False, restart=False):
        """Write to IC_DATA_CMD register."""
        value = data
        if stop:
            value |= 0x200
        if read:
            value |= 0x100
        if restart:
            value |= 0x400
        self.write_i2c_register(0x10, value)
        
    def get_mock_device_data(self, name):
        """Get data received by a mock device."""
        if name not in self.mock_devices:
            raise KeyError("Mock device '{}' not found".format(name))
        device = self.mock_devices[name]
        return list(device.GetReceivedData())
        
    def set_mock_device_response(self, name, data):
        """Set response data for a mock device."""
        if name not in self.mock_devices:
            raise KeyError("Mock device '{}' not found".format(name))
        device = self.mock_devices[name]
        device.SetResponseData(data)
        
    def is_irq_asserted(self):
        """Check if I2C IRQ is asserted."""
        if self.i2c is None:
            return False
        return self.i2c.IRQ.IsSet
        
    def get_raw_interrupt_status(self):
        """Get raw interrupt status."""
        return self.read_i2c_register(0x34)
        
    def clear_all_interrupts(self):
        """Clear all interrupts."""
        self.read_i2c_register(0x40)  # IC_CLR_INTR
        
    def wait_for_interrupt(self, timeout_sec=1.0):
        """Wait for IRQ to be asserted."""
        start = time.time()
        while time.time() - start < timeout_sec:
            if self.is_irq_asserted():
                return True
            time.sleep(0.01)
        return False


class I2CRegisterTest:
    """Tests for I2C register operations."""
    
    def __init__(self, harness):
        self.harness = harness
        self.passed = 0
        self.failed = 0
        
    def run_all(self):
        """Run all register tests."""
        print("=== I2C Register Tests ===")
        
        tests = [
            ("Reset Value - IC_CON", self.test_reset_ic_con),
            ("Reset Value - IC_TAR", self.test_reset_ic_tar),
            ("Reset Value - IC_ENABLE", self.test_reset_ic_enable),
            ("Write/Read - IC_TAR", self.test_write_read_ic_tar),
            ("Write/Read - IC_SAR", self.test_write_read_ic_sar),
        ]
        
        for name, test_func in tests:
            try:
                test_func()
                print("  [PASS] {}".format(name))
                self.passed += 1
            except AssertionError as e:
                print("  [FAIL] {}: {}".format(name, str(e)))
                self.failed += 1
            except Exception as e:
                print("  [ERROR] {}: {}".format(name, str(e)))
                self.failed += 1
                
        print("Register Tests: {} passed, {} failed".format(self.passed, self.failed))
        return self.failed == 0
        
    def test_reset_ic_con(self):
        """Test IC_CON reset value."""
        value = self.harness.read_i2c_register(0x00)
        # Master=1, Speed=2, Restart=1, SlaveDisable=1 -> 0x65
        expected = 0x65
        assert value == expected, "Expected 0x{:X}, got 0x{:X}".format(expected, value)
        
    def test_reset_ic_tar(self):
        """Test IC_TAR reset value."""
        value = self.harness.read_i2c_register(0x04)
        expected = 0x55
        assert value == expected, "Expected 0x{:X}, got 0x{:X}".format(expected, value)
        
    def test_reset_ic_enable(self):
        """Test IC_ENABLE reset value."""
        value = self.harness.read_i2c_register(0x6C)
        expected = 0x00
        assert value == expected, "Expected 0x{:X}, got 0x{:X}".format(expected, value)
        
    def test_write_read_ic_tar(self):
        """Test IC_TAR write and read back."""
        test_value = 0x17
        self.harness.write_i2c_register(0x04, test_value)
        value = self.harness.read_i2c_register(0x04)
        assert value == test_value, "Expected 0x{:X}, got 0x{:X}".format(test_value, value)
        
    def test_write_read_ic_sar(self):
        """Test IC_SAR write and read back."""
        test_value = 0x3F
        self.harness.write_i2c_register(0x08, test_value)
        value = self.harness.read_i2c_register(0x08)
        assert value == test_value, "Expected 0x{:X}, got 0x{:X}".format(test_value, value)


class I2CProtocolTest:
    """Tests for I2C protocol operations."""
    
    def __init__(self, harness):
        self.harness = harness
        self.passed = 0
        self.failed = 0
        
    def run_all(self):
        """Run all protocol tests."""
        print("=== I2C Protocol Tests ===")
        
        tests = [
            ("Write Single Byte", self.test_write_single_byte),
            ("Write Multiple Bytes", self.test_write_multiple_bytes),
            ("Read Single Byte", self.test_read_single_byte),
            ("Read Multiple Bytes", self.test_read_multiple_bytes),
            ("NACK Detection", self.test_nack_detection),
        ]
        
        for name, test_func in tests:
            try:
                test_func()
                print("  [PASS] {}".format(name))
                self.passed += 1
            except AssertionError as e:
                print("  [FAIL] {}: {}".format(name, str(e)))
                self.failed += 1
            except Exception as e:
                print("  [ERROR] {}: {}".format(name, str(e)))
                self.failed += 1
                
        print("Protocol Tests: {} passed, {} failed".format(self.passed, self.failed))
        return self.failed == 0
        
    def test_write_single_byte(self):
        """Test writing a single byte to a device."""
        self.harness.enable_i2c(0x17)
        device = self.harness.register_mock_device(0x17, "test_device")
        
        # Write single byte with STOP
        self.harness.write_data_command(0xAA, stop=True)
        
        # Verify data was received
        received = list(device.GetReceivedData())
        assert len(received) == 1, "Expected 1 byte, got {}".format(len(received))
        assert received[0] == 0xAA, "Expected 0xAA, got 0x{:X}".format(received[0])
        
    def test_write_multiple_bytes(self):
        """Test writing multiple bytes to a device."""
        self.harness.enable_i2c(0x17)
        device = self.harness.register_mock_device(0x17, "test_device")
        
        # Write multiple bytes
        self.harness.write_data_command(0x01)
        self.harness.write_data_command(0x02)
        self.harness.write_data_command(0x03, stop=True)
        
        # Verify data was received
        received = list(device.GetReceivedData())
        assert len(received) == 3, "Expected 3 bytes, got {}".format(len(received))
        assert received == [0x01, 0x02, 0x03], "Unexpected data: {}".format(received)
        
    def test_read_single_byte(self):
        """Test reading a single byte from a device."""
        self.harness.enable_i2c(0x17)
        device = self.harness.register_mock_device(0x17, "test_device")
        device.SetResponseData([0xBE])
        
        # Read with STOP
        self.harness.write_data_command(0x00, read=True, stop=True)
        
        # Read from RX FIFO
        data = self.harness.read_i2c_register(0x10) & 0xFF
        assert data == 0xBE, "Expected 0xBE, got 0x{:X}".format(data)
        
    def test_read_multiple_bytes(self):
        """Test reading multiple bytes from a device."""
        self.harness.enable_i2c(0x17)
        device = self.harness.register_mock_device(0x17, "test_device")
        device.SetResponseData([0x01, 0x02, 0x03])
        
        # Read multiple bytes
        self.harness.write_data_command(0x00, read=True)
        self.harness.write_data_command(0x00, read=True)
        self.harness.write_data_command(0x00, read=True, stop=True)
        
        # Read from RX FIFO
        data = []
        for _ in range(3):
            data.append(self.harness.read_i2c_register(0x10) & 0xFF)
            
        assert data == [0x01, 0x02, 0x03], "Unexpected data: {}".format(data)
        
    def test_nack_detection(self):
        """Test NACK detection from non-existent device."""
        self.harness.enable_i2c(0x7F)  # Non-existent address
        
        # Write with STOP - should trigger TX_ABRT
        self.harness.write_data_command(0xAA, stop=True)
        
        # Check abort was triggered
        raw_stat = self.harness.get_raw_interrupt_status()
        assert (raw_stat & 0x40) != 0, "TX_ABRT should be set"


def run_all_i2c_unit_tests():
    """Run all I2C unit tests."""
    print("\n" + "="*60)
    print("I2C Peripheral Unit Tests")
    print("="*60)
    
    harness = I2CUnitTestHarness()
    
    if not harness.setup_minimal_machine():
        print("ERROR: Could not setup I2C peripheral")
        return False
        
    print("")
    
    # Run register tests
    reg_tests = I2CRegisterTest(harness)
    reg_passed = reg_tests.run_all()
    
    print("")
    
    # Run protocol tests
    proto_tests = I2CProtocolTest(harness)
    proto_passed = proto_tests.run_all()
    
    print("")
    print("="*60)
    total_passed = reg_tests.passed + proto_tests.passed
    total_failed = reg_tests.failed + proto_tests.failed
    print("Total: {} passed, {} failed".format(total_passed, total_failed))
    print("="*60 + "\n")
    
    return total_failed == 0


# Make functions available at module level
__all__ = [
    'I2CUnitTestHarness',
    'I2CRegisterTest', 
    'I2CProtocolTest',
    'run_all_i2c_unit_tests'
]
