import React, { useState, useEffect, useRef } from "react";
import Draggable from 'react-draggable';

const Widget = ({ children }) => {
    const [position, setPosition] = useState({ x: 0, y: 0 });
    const [gridSize, setGridSize] = useState([25, 25])
    const draggableRef = useRef(null);

    const handleDrag = (e, data) => {
        const gridSize = getGridSize();
        setGridSize([gridSize, gridSize]);
        const newPosition = {
            x: data.x,
            y: data.y
        };
        setPosition(newPosition);
    }

    const preventDefault = (e) => {
        e.preventDefault();
    }

    const getGridSize = () => {
        const gridElement = document.querySelector('.grid');
        const gridStyle = window.getComputedStyle(gridElement);
        const gridColumns = gridStyle.gridTemplateColumns.split(" ").length;
        const gridSize = gridElement.clientWidth / gridColumns;
        return gridSize;
    }

    const handleStop = (e, data) => {
        const snappedX = Math.round(data.x / gridSize[0]) * gridSize[0];
        const snappedY = Math.round(data.y / gridSize[0]) * gridSize[0];
        setPosition({ x: snappedX, y: snappedY })
    }

    useEffect(() => {
        const handleResize = (e) => {
            const gridSize = getGridSize();
            setGridSize([gridSize, gridSize]);
            const snappedX = Math.round(position.x / gridSize) * gridSize;
            const snappedY = Math.round(position.y / gridSize) * gridSize;
            const newPosition = {
                x: snappedX, y: snappedY
            };
            console.log(newPosition);
            setPosition(newPosition);
        };

        window.addEventListener('resize', handleResize);

        return () => {
            window.removeEventListener('resize', handleResize);
        };
    }, [position]);

    return (
        <Draggable
            position={position}
            onDrag={handleDrag}
            onStop={handleStop}
            grid={gridSize}
            onMouseDown={preventDefault}
            onDragStart={preventDefault}
        >
            <div ref={draggableRef}
                style={{ cursor: 'pointer' }}
                onPointerDown={(e) => {
                    if (draggableRef.current) {
                        draggableRef.current.setPointerCapture(e.pointerId);
                    }
                }}
                onPointerUp={(e) => {
                    if (draggableRef.current) {
                        draggableRef.current.releasePointerCapture(e.pointerId);
                    }
                }}
            >
                {children}
            </div>
        </Draggable>
    );
}

export default Widget;
