"""
I2C Capture Tester for Robot Framework tests.

Captures I2C data written to devices for verification in tests.
"""

import clr

clr.AddReference("Infrastructure")

import sys
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
    return bool(testers[name].wait_for_data(expected_data_hex, timeout_sec))


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


def mc_AssertI2CData(name, expected_data_hex, timeout_sec):
    """Assert that the expected I2C write appears within the timeout."""
    if name not in testers:
        raise Exception("Can't find I2CCaptureTester named: {}".format(name))

    if not testers[name].wait_for_data(expected_data_hex, timeout_sec):
        captured = testers[name].get_captured_data()
        raise Exception(
            "Expected I2C data {} was not captured within {}s. Captured writes: {}".format(
                expected_data_hex, timeout_sec, captured
            )
        )


def mc_AssertAnyCapturedWriteLength(name, expected_length):
    """Assert that any captured I2C write has the given length."""
    if name not in testers:
        raise Exception("Can't find I2CCaptureTester named: {}".format(name))

    captured = testers[name]._get_captured_data()
    if not any(len(write) == int(expected_length) for write in captured):
        raise Exception(
            "Expected a captured write with length {}. Captured writes: {}".format(
                expected_length, testers[name].get_captured_data()
            )
        )


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
        if self.device is None:
            sys.stderr.write("ERROR: Device not found, cannot subscribe to events\n")

    def _get_captured_data(self):
        if self.device is None:
            return []
        return [[int(byte) for byte in write] for write in self.device.GetCapturedWrites()]

    def wait_for_data(self, expected_data_hex, timeout_sec):
        """Wait for specific data pattern."""
        expected = []
        for seq in expected_data_hex:
            if isinstance(seq, list):
                expected.append([int(b, 16) if isinstance(b, str) else int(b) for b in seq])
            else:
                expected.append([int(seq, 16) if isinstance(seq, str) else int(seq)])

        start_time = time.time()
        while time.time() - start_time < timeout_sec:
            captured_data = self._get_captured_data()
            for exp_seq in expected:
                for captured in captured_data:
                    if captured == exp_seq:
                        return True

            time.sleep(0.1)

        return False

    def get_captured_data(self):
        """Return all captured data as list of hex string lists."""
        result = []
        for data in self._get_captured_data():
            result.append(["0x{:02X}".format(int(b)) for b in data])
        return result

    def clear_captured_data(self):
        """Clear all captured data."""
        if self.device is not None:
            self.device.ClearCapturedWrites()
