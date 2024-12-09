import Widget from "./Widget.js";
import { ReactComponent as SegmentDisplayImage } from "../assets/7segment.svg";
import { ReactComponent as SegmentColonImage } from "../assets/7segment_separator.svg";
import { forwardRef, useImperativeHandle, useRef, useState, useEffect } from "react";
import { ReactComponent as LedImage } from "../assets/led.svg";

const SegmentDisplay = forwardRef(({ cells, segments, colon, editWidget, name }, ref) => {
    const child = useRef({});
    const cellsRefs = useRef([]);
    const colonRef = useRef(null);
    var [color, setColor] = useState("#ff2020");
    const [offColor] = useState("#404040");

    const mapping = ["A", "B", "C", "D", "E", "F", "G", "DP"];
    var previousCells = Array(cells.length == 0 ? 1 : cells.length).fill(true);
    const timeouts = Array(cells.length == 0 ? 1 : cells.length).fill(null);
    var previousSegments = segments.slice();

    useEffect(() => {
        return () => {

        };
    }, [colonRef, cellsRefs, child]);

    useImperativeHandle(ref, () => ({
        serialize() {
            var obj = {
                position: child.current.getCoordinates()
            };
            if (color) {
                obj.color = color;
            }
            return obj;
        },
        deserialize(data) {
            child.current.deserialize(data);
            if (data.color) {
                color = data.color;
                setColor(color);
            }

        },
        changeState(cells, segments) {
            if (!cells || cells.length == 0) {
                cells = [false];
            }
            changeCells(cells, segments);
        }
    }));

    const changeCells = (cells, segments) => {
        for (var i = 0; i < cells.length; ++i) {
            if (timeouts[i]) {
                if (previousCells[i] !== cells[i]) {
                    clearTimeout(timeouts[i]);
                    timeouts[i] = null;
                }
            }

            if (!cells[i]) {
                changeSegments(cellsRefs.current[i], segments);
            } else if (timeouts[i] === null) {
                const cellId = i;
                timeouts[i] = setTimeout(() => {
                    setCellColor(cellsRefs.current[cellId], offColor);
                }, 200);
            }
        }
        previousCells = cells.slice();
    }

    const changeSegments = (cell, segments) => {
        for (var i = 0; i < segments.length; ++i) {
            var e = cell.querySelector("#" + mapping[i]);
            if (e) {
                var p = e.querySelector("path");
                if (!p) {
                    p = e.querySelector("circle");
                }
                if (p) {
                    if (segments[i]) {
                        p.style.fill = color;
                    } else {
                        p.style.fill = offColor;
                    }

                }
            }
        }
    }

    const onClickHandler = () => {
        if (editWidget) {
            editWidget.current.registerForColorChange(colorChange, name);
        }
    }

    const colorChange = (newColor) => {
        color = newColor;
        setColor(newColor);
    }

    const setCellColor = (cell, color) => {
        var segments = cell.querySelectorAll("path");
        segments.forEach((el) => {
            el.style.fill = color;
        });
        var dots = cell.querySelectorAll("circle");
        dots.forEach((el) => {
            el.style.fill = color;
        });
    }

    const setColonColor = (cell, color) => {
        var dots = cell.querySelectorAll("circle");
        dots.forEach((el) => {
            el.style.fill = color;
        });
    }


    const registerCell = (element, index) => {
        if (!element) {
            return;
        }
        cellsRefs.current[index] = element;
        setCellColor(cellsRefs.current[index], offColor);
    }

    const registerColon = (element) => {
        if (!element) {
            return;
        }

        colonRef.current = element;

        setColonColor(colonRef.current, offColor);
    }

    return (
        <Widget
            width={cells.length == 0 ? 1 : cells.length + colon == 0 ? 0 : 1}
            height={4}
            ref={child}
            onClick={onClickHandler}
        >
            <div className="widget-image" style={{
                display: "flex",
                flexDirection: "row"
            }}>
                {
                    ((!cells || cells.length == 0) && <SegmentDisplayImage
                        className="widget-item"
                        id={0}
                        ref={(el) => registerCell(el, 0)}
                    />)
                }
                {
                    cells.slice(0, colon).map((_, index) => (
                        <SegmentDisplayImage
                            className="widget-item"
                            id={index}
                            ref={(el) => registerCell(el, index)}
                        />
                    ))
                }
                {
                    (colon > 0 && <SegmentColonImage className="widget-item" ref={(el) => registerColon(el)} />)
                }
                {
                    cells.slice(colon).map((_, index) => (
                        <SegmentDisplayImage
                            className="widget-item"
                            id={index + colon}
                            ref={(el) => registerCell(el, colon + index)}
                        />
                    ))
                }


            </div>
        </Widget>
    )
});


export default SegmentDisplay;
