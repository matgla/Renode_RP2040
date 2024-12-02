import Widget from "./Widget.js"

import LedImage from "../assets/led.svg";
import "./Led.css";

const Led = () => {
    return (
        <Widget>
            <div
                className={`led`}
            >
                <img
                    src={LedImage} alt="led" className='led-image' />
            </div>
        </Widget>
    );
}

export default Led;
