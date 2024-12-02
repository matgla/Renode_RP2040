import React from 'react';
import './Breadboard.css';
import breadboardImage from "../assets/breadboard.svg"
import Led from "./Led.js";
import Button from "./Button.js";



const Breadboard = ({ gridColumns, gridRows }) => {
    const createGridTemplateAreas = () => {
        console.log("Grid: ", gridColumns, "x", gridRows);
        let areas = '';
        for (let i = 0; i < gridRows; ++i) {
            for (let j = 0; j < gridColumns; j++) {
                areas += `item${i * gridColumns + j + 1} `;
            }
            areas += "\n";
        }
        return areas;
    }

    return (
        <div className='breadboard'>
            <img src={breadboardImage} alt="Breadboard" className='breadboard-image' />
            <div className="grid">
                <Led />
                <Button />
            </div>
        </div>
    );
};

export default Breadboard;
