import Widget from "./Widget.js"

import McuImage from "../assets/Raspberry_Pi_Pico_top.png";
import { useRef, forwardRef, useImperativeHandle } from "react";

const MCU = forwardRef(({}, ref) => {
    const child = useRef({});
    useImperativeHandle(ref, () => ({
        serialize() {
            return {
                position: child.current.getCoordinates()
            };
        }
    }));  

    return (
        <Widget width={8} height={3} ref={child}>
            <img src={McuImage} alt="mcu" className='widget-image' />
        </Widget>
    );
});

export default MCU;

