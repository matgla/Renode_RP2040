import React, { useState, useEffect, useRef } from 'react';
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

const Breadboard = ({ gridColumns, gridRows }) => {
    const [items, setItems] = useState([]);
    const [buttons, setButtons] = useState([]);


    const itemsMap = useRef({})
    const registerRef = (name, element) => {
        console.log("Adding ", name)
        itemsMap.current[name] = element;
    }
    const ws = useRef(null);

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
                        if (!items.includes(msg.name)) {
                            setItems((prevItems) => [...prevItems, { id: msg.name, status: msg.status }])
                        }
                    }
                    else if (msg.peripheral_type == "button") {
                        if (!buttons.includes(msg.name)) {
                            setButtons((prevItems) => [...prevItems, { id: msg.name }])
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
                <MCU />
                {items.map((index) => (
                    <div key={index.id} ref={(el) => registerRef(index.id, el)}> <Led id={index.id} /> </div>
                ))}
                {buttons.map((index) => (
                    <div key={index.id} ref={(el) => registerRef(index.id, el)}> <Button id={index.id} /> </div>
                ))}

            </div>
        </div>
    );
};

export default Breadboard;
