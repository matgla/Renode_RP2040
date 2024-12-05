import React, { useState, useEffect, useRef, forwardRef, useImperativeHandle } from 'react';
import './Breadboard.css';
import breadboardImage from "../assets/breadboard.svg"
import Led from "./Led.js";
import Button from "./Button.js";
import MCU from "./MCU.js";

const ledColors = ["red", "green", "blue", "orange", "pink"];
var i = 0;

function getNextColor() {
    const colorIndex = i++ % ledColors.length;
    return ledColors[colorIndex];
}

const Breadboard = forwardRef(({ gridColumns, gridRows }, ref) => {
    const [leds, setLeds] = useState([]);
    const [buttons, setButtons] = useState([]);

    const itemsMap = useRef({});
    const layoutMap = useRef({});

    const registerRef = (name, element) => {
        itemsMap.current[name] = element;
    }
    const registerLayoutElement = (name, element) => {
        layoutMap.current[name] = element;
    }
    const ws = useRef(null);

    useImperativeHandle(ref, () => ({
        saveLayout() {
            console.log("Save layout");
            var layout = {};
            layout.boards = [];

            var board = {
                mcus: {
                    "mcu": layoutMap.current["mcu"].serialize()
                }
            };
            board.leds = {}
            leds.forEach((led) => {
                board.leds[led.id] = layoutMap.current[led.id].serialize();
            });
            board.buttons = {};
            buttons.forEach((button) => {
                board.buttons[button.id] = layoutMap.current[button.id].serialize();
            });
 
            layout.boards.push(board)
            // create file in browser
            const json = JSON.stringify(layout, null, 2);
            const blob = new Blob([json], { type: "application/json" });
            const href = URL.createObjectURL(blob);

            // create "a" HTLM element with href to file
            const link = document.createElement("a");
            link.href = href;
            link.download = "layout.json";
            document.body.appendChild(link);
            link.click();

            // clean up "a" element & remove ObjectURL
            document.body.removeChild(link);
            URL.revokeObjectURL(href);
        }
    }));

    const changeLedState = (name, state) => {
        const svg = itemsMap.current[name].querySelector("svg");
        if (svg) {
            const circle = svg.querySelector("#On");
            if (circle && !state) {
                if (circle.style != null) {
                    circle.style.display = "none";
                }
            }
            else if (circle) {
                if (circle.style != null) {
                    circle.style.display = "block";
                }
            }
        }
    }

    useEffect(() => {
        if (!ws.current) {
            ws.current = new WebSocket("ws://" + window.location.host + "/ws");
            ws.current.onmessage = (event) => {
                const msg = JSON.parse(event.data);
                if (msg.msg == "register") {
                    if (msg.peripheral_type == "led") {
                        if (!leds.includes(msg.name)) {
                            setLeds((prevLeds) => [...prevLeds, { id: msg.name, status: msg.status }])
                        }
                    }
                    else if (msg.peripheral_type == "button") {
                        if (!buttons.includes(msg.name)) {
                            setButtons((prevLeds) => [...prevLeds, { id: msg.name }])
                        }
                    }
                    return;
                }
                else if (msg.msg == "state_change") {
                    if (msg.peripheral_type == "led") {
                        changeLedState(msg.name, msg.state);
                    }
                    return;
                }

                console.log("Unhandled message from server: ", msg);
            }

            ws.current.onerror = (error) => {
                console.log("Websocket error: ", error);
            }

            ws.current.onclose = () => {
                ws.current.close();
                ws.current = null;
            }

            return () => {
                //     ws.current.close();
                //     ws.current = null;
            }
        }
    }, []);


    return (
        <div className='breadboard'>
            <img src={breadboardImage} alt="Breadboard" className='breadboard-image' />
            <div className="grid">
                <MCU id="mcu" className="gridItem" ref={(el) => registerLayoutElement("mcu", el)} />
                {leds.map((index) => (
                    <div className="grid-item" key={index.id} ref={(el) => registerRef(index.id, el)}> <Led id={index.id} ref={(el) => registerLayoutElement(index.id, el)} /> </div>
                ))}
                {buttons.map((index) => (
                    <div className="grid-item" key={index.id} ref={(el) => registerRef(index.id, el)}> <Button id={index.id} ref={(el) => registerLayoutElement(index.id, el)} /> </div>
                ))}
            </div>
        </div>
    );
});

export default Breadboard;
