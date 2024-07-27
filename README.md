# Renode RP2040 Simulation (**WIP**)

> [!CAUTION]
> **_work in progress_** - it may contains bugs or incorrect simulators behaviour.

This repository contains RP2040 MCU simulation description for [Renode](https://github.com/renode/renode).
Currently supported peripherals described in peripherals section.

It is a framework to build your own board level simulations that uses RP2040.
There is predefined Raspberry Pico board description in: 'boards/raspberry_pico.repl'

# Supported Peripherals And Hardware 

|    Peripheral   |  Supported    | Known Limitations  |
|       :---:     |     :---:     |       :---:        |
|    **SIO**      |      $${\color{yellow}✓}$$       | Partially supported, limitations to be filled when known                 |
| **IRQ**  | $${\color{red}✗}$$ | Propagation from peripherals is not implemented |
| **DMA**  | $${\color{red}✗}$$  | DMA in peripherals not yet implemented, MCU may support due to official ARM emulation |
| **Clocks** | $${\color{yellow}✓}$$ | Clocks are currently just stubs to pass PicoSDK initialization, but virtual time is always correct | 
| **GPIO** | $${\color{yellow}✓}$$ | Pins manipulation implemented, limitations not yet known except when some pins changed PIO may needs to be manually reevaluated due to CPU emulation (it's not step by step). Look for RP2040_SPI (PL022) peripheral as an example |
| **XOSC** |  $${\color{red}✗}$$  | |
| **ROSC** | $${\color{red}✗}$$  | |
| **PLL** | $${\color{red}✗}$$  | |
| **SysConfig** | $${\color{red}✗}$$  | |
| **SysInfo** | $${\color{red}✗}$$  | | 
| **PIO** |  $${\color{yellow}✓}$$  | Manual reevaluation may be neccessary to synchronize PIO together with actions on MCU. IRQ and DMA not yet supported |
| **USB** | $${\color{red}✗}$$  |  |
| **UART** | $${\color{green}✓}$$  | Official PL011 simulation from Renode, limitations not yet known except it doen't manage GPIO states so can't interwork with PIO |
| **SPI** |  $${\color{yellow}✓}$$ | Clock configuration not yet supported. Only master mode implemented with only one mode. Interworking with PIO is implemented! |
| **I2C** |  $${\color{red}✗}$$  |  |
| **PWM** |  $${\color{red}✗}$$  |  |
| **Timers** | $${\color{yellow}✓}$$  | Alarms implemented, but not all registers |
| **Watchdog** | $${\color{red}✗}$$  | |
| **RTC** | $${\color{red}✗}$$  | |
| **ADC** | $${\color{red}✗}$$  | |
| **SSI** | $${\color{red}✗}$$  | |

# How PIO simulation works 

PIO is implemented as external simulator written in C++: `piosim` directory. Decision was made due to performance issues with C# implementation. 
Due to that PIO is modelled as additional CPU. 
Renode executes more than 1 step at once on given CPU, so manual synchronization is necessary in some cases, like interworking between SPI and PIO. 

`piosim` requires `cmake` and `C++` compiler available on machine. 

It is compiled by renode simulation automatically, no manual steps are necessary. 

# How to use Raspberry Pico simulation

To use Raspberry Pico simulation clone Renode_RP2040 repository, then add path to it and include `boards/initialize_raspberry_pico.resc`. 

Example use:
```
(monitor) path add @repos/Renode_RP2040 
(monitor) include @repos/Renode_RP2040/board/initialize_raspberry_pico.resc 
(raspberry_pico) sysbus LoadELF @repos/Renode_RP2040/pico-examples/build/hello_world/serial/
hello_serial.elf
(raspberry_pico) sysbus.cpu0 VectorTableOffset `sysbus GetSymbolAddress "__VECTOR_TABLE"`
(raspberry_pico) sysbus.cpu1 VectorTableOffset `sysbus GetSymbolAddress "__VECTOR_TABLE"`
(raspberry_pico) showAnalyzer sysbus.uart0
(raspberry_pico) start
```
> [!NOTE] 
> set VectorTableOffset to valid address for your firmware, for pico-examples it can be __VECTOR_TABLE symbol.

You may use it inside your simulation scripts, look at `tests/prepare.resc` as an example.

# How to define own board 
Raspberry Pico configuration may be extended to configure board connections. 
As an example you can check `tests/pio/clocked_input/raspberry_pico_with_redirected_spi.repl`.

You may also build your own board using pure RP2040 target. 

Example from [MSPC Board Simulation](https://github.com/matgla/mspc-south-bridge/) simulation directory: 
```
path add @${RENODE_BOARD_DIR}
$machine_name="mspc_north_bridge"
include @initialize_rp2040.resc
```

# Multi Node simulation. 
Many RP2040 simulators may interwork together. I am using that possibility in full MSPC simulation. To interwork between them GPIOConnector may be used, please check existing usage (`simulation` directory):
 [MSPC Board Simulation](https://github.com/matgla/mspc-south-bridge/) 

# Testing 
I am testing simulator code using official pico-examples. Tests in use are: 

**ADC**
| Example | Passed |
| :---: | :---:    |




# License 

MIT License

Copyright (c) 2024 Mateusz Stadnik (matgla@live.com)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
