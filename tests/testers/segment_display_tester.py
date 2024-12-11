import clr

clr.AddReference("Renode-peripherals")
clr.AddReference("IronPython.StdLib")

import json
import types
import sys
import threading

testers = {}


def mc_RegisterDisplayTester(name, display):
    global testers
    testers[name] = SegmentDisplayTester(display, name)


def mc_WaitForSequence(name, file, timeout):
    if name not in testers:
        sys.stderr.write("Can't find SegmentDisplayTester named:" + name)
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
                    sys.stderr.write("Tree is branched, please fix that code")
                current = p
                x += 1
        if found:
            return peri.Peripheral
    return None


class SegmentDisplayTester:
    def __init__(self, display, timeout):
        emulation = Antmicro.Renode.Core.EmulationManager.Instance.CurrentEmulation
        self.machine = emulation.Machines[0]
        self.display = machine_find_peripheral(self.machine, display)
        self.default_timeout = timeout
        self.finished = None
        self.last_event = None
        self.last_expectation = None
        self.first_unmatched = None
        self.success = False
        self.display.StateChanged += types.MethodType(
            SegmentDisplayTester.handle_display_event, self
        )

    def wait_for_sequence(self, file, timeout=None):
        expectations = None
        with open(file, "r") as f:
            expectations = json.loads(f.read())
        self.expectations = expectations
        self.matched_elements = 0
        self.success = False
        if timeout is None:
            timeout = self.default_timeout
        self.finished = threading.Event()
        self.finished.wait(timeout)
        if not self.success:
            sys.stderr.write(
                "SegmentDisplayTester: Expected sequence was not found: "
                + file
                + " "
                + str(self.first_unmatched)
                + " "
                + str(self.success)
            )

        self.finished = None

    @staticmethod
    def convert_to_array(o):
        arr = []
        for e in o:
            arr.append(e)
        return arr

    @staticmethod
    def segments_to_value(segments):
        arr = 0
        for i in range(0, len(segments)):
            arr = arr | int(segments[i]) << i
        return arr

    def _expectation_matched(self):
        if self.matched_elements + 1 >= len(self.expectations["sequence"]):
            self.success = True
            self.finished.set()
        self.matched_elements += 1
        self.last_expectation = None

    def _fail_expectation(self, event_time, segments, cells):
        if (
            self.first_unmatched is None
            or self.first_unmatched["expectation_number"] < self.matched_elements
        ):
            self.first_unmatched = {
                "expectation_number": self.matched_elements,
                "expectation": self.expectations["sequence"][self.matched_elements],
                "segments: ": hex(segments),
                "cells": cells,
                "time": event_time,
            }

        self.matched_elements = 0
        self.last_expectation = None

    def _verify_element(self, cells, segments):
        time_diff = 0
        if self.last_event is not None:
            time_diff = (
                self.machine.ElapsedVirtualTime.TimeElapsed.TotalMilliseconds
                - self.last_event
            )

        if self.last_expectation is not None:
            # delay was scheduled, so check previous event if time matches
            if (
                abs(time_diff - self.last_expectation["time"] * 1000)
                < self.last_expectation["tolerance"] * 1000
            ):
                # expectation matched
                self._expectation_matched()
            else:
                self._fail_expectation(
                    time_diff,
                    int(
                        self.expectations["mapping"][self.last_expectation["value"]], 16
                    ),
                    [],
                )

        if self.success:
            return

        element = self.expectations["sequence"][self.matched_elements]
        # validate cell
        if "cells" in element:
            if cells != element["cells"]:
                self._fail_expectation(
                    time_diff, self.segments_to_value(segments), cells
                )
                return

        # validate sequence
        if int(
            self.expectations["mapping"][element["value"]], 16
        ) != SegmentDisplayTester.segments_to_value(segments):
            self._fail_expectation(time_diff, self.segments_to_value(segments), cells)
            return

        # if time must be check, schedule it for next event
        if "time" in self.expectations["sequence"][self.matched_elements]:
            self.last_event = (
                self.machine.ElapsedVirtualTime.TimeElapsed.TotalMilliseconds
            )
            self.last_expectation = element
        else:
            self._expectation_matched()

    def handle_display_event(self, display, cells, segments):
        if self.success or self.finished is None:
            return

        cells = SegmentDisplayTester.convert_to_array(cells)
        segments = SegmentDisplayTester.convert_to_array(segments)
        if len(cells) == 0:
            cells = [False]
            # this is cell 0 only event
        self._verify_element(cells, segments)
