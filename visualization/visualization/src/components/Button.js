import Widget from "./Widget.js";
import { ReactComponent as ButtonImage } from "../assets/button.svg";
import { useRef, forwardRef, useImperativeHandle } from "react";

const Button = forwardRef(({ onPress, onRelease }, ref) => {
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
        <Widget ref={child} onClick={onPress} onRelease={onRelease} width={2} height={2}>
            <ButtonImage className='widget-image' />
        </Widget >
    )
});

export default Button;
