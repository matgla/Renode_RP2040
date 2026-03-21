"""
Segment Display Tester for Robot Framework tests.

Provides testers for LED segment displays like HT16K33.
"""

import clr

clr.AddReference("Infrastructure")

import json
import types
import sys
import threading
import time

testers = {}


def mc_RegisterSegmentDisplayTester(name, device):
    """Register a segment display tester for the given HT16K33 device.
    
    Args:
        name: Unique name for this tester instance
        device: Full path to the HT16K33 device (e.g., "sysbus.i2c0.ht16k33")
    """
    global testers
    testers[name] = SegmentDisplayTester(device, name)


def mc_GetDisplayRowData(name, row):
    """Get the display RAM data for a specific row (0-15).
    
    Args:
        name: Name of the registered tester
        row: Row index (0-15)
    
    Returns:
        Integer value for that row (0-255)
    """
    if name not in testers:
        sys.stderr.write("Can't find SegmentDisplayTester named: " + name)
        return 0
    return int(testers[name].get_row_data(row))


def mc_GetAllDisplayData(name):
    """Get all 16 bytes of display RAM.
    
    Args:
        name: Name of the registered tester
    
    Returns:
        List of 16 integer values
    """
    if name not in testers:
        sys.stderr.write("Can't find SegmentDisplayTester named: " + name)
        return []
    return [int(x) for x in testers[name].get_all_data()]


def mc_IsDisplayEnabled(name):
    """Check if the display is enabled.
    
    Args:
        name: Name of the registered tester
    
    Returns:
        True if display is enabled
    """
    if name not in testers:
        sys.stderr.write("Can't find SegmentDisplayTester named: " + name)
        return False
    return bool(testers[name].is_display_enabled())


def mc_GetDimmingLevel(name):
    """Get the current dimming level (0-15).
    
    Args:
        name: Name of the registered tester
    
    Returns:
        Dimming level (0-15)
    """
    if name not in testers:
        sys.stderr.write("Can't find SegmentDisplayTester named: " + name)
        return 0
    return int(testers[name].get_dimming_level())


def mc_WaitForDisplayData(name, expected_data, timeout_sec):
    """Wait for specific display data to be written.
    
    Args:
        name: Name of the registered tester
        expected_data: List of expected integer values for rows 0-15
        timeout_sec: Timeout in seconds
    
    Returns:
        True if data was found, False otherwise
    """
    if name not in testers:
        sys.stderr.write("Can't find SegmentDisplayTester named: " + name)
        return False
    return bool(testers[name].wait_for_data(expected_data, timeout_sec))


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


class SegmentDisplayTester:
    def __init__(self, device, name):
        emulation = Antmicro.Renode.Core.EmulationManager.Instance.CurrentEmulation
        self.machine = emulation.Machines[0]
        self.device = machine_find_peripheral(self.machine, device)
        self.name = name
        self.data_event = threading.Event()
        
        # Subscribe to DataWritten event if available
        if self.device is not None:
            self.device.DataWritten += types.MethodType(
                SegmentDisplayTester.handle_data_written, self
            )

    def handle_data_written(self, data):
        """Callback when data is written to the display."""
        self.data_event.set()

    def get_row_data(self, row):
        """Get display RAM for a specific row."""
        if self.device is None:
            return 0
        return int(self.device.GetRowData(row))

    def get_all_data(self):
        """Get all 16 bytes of display RAM."""
        if self.device is None:
            return []
        return [int(self.device.GetRowData(i)) for i in range(16)]

    def is_display_enabled(self):
        """Check if display is enabled."""
        if self.device is None:
            return False
        return bool(self.device.IsDisplayEnabled)

    def get_dimming_level(self):
        """Get current dimming level."""
        if self.device is None:
            return 0
        return int(self.device.CurrentDimmingLevel)

    def wait_for_data(self, expected_data, timeout_sec):
        """Wait for specific display data pattern."""
        start_time = time.time()
        
        while time.time() - start_time < timeout_sec:
            match = True
            for i, expected_value in enumerate(expected_data):
                actual_value = self.get_row_data(i)
                if actual_value != expected_value:
                    match = False
                    break
            
            if match:
                return True
            
            # Wait a bit for new data
            self.data_event.clear()
            self.data_event.wait(0.1)
        
        return False
