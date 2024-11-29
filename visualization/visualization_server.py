# -*- coding: utf-8 -*-

#
# visualization_server.py
#
# Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
#
# Distributed under the terms of the MIT License.
#

import SocketServer 
import SimpleHTTPServer 

import subprocess
import sys
print("Executable: ", sys.executable)

subprocess.check_call([sys.executable, "-m", "pip", "install", "websockets"])

from websockets.server import serve 
import asyncio

from threading import Thread

import json 

class VisualizationServer:
    def __init__(self, port):
        self.port = port 
        self.leds = {}
        self.buttons = {}
        self.server = None

    def serve(self):
        if self.server is not None:
            print("Visualization is already running")
            return

        print("Serving visualization at localhost:" + str(self.port))

        try:
            self.server = SocketServer.TCPServer(("", self.port), SimpleHTTPServer.SimpleHTTPRequestHandler)
            self.server_thread = Thread(target = self.server.serve_forever)
            self.server.allow_reuse_address = True
            
            #self.server_thread.deamon = True
            self.server_thread.start()
        except:
            import traceback
            traceback.print_exc()



    def stop(self):
        print("Stopping visualization from localhost:" + str(self.port))
        if self.server is not None:
            self.server.shutdown() 
            self.server.server_close()

        # if self.websocket is not None:
            # self.websocket.close_server()
    
        self.server_thread.join()
        # self.websocket_thread.join()
        
        print("Visualization is done")
        
        # self.websocket_thread = None
        # self.websocket = None 
        self.server = None
        self.server_thread = None

    def on_led_change(self, led, state):
        self.websocket_send_message({
            "type": "led",
            "target": led,
            "state": state
        })

    def register_led(self, led):
        self.leds[led] = False
        self.websocket_send_message({
            "type": "register",
            "peripheral_type": "led",
            "target": led,
            "state": False
        })

    def register_button(self, name, button):
        self.buttons[name] = button 
        self.websocket_send_message({
            "type": "register",
            "peripheral_type": "button",
            "target": button
        })

    def websocket_new_client(self, client, server):
        print("New client is registered, sending registration messages")
        for key, value in self.leds.items(): 
            self.websocket_send_to_client(client, {
                "type": "register",
                "peripheral_type": "led",
                "target": key,
                "state": value 
            })

        for key, value in self.buttons.items(): 
            self.websocket_send_to_client(client, {
                "type": "register",
                "peripheral_type": "button",
                "target": key
            })

    def websocket_message_received(self, client, server, message):
        message = json.loads(message)
        if (message["type"] == "action"):
            if (message["peripheral_type"] == "button"):
                if message["action"] == "press":
                    self.buttons[message["target"]].Press()
                else:
                    self.buttons[message["target"]].Release()

    def websocket_send_message(self, message):
        if self.websocket is not None and len(self.websocket.clients) > 0:
            for client in self.websocket.clients:
                try:
                    self.websocket.send_message(client, json.dumps(message))
                except Exception as e:
                    pass

    def websocket_send_to_client(self, client, message):
        if self.websocket is not None and client is not None:
            try:
                self.websocket.send_message(client, json.dumps(message))
            except Exception as e:
                pass
