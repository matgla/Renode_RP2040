import React from 'react';
import './Breadboard.css';
import breadboardImage from "../assets/breadboard.svg"
import Led from "./Led.js"; 

const Breadboard = () => {
    return (
        <div className='breadboard'>
            <img src={breadboardImage} alt="Breadboard" className='breadboard-image' />
            <div className="grid">
                <Led gridRow={1} gridColumn={2} gridSize="48px"/>
            </div>
        </div>
    );
};

export default Breadboard;