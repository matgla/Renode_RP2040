import Widget from "./Widget.js"

import McuImage from "../assets/Raspberry_Pi_Pico_top.jpg";

const MCU = () => {
    return (
        <Widget width={8} height={3}>
            <img src={McuImage} alt="mcu" className='widget-image' />
        </Widget>
    );
}

export default MCU;

