"""
Robot Framework library stub for display tester.

The actual functionality is implemented in segment_display_tester.py which runs
inside Renode's IronPython environment. This stub satisfies Robot Framework's
library import requirement while the actual execution happens via 'Execute Command'.
"""


class DisplayTester:
    """Stub class for Robot Framework library import."""
    
    ROBOT_LIBRARY_SCOPE = 'TEST'
    
    def __init__(self):
        pass
    
    def register_display_tester(self, name, display):
        """Stub keyword - actual implementation runs in Renode."""
        pass
    
    def wait_for_sequence(self, name, file, timeout):
        """Stub keyword - actual implementation runs in Renode."""
        pass
