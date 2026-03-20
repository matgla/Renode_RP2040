"""
LCD 1602 I2C Tester for Robot Framework tests.

Provides tester for LCD 1602 I2C display to capture displayed messages.
"""

import clr

clr.AddReference("Infrastructure")

import json
import types
import sys
import threading
import time

testers = {}


def mc_RegisterLCD1602Tester(name, device):
    """Register an LCD1602 tester for the given device.
    
    Args:
        name: Unique name for this tester instance
        device: Full path to the LCD1602 device (e.g., "sysbus.i2c0.lcd1602")
    """
    global testers
    sys.stderr.write("Registering LCD1602 tester '{}' for device '{}'\n".format(name, device))
    testers[name] = LCD1602Tester(device, name)


def mc_WaitForLCDMessage(name, substring, timeout_sec):
    """Wait for a message containing the substring to appear.
    
    Args:
        name: Name of the registered tester
        substring: Substring to look for in messages
        timeout_sec: Timeout in seconds
    
    Returns:
        True if message was found, False otherwise
    """
    if name not in testers:
        sys.stderr.write("Can't find LCD1602Tester named: " + name + "\n")
        return False
    return bool(testers[name].wait_for_message(substring, timeout_sec))


def mc_IsBacklightOn(name):
    """Check if the LCD backlight is on."""
    if name not in testers:
        sys.stderr.write("Can't find LCD1602Tester named: " + name + "\n")
        return False
    result = testers[name].is_backlight_on()
    return bool(result)


def mc_IsDisplayEnabled(name):
    """Check if the LCD display is enabled."""
    if name not in testers:
        sys.stderr.write("Can't find LCD1602Tester named: " + name + "\n")
        return False
    result = testers[name].is_display_enabled()
    return bool(result)


def mc_GetLCDLine(name, row):
    """Get the content of a specific line."""
    if name not in testers:
        sys.stderr.write("Can't find LCD1602Tester named: " + name + "\n")
        return ""
    result = testers[name].get_line(row)
    return str(result)


def mc_GetLCDMessages(name):
    """Get all captured messages."""
    if name not in testers:
        sys.stderr.write("Can't find LCD1602Tester named: " + name + "\n")
        return []
    return [str(msg) for msg in testers[name].get_messages()]


def mc_GetLastLCDMessage(name):
    """Get the last captured message."""
    if name not in testers:
        sys.stderr.write("Can't find LCD1602Tester named: " + name + "\n")
        return ""
    return str(testers[name].get_last_message())


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
                    sys.stderr.write("Tree is branched, please fix that code\n")
                current = p
                x += 1
        if found:
            return peri.Peripheral
    return None


class LCD1602Tester:
    def __init__(self, device, name):
        emulation = Antmicro.Renode.Core.EmulationManager.Instance.CurrentEmulation
        self.machine = emulation.Machines[0]
        self.device = machine_find_peripheral(self.machine, device)
        self.name = name
        self.message_event = threading.Event()
        self.captured_messages = []
        
        if self.device is not None:
            self.device.MessageChanged += types.MethodType(
                LCD1602Tester.handle_message_changed, self
            )

    def handle_message_changed(self, message):
        """Callback when a message is captured."""
        self.captured_messages.append(str(message))
        self.message_event.set()

    def wait_for_message(self, substring, timeout_sec):
        """Wait for a message containing substring."""
        start_time = time.time()
        substring_lower = substring.lower()
        
        while time.time() - start_time < timeout_sec:
            # Check current display content
            line0 = self.get_line(0)
            line1 = self.get_line(1)
            content = (line0 + line1).lower()
            
            if substring_lower in content:
                return True
            
            # Also check captured messages
            for msg in self.captured_messages:
                if substring_lower in msg.lower():
                    return True
            
            # Wait a bit
            self.message_event.clear()
            self.message_event.wait(0.1)
        
        return False

    def is_backlight_on(self):
        """Check if backlight is on."""
        if self.device is None:
            return False
        return bool(self.device.IsBacklightOn)

    def is_display_enabled(self):
        """Check if display is enabled."""
        if self.device is None:
            return False
        return bool(self.device.IsDisplayEnabled)

    def get_line(self, row):
        """Get display line content."""
        if self.device is None:
            return ""
        return str(self.device.GetLine(row))

    def get_messages(self):
        """Get all captured messages."""
        if self.device is None:
            return []
        return [str(msg) for msg in self.device.GetCapturedMessages()]

    def get_last_message(self):
        """Get the last captured message."""
        if self.device is None:
            return ""
        return str(self.device.GetLastMessage())
