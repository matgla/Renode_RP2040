"""
Robot Framework library stub for I2C capture tester.

The actual functionality is implemented in i2c_capture_tester.py which runs
inside Renode's IronPython environment. This stub satisfies Robot Framework's
library import requirement while the actual execution happens via 'Execute Command'.
"""


class I2CCaptureTester:
    """Stub class for Robot Framework library import."""
    
    ROBOT_LIBRARY_SCOPE = 'TEST'
    
    def __init__(self):
        pass
    
    def register_i2c_capture_tester(self, name, device):
        """Stub keyword - actual implementation runs in Renode."""
        pass
    
    def wait_for_i2c_data(self, name, expected_data_hex, timeout_sec):
        """Stub keyword - actual implementation runs in Renode."""
        pass
    
    def get_i2c_captured_data(self, name):
        """Stub keyword - actual implementation runs in Renode."""
        pass
    
    def clear_i2c_captured_data(self, name):
        """Stub keyword - actual implementation runs in Renode."""
        pass
