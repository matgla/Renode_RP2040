import React from "react";
import "./Button.css";

const Button = ({ onClick }) => {
    return (
        <button className="button" onClick={onClick}>Press me</button>
    )
}

export default Button;