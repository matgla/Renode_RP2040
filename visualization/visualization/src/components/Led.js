import React, {useState } from "react";
import Draggable from 'react-draggable';
import "./Led.css";
import LedImage from "../assets/led.svg";

const Led = ({gridRow, gridColumn, gridSize}) => {
    const [isOn, setIsOn] = useState(false);
    const [position, setPosition] = useState({x: 0, y: 0});
    const toggle = () => {
        setIsOn(!isOn);
    }

    const handleDrag = (e, data) => {
        setPosition({x: data.x, y: data.y});
    }

    const handleStop = (e, data) => {
        const snappedX = Math.round(data.x / gridSize) * gridSize;
        const snappedY = Math.round(data.y / gridSize) * gridSize;
        setPosition({x: snappedX, y: snappedY });
    }

    return (
        <Draggable
            position={position}
            onDrag={handleDrag} 
            onStop={handleStop}
            grid={[gridSize, gridSize]}
        >
            <div 
                className={`led ${isOn ? 'on' : ''}`} 
                onClick={toggle}
                style={{ gridRow: gridRow, gridColumn: gridColumn }}
            >
            <img src={LedImage} alt="led" className='led' />
            </div>
        </Draggable>
    );
}

export default Led;