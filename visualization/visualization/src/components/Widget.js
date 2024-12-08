import React, { useState, useEffect, useRef, forwardRef, useImperativeHandle } from "react";
import Draggable from 'react-draggable';
import "./Widget.css";

const Widget = forwardRef(({ children, width = 1, height = 1, onClick = null, onRelease = null }, ref) => {
    const [position, setPosition] = useState({ x: 0, y: 0 });
    const [gridSize, setGridSize] = useState([25, 25])
    const [gridPosition, setGridPosition] = useState({ row: 1, column: 1 })
    const [gridDimension, setGridDimension] = useState({ width: width, height: height })
    const draggableRef = useRef(null);
    const [initialized, setInitialized] = useState(false);

    useImperativeHandle(ref, () => ({
        getCoordinates() {
            return gridPosition;
        },
        deserialize(data) {
            const gridSize = getGridSize();
            setGridSize([gridSize, gridSize]);
            setGridPosition(data.position);
            moveAccordingToGrid();
        }
    }))

    useEffect(() => {
        const gridSize = getGridSize();
        setGridSize([gridSize, gridSize]);
        return () => {
        };
    }, [initialized]);

    useEffect(() => {
        moveAccordingToGrid();
    }, [gridPosition]);

    const recalculateGrid = (newPosition) => {
        const newGridPosition = {
            column: Math.round(newPosition.x / gridSize[0]) + 1,
            row: Math.round(newPosition.y / gridSize[0]) + 1
        }
        setGridPosition(newGridPosition);
    }

    const snapTo = (newPosition) => {
        const snappedX = Math.round(newPosition.x / gridSize[0]) * gridSize[0];
        const snappedY = Math.round(newPosition.y / gridSize[0]) * gridSize[0];
        setPosition({ x: snappedX, y: snappedY })
    }

    const moveAccordingToGrid = () => {
        const newPosition = {
            x: (gridPosition.column - 1) * gridSize[0], y: (gridPosition.row - 1) * gridSize[0]
        };

        setPosition(newPosition);

    }

    const handleDrag = (e, data) => {
        const gridSize = getGridSize();
        setGridSize([gridSize, gridSize]);

        const newPosition = {
            x: data.x,
            y: data.y
        };

        setPosition(newPosition);
        recalculateGrid(newPosition);
    }

    const preventDefault = (e) => {
        e.preventDefault();
    }

    const onMouseClick = (e) => {
        preventDefault(e);
    }

    const getGridSize = () => {
        const gridElement = document.querySelector('.grid');
        const gridStyle = window.getComputedStyle(gridElement);
        const gridColumns = gridStyle.gridTemplateColumns.split(" ").length;
        const gridSize = gridElement.clientWidth / gridColumns;
        return gridSize;
    }

    const handleStop = (e, data) => {
        snapTo(data);
    }

    useEffect(() => {
        const handleResize = (e) => {
            const gridSize = getGridSize();
            setGridSize([gridSize, gridSize]);
            moveAccordingToGrid();
        };

        window.addEventListener('resize', handleResize);

        return () => {
            window.removeEventListener('resize', handleResize);
        };
    }, [position]);

    return (
        <div
            className="grid-item"
            style={{
                display: "flex",
                gridRow: `1 / span ${gridDimension.height} `,
                gridColumn: `1 / span ${gridDimension.width}`,
                minWidth: "0",
                minHeight: "0",
            }}
        >
            <Draggable
                position={position}
                onDrag={handleDrag}
                onStop={handleStop}
                grid={gridSize}
                onMouseDown={onMouseClick}
                onDragStart={preventDefault}
                onMouseUp={() => { console.log("Button released"); if (onRelease) onRelease(); }}
            >
                <div ref={draggableRef}
                    style={{ cursor: 'pointer' }}

                    onPointerDown={(e) => {
                        if (onClick) {
                            onClick();
                        }
                        if (draggableRef.current) {
                            draggableRef.current.setPointerCapture(e.pointerId);
                        }
                    }}
                    onPointerUp={(e) => {
                        if (onRelease) {
                            onRelease();
                        }
                        if (draggableRef.current) {
                            draggableRef.current.releasePointerCapture(e.pointerId);
                        }
                    }}
                >
                    {children}
                </div>
            </Draggable>
        </div>
    );
});

export default Widget;
