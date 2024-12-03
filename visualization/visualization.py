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
visualizationPath = None
os.chdir(script_dir)

from Antmicro.Renode.Peripherals.Miscellaneous import LED
from Antmicro.Renode.Peripherals.Miscellaneous import Button
from Antmicro.Renode.Core import MachineStateChangedEventArgs

from threading import Thread
from multiprocessing import Process, Pipe
import subprocess
import json


close = False 
receiver = None
machine = None
buttons = {} 

def led_state_change(led, state):
    sendMessage({
        "msg": "state_change",
        "peripheral_type": "led",
        "name": machine.GetLocalName(led),
        "state": state
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
    visualizationPath = path
    os.chdir(path)

def mc_stopVisualization():
    global process
    global close 
    global receiver
    print("Closing visualization")
    if process is not None: 
        sendMessage({
            "msg": "exit"
        })
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
        mc_stopVisualization()

def mc_startVisualization(port):
    global process 
    global machine
    global receiver
    global close 

    command = ["python3", script_dir + "/visualization_server.py", "--port", str(port)]
    process = subprocess.Popen(command, 
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE)

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
            "state": led.State
        })
    machine_buttons = machine.GetPeripheralsOfType[Button]()
    global buttons
    for button in machine_buttons:
        sendMessage({
            "msg": "register",
            "peripheral_type": "button",
            "name": machine.GetLocalName(button),
            "state": led.State
        })
        buttons[machine.GetLocalName(button)] = button

    receiver = Thread(target = getMessage)
    close = False
    receiver.start()


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
