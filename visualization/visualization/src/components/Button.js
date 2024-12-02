import Widget from "./Widget.js";
import ButtonImage from "../assets/button.svg";
import "./Button.css";

const Button = () => {
    return (
        <Widget>
            <div
                className={`button`}
            >
                <img
                    src={ButtonImage} alt="button" className='button-image' />
            </div>
        </Widget>

    )
}

export default Button;
