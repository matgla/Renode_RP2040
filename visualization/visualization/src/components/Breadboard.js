import React, { useState, useEffect, useRef, forwardRef, useImperativeHandle } from 'react';
import './Breadboard.css';
import breadboardImage from "../assets/breadboard.svg"
import Led from "./Led.js";
import Button from "./Button.js";
import MCU from "./MCU.js";
import SegmentDisplay from "./SegmentDisplay.js";

const Breadboard = forwardRef(({ editWidget }, ref) => {
    const [leds, setLeds] = useState([]);
    const [buttons, setButtons] = useState([]);
    const [segmentDisplays, setSegmentDisplays] = useState([]);

    const layoutMap = useRef({});
    const layoutCache = useRef();

    const registerLayoutElement = (name, element) => {
        console.log("Register: ", name)
        if (!Object.hasOwn(layoutMap.current, name)) {
            layoutMap.current[name] = element;
            // if there is layout re render it 
            if (layoutCache.current) {
                loadLayoutFromObject(layoutCache.current);
            }
        }
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

            board.segmentDisplays = {};
            segmentDisplays.forEach((display) => {
                board.segmentDisplays[display.id] = layoutMap.current[display.id].serialize();
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
        },
        loadLayout(layout) {
            loadLayoutFromObject(layout);
        }
    }));

    const loadLayoutFromObject = (layout) => {
        console.log("Loading layout from file");
        layoutCache.current = layout;
        for (const board of layout.boards) {
            for (const mcu in board.mcus) {
                if (Object.hasOwn(layoutMap.current, mcu)) {
                    layoutMap.current[mcu].deserialize(board.mcus[mcu]);
                }
            }
            for (const led in board.leds) {
                if (Object.hasOwn(layoutMap.current, led)) {
                    layoutMap.current[led].deserialize(board.leds[led]);
                }
            }
            for (const button in board.buttons) {
                if (Object.hasOwn(layoutMap.current, button)) {
                    layoutMap.current[button].deserialize(board.buttons[button]);
                }
            }
            for (const display in board.segmentDisplays) {
                if (Object.hasOwn(layoutMap.current, display)) {
                    layoutMap.current[display].deserialize(board.segmentDisplays[display]);
                }
            }
        }

    }

    const changeLedState = (name, state) => {
        if (Object.hasOwn(layoutMap.current, name)) {
            layoutMap.current[name].light(state);
        }
    }

    const changeSegmentDisplayState = (name, cells, segments) => {
        if (Object.hasOwn(layoutMap.current, name)) {
            layoutMap.current[name].changeState(cells, segments);
        }
    }


    const handleButtonPress = (name) => {
        ws.current.send(JSON.stringify({ type: "action", target: "button", action: "press", name: name }));
    }

    const handleButtonRelease = (name) => {
        ws.current.send(JSON.stringify({ type: "action", target: "button", action: "release", name: name }));
    }

    useEffect(() => {
        if (!ws.current) {
            ws.current = new WebSocket("ws://" + window.location.host + "/ws");
            ws.current.onmessage = (event) => {
                const msg = JSON.parse(event.data);
                if (msg.msg == "register") {
                    if (msg.peripheral_type == "led") {
                        if (!leds.includes(msg.name)) {
                            setLeds((prevLeds) => [...prevLeds, { id: msg.name, status: msg.status }]);
                        }
                        return;
                    }
                    else if (msg.peripheral_type == "button") {
                        if (!buttons.includes(msg.name)) {
                            setButtons((prevLeds) => [...prevLeds, { id: msg.name }]);
                        }
                        return;
                    }
                    else if (msg.peripheral_type == "segment_display") {
                        if (!segmentDisplays.includes(msg.name)) {
                            console.log("Adding segment display: ", msg.name);
                            setSegmentDisplays((prev) => [...prev, { id: msg.name, segments: msg.segments, cells: msg.cells, colon: msg.colon }]);
                        }
                        return;
                    }
                }
                else if (msg.msg == "state_change") {
                    if (msg.peripheral_type == "led") {
                        changeLedState(msg.name, msg.state);
                        return;
                    }
                    else if (msg.peripheral_type == "segment_display") {
                        changeSegmentDisplayState(msg.name, msg.cells, msg.segments);
                        return;
                    }
                }
                else if (msg.msg == "load_layout") {
                    console.log("Loaded layout from file: ", msg.file)
                    loadLayoutFromObject(msg.file);
                    return;
                }
                else if (msg.msg == "set_board_element") {
                    console.log("Element ", msg.name, " belongs to board");
                    if (Object.hasOwn(layoutMap.current, msg.name)) {
                        delete layoutMap.current[msg.name];
                    }
                    const newLeds = leds.filter(item => item !== msg.name);
                    setLeds(newLeds);

                    const newButtons = buttons.filter(item => item !== msg.name);
                    setButtons(newButtons);
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
            }
        }
    }, []);


    return (
        <div className='breadboard' style={{ minWidth: "600px" }}>
            <img src={breadboardImage} alt="Breadboard" className='breadboard-image' />
            <div className="grid">
                <MCU id="mcu" className="gridItem" ref={(el) => registerLayoutElement("mcu", el)} />
                {
                    leds.map((index) => (
                        <Led id={index.id}
                            ref={(el) => registerLayoutElement(index.id, el)}
                            editWidget={editWidget} name={index.id}
                        />
                    ))
                }
                {
                    buttons.map((index) => (
                        <Button id={index.id} ref={(el) => registerLayoutElement(index.id, el)}
                            onPress={() => {
                                handleButtonPress(index.id);
                            }}
                            onRelease={() => {
                                handleButtonRelease(index.id);
                            }}
                        />
                    ))
                }
                {
                    segmentDisplays.map((index) => (
                        <SegmentDisplay id={index.id}
                            ref={(el) => registerLayoutElement(index.id, el)}
                            editWidget={editWidget}
                            name={index.id}
                            style={{
                                display: "flex"
                            }}
                            segments={index.segments}
                            cells={index.cells}
                            colon={index.colon}
                        />
                    ))
                }
            </div>

        </div >
    );
});

export default Breadboard;
