import Widget from "./Widget.js";
import ButtonImage from "../assets/button.svg";
import { useRef, forwardRef, useImperativeHandle } from "react";

const Button = forwardRef(({}, ref) => {
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
            <img src={ButtonImage} alt="button" className='widget-image' />
        </Widget>
    )
});

export default Button;
