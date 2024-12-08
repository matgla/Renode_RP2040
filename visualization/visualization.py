# -*- coding: utf-8 -*-

#
# visualization_glue.py
#
# Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
#
# Distributed under the terms of the MIT License.
#
#

import clr

clr.AddReference("Renode-peripherals")
clr.AddReference("IronPython.StdLib")

import os

script_dir = os.path.dirname(os.path.abspath(__file__))

import sys

sys.path.append(script_dir)
os.chdir(script_dir)

from Antmicro.Renode.Peripherals.Miscellaneous import LED
from Antmicro.Renode.Peripherals.Miscellaneous import Button

import Antmicro.Renode.Peripherals.Miscellaneous

from Antmicro.Renode.Core import MachineStateChangedEventArgs

from threading import Thread
from multiprocessing import Process, Pipe
import subprocess
import json


close = False
receiver = None
machine = None
buttons = {}
process = None
layout = None


def convert_to_array(o):
    arr = []
    for e in o:
        arr.append(e)
    return arr


def led_state_change(led, state):
    sendMessage({
        "msg": "state_change",
        "peripheral_type": "led",
        "name": machine.GetLocalName(led),
        "state": state,
    })


def segment_display_state_changed(display, cells, segments):
    sendMessage({
        "msg": "state_change",
        "peripheral_type": "segment_display",
        "name": machine.GetLocalName(display),
        "cells": convert_to_array(cells),
        "segments": convert_to_array(segments),
    })


def process_message(msg):
    if msg["type"] == "action":
        if msg["target"] == "button":
            if msg["action"] == "press":
                buttons[msg["name"]].Press()
            else:
                buttons[msg["name"]].Release()


def mc_setVisualizationPath(path):
    print("Visualization will be served from: " + path)
    os.chdir(path)


def mc_stopVisualization():
    global process
    global close
    global receiver
    print("Closing visualization")
    if process is not None:
        sendMessage({"msg": "exit"})
        process.wait()

    close = True
    print("Closing receiver thread")
    if receiver is not None:
        receiver.join()

    print("Visualization was closed")
    process = None
    receiver = None


def machine_state_changed(machine, state):
    if state.CurrentState == MachineStateChangedEventArgs.State.Disposed:
        print("Dispose visualization")
        mc_stopVisualization()


def machine_find_peripheral_type(machine, name):
    for peri in machine.GetRegisteredPeripherals():
        if name in str(peri.Type):
            return peri.Type
    return None


def mc_startVisualization(port):
    global process
    global machine
    global receiver
    global close

    if process is not None:
        print(
            "Visualization already started, use stopVisualization before starting next one"
        )
        return
    command = ["python3", script_dir + "/visualization_server.py", "--port", str(port)]
    process = subprocess.Popen(
        command, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE
    )

    print("Spawned process with PID: " + str(process.pid))

    emulation = Antmicro.Renode.Core.EmulationManager.Instance.CurrentEmulation
    machine = emulation.Machines[0]
    machine.StateChanged += machine_state_changed
    leds = machine.GetPeripheralsOfType[LED]()
    for led in leds:
        led.StateChanged += led_state_change
        sendMessage({
            "msg": "register",
            "peripheral_type": "led",
            "name": machine.GetLocalName(led),
            "state": led.State,
        })
    machine_buttons = machine.GetPeripheralsOfType[Button]()
    global buttons
    for button in machine_buttons:
        sendMessage({
            "msg": "register",
            "peripheral_type": "button",
            "name": machine.GetLocalName(button),
        })
        buttons[machine.GetLocalName(button)] = button

    SegmentDisplay = machine_find_peripheral_type(
        machine, "Miscellaneous.SegmentDisplay"
    )
    segmentDisplays = machine.GetPeripheralsOfType[SegmentDisplay]()
    for display in segmentDisplays:
        sendMessage({
            "msg": "register",
            "peripheral_type": "segment_display",
            "name": machine.GetLocalName(display),
            "segments": convert_to_array(display.Segments),
            "cells": convert_to_array(display.Cells),
            "colon": display.Colon,
        })
        display.StateChanged += segment_display_state_changed

    if layout is not None:
        sendMessage({"msg": "load_layout", "file": layout})

    receiver = Thread(target=getMessage)
    receiver.deamon = True
    close = False
    receiver.start()


def mc_visualizationLoadLayout(file):
    print("Loading visualization layout from: " + file)
    global layout
    if not os.path.exists(file):
        print("Layout file doesn't exists: " + file)
        return
    with open(file, "r") as f:
        layout = json.load(f)

    sendMessage({"msg": "load_layout", "file": layout})


def mc_visualizationSetBoardElement(name):
    print("Setting board element: " + name)
    sendMessage({"msg": "set_board_element", "name": name})


def sendMessage(message):
    global process
    if process.poll() is None:
        process.stdin.write(json.dumps(message) + "\n")
        process.stdin.flush()


def getMessage():
    global process
    while not close and process.poll() is None:
        data = process.stdout.readline()
        try:
            process_message(json.loads(data.strip()))
        except:
            continue

    print("Process IO has died: ")
    print(process.stdout.read())
    print(process.stderr.read())
