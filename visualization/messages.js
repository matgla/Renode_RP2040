var ws;

function registerLed(name, state) {
  if (name == "led") {
    console.log("adding board led handler");
    return;
  }
  console.log("adding user led: ", name)
  var led = document.createElement("div");
  led.className = state ? "led_on" : "led_off";
  led.id = name;

  document.getElementById("leds").appendChild(led);
}

function sendMessage(obj) {
  ws.send(JSON.stringify(obj));
}

function registerButton(name) {
  var button = document.createElement("div");
  button.className = "button";
  button.id = name;
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
  if (msg["state"]) {
    document.getElementById(msg["name"]).className = "led_on";
  } else {
    document.getElementById(msg["name"]).className = "led_off";
  }

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

  ws.onopen = function () {
    console.log("WebSocket is open");
  }

  ws.onmessage = function (e) {
    const obj = JSON.parse(e.data);
    processMessage(obj); 
  }

  ws.onclose = function () {
    console.log("WebSocket is close")
  }

  ws.onerror = function (e) {
    console.log("WebSocket error:")
    console.log(e)
  }
})
