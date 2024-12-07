import Widget from "./Widget.js"

import McuImage from "../assets/Raspberry_Pi_Pico_top.png";
import { useRef, forwardRef, useImperativeHandle } from "react";

const MCU = forwardRef(({ }, ref) => {
    const child = useRef({});
    useImperativeHandle(ref, () => ({
        serialize() {
            return {
                position: child.current.getCoordinates()
            };
        },
        deserialize(data) {
            child.current.deserialize(data);
        }
    }));

    return (
        <Widget width={8} height={3} ref={child}>
            <img style={{ marginTop: "-3px" }} src={McuImage} alt="mcu" className='widget-image' />
        </Widget>
    );
});

export default MCU;

