var ws;

i = 0;
ledColors = ["red", "green", "blue", "orange", "pink"];

function getNextColor() {
  colorIndex = i++ % ledColors.length;
  console.log("Getting: ", colorIndex);

  return ledColors[colorIndex];
}

function changeLedState(led, state, color = null) {
  svg = led.querySelector("svg");
  if (svg) {
    const circle = svg.querySelector("#On");
    if (circle && !state) {
      if (circle.style != null) {
        circle.style.display = "none";
      }
    }
    else if (circle) {
      if (circle.style != null) {
        circle.style.display = "block";
      }
    }
    if (color) {
      console.log(color, " is ")
    }
    if (circle && color != null) {
      console.log("Setting color: " + color)
      const bulb = circle.querySelector("circle");
      if (bulb) {
        bulb.style.fill = color;
      }
    }
  }
}

function registerLed(name, state) {
  if (name == "led") {
    console.log("adding board led handler");
    return;
  }
  console.log("adding user led: ", name)
  var ledContainer = document.createElement("div");
  ledContainer.className = "ledElement"

  var led = document.createElement("div");
  fetch("./assets/led.svg")
    .then(response => response.text())
    .then(svgContent => {
      led.innerHTML = svgContent
      led.id = name;
      led.className = "led";
      console.log("Adding led");
      changeLedState(led, state, getNextColor());
      var ledText = document.createElement("div");
      ledText.innerHTML += name;
      ledContainer.appendChild(ledText)
      ledContainer.appendChild(led)
      document.getElementById("leds").appendChild(ledContainer);
    })
    .catch(error => console.error("Error loading SVG: ", error));
}

function sendMessage(obj) {
  ws.send(JSON.stringify(obj));
}

function registerButton(name) {
  var buttonsContainer = document.createElement("div");
  buttonsContainer.className = "buttonElement";
  var button = document.createElement("div");
  button.className = "button";
  button.id = name;

  fetch("./assets/button.svg")
    .then(response => response.text())
    .then(svgContent => {
      button.innerHTML = svgContent;
      var buttonText = document.createElement("div");
      buttonText.innerHTML += name;
      buttonsContainer.appendChild(buttonText);
      buttonsContainer.appendChild(button);
      document.getElementById("buttons").appendChild(buttonsContainer);
    })
    .catch(error => console.error("Error loading SVG: ", error));

  button.addEventListener('mousedown', function() {
    console.log("Button " + name + " pressed");
    let message = {
      "type": "action",
      "target": "button",
      "name": name,
      "action": "press"
    };
    sendMessage(message);
  });

  button.addEventListener('mouseup', function() {
    console.log("Button " + name + "released");
    let message = {
      "type": "action",
      "target": "button",
      "name": name,
      "action": "release"
    };
    sendMessage(message);
  })

  document.getElementById("buttons").appendChild(button);
}

function ledStateChange(msg) {
  changeLedState(document.getElementById(msg["name"]), msg["state"]);
}

function registerAsset(msg) {
  if (msg["peripheral_type"] == "led") {
    registerLed(msg["name"], msg["state"]);
  }
  if (msg["peripheral_type"] == "button") {
    registerButton(msg["name"]);
  }
}

function processMessage(msg) {
  if (msg["msg"] == "register") {
    registerAsset(msg);
    return;
  }
  if (msg["msg"] == "state_change") {
    ledStateChange(msg);
    return;
  }
  console.log("Got unhandled message: ", msg);
}

window.addEventListener('DOMContentLoaded', (event) => {
  let interactive = document.getElementsByClassName("interactive");
  ws = new WebSocket("ws://" + location.host + "/ws");

  ws.onopen = function() {
    console.log("WebSocket is open");
  }

  ws.onmessage = function(e) {
    const obj = JSON.parse(e.data);
    processMessage(obj);
  }

  ws.onclose = function() {
    console.log("WebSocket is close")
  }

  ws.onerror = function(e) {
    console.log("WebSocket error:")
    console.log(e)
  }
})
