import sys
import psutil
import os
import json
import asyncio
import websockets
import argparse

parser = argparse.ArgumentParser()
parser.add_argument("--port", type=int, help="Server Port", required=True)
args, _ = parser.parse_known_args()

script_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.append(script_dir)
os.chdir(script_dir)

parent_pid = psutil.Process(os.getpid()).ppid()


leds = {"led0": False}
buttons = []
clients = []


async def send_to_clients(msg):
    if clients:
        for ws in clients:
            await ws.send(json.dumps(msg))


async def register_device(msg):
    if msg["peripheral_type"] == "led":
        leds[msg["name"]] = msg["state"]
        await send_to_clients(msg)
    if msg["peripheral_type"] == "button":
        buttons.append(msg["name"])
        await send_to_clients(msg)


async def state_change(msg):
    if msg["peripheral_type"] == "led":
        leds[msg["name"]] = msg["state"]
        await send_to_clients(msg)


async def process_message(message, stop_event):
    print("Processing ", message)
    if message["msg"] == "exit":
        stop_event.set()
        for ws in clients:
            await ws.close()

    if message["msg"] == "register":
        await register_device(message)

    if message["msg"] == "state_change":
        await state_change(message)


async def check_parent(stop_event):
    while not stop_event.is_set() and psutil.pid_exists(parent_pid):
        loop = asyncio.get_event_loop()
        data = await loop.run_in_executor(None, sys.stdin.readline)
        try:
            msg = json.loads(data.strip())
            await process_message(msg, stop_event)
        except:
            if data.strip() == "quit":
                stop_event.set()
                return

    stop_event.set()


async def websocket_handler(websocket):
    clients.append(websocket)
    print("Client connected")
    for led, value in leds.items():
        await websocket.send(
            json.dumps({
                "msg": "register",
                "peripheral_type": "led",
                "name": led,
                "state": value,
            })
        )

    for button in buttons:
        await websocket.send(
            json.dumps({"msg": "register", "peripheral_type": "button", "name": button})
        )

    try:
        async for message in websocket:
            print(message.data)
    except websockets.ConnectionClosed:
        clients.remove(websocket)


async def main():
    stop_event = asyncio.Event()
    parent_exists = asyncio.create_task(check_parent(stop_event))
    server_task = await websockets.serve(websocket_handler, "localhost", 9123)

    async def shutdown():
        await stop_event.wait()
        server_task.close()
        await server_task.wait_closed()

    try:
        await asyncio.gather(parent_exists, shutdown())
    except asyncio.CancelledError:
        pass


asyncio.run(main())
