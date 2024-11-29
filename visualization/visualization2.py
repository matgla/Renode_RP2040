# -*- coding: utf-8 -*-

#
# visualization.py
#
# Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
#
# Distributed under the terms of the MIT License.
#

import clr
clr.AddReference("Renode-peripherals")
clr.AddReference("IronPython.StdLib")

import os
script_dir = os.path.dirname(os.path.abspath(__file__))

import sys 
sys.path.append(script_dir)
visualizationPath = None
os.chdir(script_dir)

from threading import Thread

from visualization_server import VisualizationServer

from Antmicro.Renode.Peripherals.Miscellaneous import LED
from Antmicro.Renode.Peripherals.Miscellaneous import Button


# This is just glue code to Renode environment

def led_state_change(led, state):
    global visualization 
    visualization.on_led_change(machine.GetLocalName(led), state)


def mc_setVisualizationPath(path):
    print("Visualization will be served from: " + path) 
    global visualizationPath 
    visualizationPath = path
    os.chdir(path)


def mc_startVisualization(port):
    global visualizationPath 
    if visualizationPath is None: 
        print("Set visualizationPath before starting server!")
        return 

    global visualization
    global machine  
    emulation = Antmicro.Renode.Core.EmulationManager.Instance.CurrentEmulation 
    machine = emulation.Machines[0]
    leds = machine.GetPeripheralsOfType[LED]()
    buttons = machine.GetPeripheralsOfType[Button]()
    
    visualization = VisualizationServer(port) 
    visualization.serve()

    for led in leds:
        led.StateChanged += led_state_change
        visualization.register_led(machine.GetLocalName(led))
    
    for button in buttons:
        visualization.register_button(machine.GetLocalName(button), button)

def mc_stopVisualization():
    global visualization

    if visualization is None:
        return 

    visualization.stop()

