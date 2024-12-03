import Widget from "./Widget.js";
import ButtonImage from "../assets/button.svg";

const Button = () => {
    return (
        <Widget>
            <img src={ButtonImage} alt="button" className='widget-image' />
        </Widget>
    )
}

export default Button;
