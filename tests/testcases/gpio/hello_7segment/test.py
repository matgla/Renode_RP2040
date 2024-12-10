import clr

clr.AddReference("Renode-peripherals")
clr.AddReference("IronPython.StdLib")

import json
import types

testers = {}


def mc_RegisterDisplayTester(name, display, timeout):
    global testers
    testers[name] = SegmentDisplayTester(display, name)


def mc_WaitForSequence(name, file, timeout):
    if name not in testers:
        print("Can't find SegmentDisplayTester named:" + name)
    testers[name].wait_for_sequence(file, timeout)


def machine_find_peripheral(machine, name):
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
                    print("Tree is branched, please fix that code")
                current = p
                x += 1
        if found:
            return peri.Peripheral
    return None


class SegmentDisplayTester:
    def __init__(self, display, timeout):
        emulation = Antmicro.Renode.Core.EmulationManager.Instance.CurrentEmulation
        machine = emulation.Machines[0]
        self.display = machine_find_peripheral(machine, display)
        self.defaultTimeout = timeout

    def wait_for_sequence(self, file, timeout=None):
        print(file, timeout)
        self.display.StateChanged += types.MethodType(
            SegmentDisplayTester.handle_display_event, self
        )

    def handle_display_event(self, display, cells, segments):
        print("Handle that ")
        print(cells)
        print(segments)
