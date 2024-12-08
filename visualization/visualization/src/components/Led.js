import Widget from "./Widget.js"
import { ReactComponent as LedImage } from "../assets/led.svg";
import { useEffect, useRef, forwardRef, useImperativeHandle, useState } from "react";

const Led = forwardRef(({ editWidget, name }, ref) => {
    const child = useRef({});
    const image = useRef({});
    const [state, setState] = useState(false);
    var [color, setColor] = useState(null);

    useImperativeHandle(ref, () => ({
        serialize() {
            var obj = {
                position: child.current.getCoordinates(),
            };
            if (color) {
                obj.color = color;
            }
            return obj;
        },

        deserialize(data) {
            if (data.color) {
                color = data.color;
                setColor(data.color);
            }
            child.current.deserialize(data);
        },

        light(enable) {
            setState(enable);
        }
    }));

    useEffect(() => {
        updateColor();
        return () => {

        };
    }, [color, state]);

    const onClickHandler = () => {
        if (editWidget) {
            editWidget.current.registerForColorChange(colorChange, name);
        }
    }

    const updateColor = () => {
        if (!color) {
            return;
        }
        const on = image.current.querySelector("#On")
        if (on) {
            on.style.fill = color;
            if (!state) {
                on.style.display = "none";
            }
            else {
                on.style.display = null;
            }
        }
        const outline = image.current.querySelector("#Outline");
        if (outline) {
            outline.style.stroke = color;
            outline.style.fill = color;
        }

    }

    const colorChange = (newColor) => {
        color = newColor;
        setColor(newColor);
    }

    return (
        <Widget ref={child} onClick={onClickHandler} width={2} height={2}>
            <LedImage ref={image} className="widget-image" />
        </Widget >
    );
});

export default Led;
