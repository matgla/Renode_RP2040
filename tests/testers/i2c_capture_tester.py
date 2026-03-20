"""
I2C Capture Tester for Robot Framework tests.

Captures I2C data written to devices for verification in tests.
"""

import clr

clr.AddReference("Infrastructure")

import json
import types
import sys
import threading
import time

testers = {}


def mc_RegisterI2CCaptureTester(name, device):
    """Register an I2C capture tester for the given device.
    
    Args:
        name: Unique name for this tester instance
        device: Full path to the I2C device (e.g., "sysbus.i2c0.ht16k33")
    """
    global testers
    testers[name] = I2CCaptureTester(device, name)


def mc_WaitForI2CData(name, expected_data_hex, timeout_sec):
    """Wait for specific I2C data to be written to the device.
    
    Args:
        name: Name of the registered tester
        expected_data_hex: List of hex strings representing expected byte sequences
        timeout_sec: Timeout in seconds
    
    Returns:
        True if data was found, False otherwise
    """
    if name not in testers:
        sys.stderr.write("Can't find I2CCaptureTester named: " + name)
        return False
    return testers[name].wait_for_data(expected_data_hex, timeout_sec)


def mc_GetI2CCapturedData(name):
    """Get all captured I2C writes.
    
    Args:
        name: Name of the registered tester
    
    Returns:
        List of captured byte arrays as hex strings
    """
    if name not in testers:
        sys.stderr.write("Can't find I2CCaptureTester named: " + name)
        return []
    return testers[name].get_captured_data()


def mc_ClearI2CCapturedData(name):
    """Clear all captured I2C writes.
    
    Args:
        name: Name of the registered tester
    """
    if name not in testers:
        sys.stderr.write("Can't find I2CCaptureTester named: " + name)
        return
    testers[name].clear_captured_data()


def machine_find_peripheral(machine, name):
    """Find a peripheral by its full path name."""
    tree = name.split(".")
    tree.reverse()
    for peri in machine.GetRegisteredPeripherals():
        current = peri.Peripheral
        found = True
        for part in tree:
            if machine.GetLocalName(current) != part:
                found = False
                break
            current = machine.GetParentPeripherals(current)
            x = 0
            for p in current:
                if x != 0:
                    sys.stderr.write("Tree is branched, please fix that code")
                current = p
                x += 1
        if found:
            return peri.Peripheral
    return None


class I2CCaptureTester:
    def __init__(self, device, name):
        emulation = Antmicro.Renode.Core.EmulationManager.Instance.CurrentEmulation
        self.machine = emulation.Machines[0]
        self.device = machine_find_peripheral(self.machine, device)
        self.name = name
        self.captured_data = []
        self.data_event = threading.Event()
        self.finished = None
        
        # Subscribe to DataWritten event
        if self.device is not None:
            self.device.DataWritten += types.MethodType(
                I2CCaptureTester.handle_data_written, self
            )

    def handle_data_written(self, sender, data):
        """Callback when data is written to the I2C device."""
        data_bytes = list(data)
        self.captured_data.append(data_bytes)
        self.data_event.set()

    def wait_for_data(self, expected_data_hex, timeout_sec):
        """Wait for specific data pattern."""
        expected = [[int(b, 16) for b in seq] for seq in expected_data_hex]
        
        start_time = time.time()
        while time.time() - start_time < timeout_sec:
            # Check if we have matching data
            for exp_seq in expected:
                for captured in self.captured_data:
                    if captured == exp_seq:
                        return True
            
            # Wait a bit for new data
            self.data_event.clear()
            self.data_event.wait(0.1)
        
        return False

    def get_captured_data(self):
        """Return all captured data as list of hex string lists."""
        result = []
        for data in self.captured_data:
            result.append(["0x{:02X}".format(b) for b in data])
        return result

    def clear_captured_data(self):
        """Clear all captured data."""
        self.captured_data.clear()
