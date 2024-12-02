import Widget from "./Widget.js"

import McuImage from "../assets/Raspberry_Pi_Pico_top.jpg";
import "./MCU.css";

const MCU = () => {
    return (
        <Widget width={8} height={3}>
            <div
                className={`mcu`}
            >
                <img src={McuImage} alt="mcu" className='mcu-image' />
            </div>
        </Widget>
    );
}

export default MCU;

