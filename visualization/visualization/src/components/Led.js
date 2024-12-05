import Widget from "./Widget.js"
import { ReactComponent as LedImage } from "../assets/led.svg";
import { useRef, forwardRef, useImperativeHandle } from "react";

const Led = forwardRef(({}, ref) => {
    const child = useRef({}); 
    useImperativeHandle(ref, () => ({
        serialize() {
            return {
                position: child.current.getCoordinates()
            };
        }
    }));
    return (
        <Widget ref={child}>
            <LedImage />
        </Widget >
    );
});

export default Led;
