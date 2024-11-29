import sys
import time 
import psutil 
import os
import json
import asyncio
import websockets
import aiohttp
from aiohttp import web
import argparse 

parser = argparse.ArgumentParser()
parser.add_argument("--port", type=int, help="Server Port", required=True)
args, _ = parser.parse_known_args()

script_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.append(script_dir)
os.chdir(script_dir)

parent_pid = psutil.Process(os.getpid()).ppid()

async def handle_root(request):
    return web.FileResponse("index.html") 

app = web.Application()
app.router.add_get("/", handle_root)
app.router.add_static("/", path=".", name="static", show_index=True)

clients = [] 

def run_http_server():
    runner = web.AppRunner(app)
    return runner

leds = {}
buttons = []

async def send_to_clients(msg):
    if clients:
        for ws in clients:
            await ws.send_str(json.dumps(msg)) 

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
    if message["msg"] == "exit":
        stop_event.set()
        for ws in clients:
            await ws.close()

    if message["msg"] == "register":
        await register_device(message)

    if message["msg"] == "state_change":
        await state_change(message)

async def process_message_from_ws(msg):
    sys.stdout.write(msg + "\n") 
    sys.stdout.flush()

async def websocket_handler(request):
    global ws
    ws = web.WebSocketResponse()
    await ws.prepare(request)

    clients.append(ws)
    for led, value in leds.items():
        await ws.send_str(json.dumps({
            "msg": "register",
            "peripheral_type": "led",
            "name": led,
            "state": value
        }))

    for button in buttons: 
        await ws.send_str(json.dumps({
            "msg": "register",
            "peripheral_type": "button",
            "name": button
        }))

    try:
        async for msg in ws:
            if msg.type == aiohttp.WSMsgType.TEXT:
                await process_message_from_ws(msg.data)
            elif msg.type == aiohttp.WSMsgType.ERROR or msg.type == aiohttp.WSMsgType.CLOSED:
                clents.remove(ws)
    finally:
        clients.remove(ws)
    return ws


app.add_routes([web.get('/ws', websocket_handler)])

async def check_parent(stop_event):
    while not stop_event.is_set() and psutil.pid_exists(parent_pid):
        loop = asyncio.get_event_loop()
        data = await loop.run_in_executor(None, sys.stdin.readline)
        try: 
            msg = json.loads(data.strip())
            await process_message(msg, stop_event)
        except:
            if msg == "quit":
                stop_event.set() 
                for ws in clients:
                    await ws.close() 

    stop_event.set()    


async def start_http_server(runner, stop_event):
    await runner.setup()
    site = web.TCPSite(runner, "localhost", args.port)
    await site.start()
    await stop_event.wait()
    await runner.cleanup()

async def main():
    stop_event = asyncio.Event()
    runner = run_http_server()
    server_task = asyncio.create_task(start_http_server(runner, stop_event))
    parent_exists = asyncio.create_task(check_parent(stop_event))
    try:
        await asyncio.gather(server_task, parent_exists)
    except asyncio.CancelledError:
        pass 

asyncio.run(main())

