import React, { useState, useEffect, useRef } from "react";
import Draggable from 'react-draggable';

const Widget = ({ children, width = 1, height = 1 }) => {
    const [position, setPosition] = useState({ x: 0, y: 0 });
    const [gridSize, setGridSize] = useState([25, 25])
    const [gridPosition, setGridPosition] = useState({row: 1, column: 1})
    const [gridDimension, setGridDimension] = useState({width: width, height: height})
    const [rowSpan, setRowSpan] = useState(height);
    const draggableRef = useRef(null);

    const handleDrag = (e, data) => {
        console.log("width: " + width);
        const gridSize = getGridSize();
        setGridSize([gridSize, gridSize]);
        const newPosition = {
            x: data.x,
            y: data.y
        };
        setPosition(newPosition);

        const newGridPosition = {
            column: Math.round(newPosition.x / gridSize) + 1,
            row: Math.round(newPosition.y / gridSize) + 1
        }
        setGridPosition(newGridPosition);
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

    useEffect(() => {
        const handleResize = (e) => {
            const gridSize = getGridSize();
            setGridSize([gridSize, gridSize]);
            const newPosition = {
                x: (gridPosition.column - 1) * gridSize, y: (gridPosition.row - 1) * gridSize
            };
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
            grid={gridSize}
            onMouseDown={preventDefault}
            onDragStart={preventDefault}
        >
            <div ref={draggableRef}
                style={{ cursor: 'pointer', gridRow: `1 / span ${gridDimension.height} `, gridColumn: `1 / span ${gridDimension.width}` }}
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
