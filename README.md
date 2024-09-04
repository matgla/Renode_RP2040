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
| **DMA**  | $${\color{red}✗}$$  | DMA in peripherals not yet implemented |
| **Clocks** | $${\color{yellow}✓}$$ | Clocks are currently just stubs to pass PicoSDK initialization, but virtual time is always correct | 
| **GPIO** | $${\color{yellow}✓}$$ | Pins manipulation implemented, limitations not yet known except when some pins changed PIO may needs to be manually reevaluated due to CPU emulation (it's not step by step). Look for RP2040_SPI (PL022) peripheral as an example |
| **XOSC** |  $${\color{yellow}✓}$$  | |
| **ROSC** | $${\color{yellow}✓}$$  | |
| **PLL** | $${\color{yellow}✓}}$$  | |
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
| **XIP** | $${\color{yellow}✓}$$  | Partially implemented, bootrom correctly starts firmware |



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

## ADC
| Example | Passed |
| :---: | :---:    |
| [adc_console](https://github.com/raspberrypi/pico-examples/tree/master/adc/adc_console) | $${\color{red}✗}$$ |
| [dma_capture](https://github.com/raspberrypi/pico-examples/tree/master/adc/dma_capture) | $${\color{red}✗}$$ |
| [hello_adc](https://github.com/raspberrypi/pico-examples/tree/master/adc/hello_adc) | $${\color{green}✓}$$ |
| [joystick_display](https://github.com/raspberrypi/pico-examples/tree/master/adc/joystick_display) | $${\color{red}✗}$$ | 
| [microphone_adc](https://github.com/raspberrypi/pico-examples/tree/master/adc/microphone_adc) | $${\color{red}✗}$$ | 
| [onboard_temperature](https://github.com/raspberrypi/pico-examples/tree/master/adc/onboard_temperature) | $${\color{red}✗}$$ | 
| [read_vsys](https://github.com/raspberrypi/pico-examples/tree/master/adc/read_vsys) | $${\color{red}✗}$$ | 

## Blink
| Example | Passed |
| :---: | :---:    |
| [blink](https://github.com/raspberrypi/pico-examples/tree/master/blink) | $${\color{green}✓}$$ |

## Clocks
| Example | Passed |
| :---: | :---:    |
| [detached_clk_peri](https://github.com/raspberrypi/pico-examples/tree/master/clocks/detached_clk_peri) | $${\color{green}✓}$$ |
| [hello_48MHz](https://github.com/raspberrypi/pico-examples/tree/master/clocks/hello_48MHz) | $${\color{green}✓}$$ | 
| [hello_gpout](https://github.com/raspberrypi/pico-examples/tree/master/clocks/hello_gpout) | $${\color{red}✗}$$ |
| [hello_resus](https://github.com/raspberrypi/pico-examples/tree/master/clocks/hello_resus) | $${\color{green}✓}$$ |

## Divider

| Example | Passed |
| :---: | :---:    |
| [divider](https://github.com/raspberrypi/pico-examples/tree/master/divider) | $${\color{green}✓}$$ |

## DMA
| Example | Passed |
| :---: | :---:    |
| [channel_irq](https://github.com/raspberrypi/pico-examples/tree/master/dma/channel_irq) | $${\color{red}✗}$$ |
| [control_blocks](https://github.com/raspberrypi/pico-examples/tree/master/dma/control_blocks) | $${\color{red}✗}$$ |
| [hello_dma](https://github.com/raspberrypi/pico-examples/tree/master/dma/hello_dma) | $${\color{red}✗}$$ |
| [sniff_crc](https://github.com/raspberrypi/pico-examples/tree/master/dma/sniff_crc) | $${\color{red}✗}$$ |

## Flash
| Example | Passed |
| :---: | :---:    |
| [cache_perfctr](https://github.com/raspberrypi/pico-examples/tree/master/flash/cache_perfctr) | $${\color{red}✗}$$ | 
| [nuke](https://github.com/raspberrypi/pico-examples/tree/master/flash/nuke) | $${\color{red}✗}$$ |
| [program](https://github.com/raspberrypi/pico-examples/tree/master/flash/program) | $${\color{red}✗}$$ |
| [ssi_dma](https://github.com/raspberrypi/pico-examples/tree/master/flash/ssi_dma) | $${\color{red}✗}$$ |
| [xip_stream](https://github.com/raspberrypi/pico-examples/tree/master/flash/xip_stream) | $${\color{red}✗}$$ |

## GPIO
| Example | Passed |
| :---: | :---:    |
| [dht_sensor](https://github.com/raspberrypi/pico-examples/tree/master/gpio/dht_sensor) | $${\color{red}✗}$$ |
| [hello_7segment](https://github.com/raspberrypi/pico-examples/tree/master/gpio/hello_7segment) | $${\color{red}✗}$$ |
| [hello_gpio_irq](https://github.com/raspberrypi/pico-examples/tree/master/gpio/hello_gpio_irq) | $${\color{red}✗}$$ |

## Hello World

| Example | Passed |
| :---: | :---:    |
| [serial](https://github.com/raspberrypi/pico-examples/tree/master/hello_world/serial) | $${\color{green}✓}$$ |
| [usb](https://github.com/raspberrypi/pico-examples/tree/master/hello_world/usb) | $${\color{red}✗}$$ |

## I2C
| Example | Passed |
| :---: | :---:    |
| [bmp280_i2c](https://github.com/raspberrypi/pico-examples/tree/master/i2c/bmp280_i2c) | $${\color{red}✗}$$ |
| [bus_scan](https://github.com/raspberrypi/pico-examples/tree/master/i2c/bus_scan) | $${\color{red}✗}$$ | 
| [ht16k33_i2c](https://github.com/raspberrypi/pico-examples/tree/master/i2c/ht16k33_i2c) | $${\color{red}✗}$$ |
| [lcd_1602_i2c](https://github.com/raspberrypi/pico-examples/tree/master/i2c/lcd_1602_i2c) | $${\color{red}✗}$$ |
| [lis3dh_i2c](https://github.com/raspberrypi/pico-examples/tree/master/i2c/lis3dh_i2c) | $${\color{red}✗}$$ |
| [mpc9808_i2c](https://github.com/raspberrypi/pico-examples/tree/master/i2c/mcp9808_i2c) | $${\color{red}✗}$$ |
| [mma8451_i2c](https://github.com/raspberrypi/pico-examples/tree/master/i2c/mma8451_i2c) | $${\color{red}✗}$$ |
| [mpl3115a2_i2c](https://github.com/raspberrypi/pico-examples/tree/master/i2c/mpl3115a2_i2c) | $${\color{red}✗}$$ |
| [mpu6050_i2c](https://github.com/raspberrypi/pico-examples/tree/master/i2c/mpu6050_i2c) | $${\color{red}✗}$$ |
| [pa1010d_i2c](https://github.com/raspberrypi/pico-examples/tree/master/i2c/pa1010d_i2c) | $${\color{red}✗}$$ |
| [pcf8523_i2c](https://github.com/raspberrypi/pico-examples/tree/master/i2c/pcf8523_i2c) | $${\color{red}✗}$$ |
| [slave_mem_i2c](https://github.com/raspberrypi/pico-examples/tree/master/i2c/slave_mem_i2c) | $${\color{red}✗}$$ |
| [ssd1306_i2c](https://github.com/raspberrypi/pico-examples/tree/master/i2c/ssd1306_i2c) | $${\color{red}✗}$$ |

## Interp

| Example | Passed |
| :---: | :---:    |
| [hello_interp](https://github.com/raspberrypi/pico-examples/tree/master/interp/hello_interp) | $${\color{red}✗}$$ |

## Multicore
| Example | Passed |
| :---: | :---:    |
| [hello_multicore](https://github.com/raspberrypi/pico-examples/tree/master/multicore/hello_multicore) | $${\color{red}✗}$$ |
| [multicore_fifo_irqs](https://github.com/raspberrypi/pico-examples/tree/master/multicore/multicore_fifo_irqs) | $${\color{red}✗}$$ |
| [multicore_runners](https://github.com/raspberrypi/pico-examples/tree/master/multicore/multicore_runner) | $${\color{red}✗}$$ |
| [multicore_runner_queue](https://github.com/raspberrypi/pico-examples/tree/master/multicore/multicore_runner_queue) | $${\color{red}✗}$$ |

## PIO
| Example | Passed |
| :---: | :---:    |
| [addition](https://github.com/raspberrypi/pico-examples/tree/master/pio/addition) | $${\color{green}✓}$$ |
| [apa102](https://github.com/raspberrypi/pico-examples/tree/master/pio/apa102) | $${\color{red}✗}$$ |
| [clocked_input](https://github.com/raspberrypi/pico-examples/tree/master/pio/clocked_input) | $${\color{green}✓}$$ |
| [differential_manchester](https://github.com/raspberrypi/pico-examples/tree/master/pio/differential_manchester) | $${\color{green}✓}$$ |
| [hello_pio](https://github.com/raspberrypi/pico-examples/tree/master/pio/hello_pio) | $${\color{green}✓}$$ |
| [hub75](https://github.com/raspberrypi/pico-examples/tree/master/pio/hub75) | $${\color{red}✗}$$ | 
| [i2c](https://github.com/raspberrypi/pico-examples/tree/master/pio/i2c) | $${\color{red}✗}$$ |
| [ir_nec](https://github.com/raspberrypi/pico-examples/tree/master/pio/ir_nec) | $${\color{red}✗}$$ |
| [logic_analyser](https://github.com/raspberrypi/pico-examples/tree/master/pio/logic_analyser) | $${\color{red}✗}$$ |
| [manchester_encoding](https://github.com/raspberrypi/pico-examples/tree/master/pio/manchester_encoding) | $${\color{red}✗}$$ | 
| [onewire](https://github.com/raspberrypi/pico-examples/tree/master/pio/onewire) | $${\color{red}✗}$$ |
| [pio_blink](https://github.com/raspberrypi/pico-examples/tree/master/pio/pio_blink) | $${\color{green}✓}$$ |
| [pwm](https://github.com/raspberrypi/pico-examples/tree/master/pio/pwm) | $${\color{red}✗}$$ |
| [quadrature_encoder](https://github.com/raspberrypi/pico-examples/tree/master/pio/quadrature_encoder) | $${\color{red}✗}$$ |
| [spi](https://github.com/raspberrypi/pico-examples/tree/master/pio/spi) | $${\color{red}✗}$$ |
| [squarewave](https://github.com/raspberrypi/pico-examples/tree/master/pio/squarewave) | $${\color{red}✗}$$ |
| [st7789_lcd](https://github.com/raspberrypi/pico-examples/tree/master/pio/st7789_lcd) | $${\color{red}✗}$$ |
| [uart_rx](https://github.com/raspberrypi/pico-examples/tree/master/pio/uart_rx) | $${\color{red}✗}$$ |
| [uart_tx](https://github.com/raspberrypi/pico-examples/tree/master/pio/uart_tx) | $${\color{red}✗}$$ |
| [ws2812](https://github.com/raspberrypi/pico-examples/tree/master/pio/ws2812) | $${\color{red}✗}$$ |

## PWM
| Example | Passed |
| :---: | :---:    |
| [hello_pwm](https://github.com/raspberrypi/pico-examples/tree/master/pwm/hello_pwm) | $${\color{red}✗}$$ |
| [led_fade](https://github.com/raspberrypi/pico-examples/tree/master/pwm/led_fade) | $${\color{red}✗}$$ |
| [measure_duty_cycle](https://github.com/raspberrypi/pico-examples/tree/master/pwm/measure_duty_cycle) | $${\color{red}✗}$$ |

## Reset
| Example | Passed |
| :---: | :---:    |
| [hello_reset](https://github.com/raspberrypi/pico-examples/tree/master/reset/hello_reset) | $${\color{red}✗}$$ |

## RTC
| Example | Passed |
| :---: | :---:    |
| [hello_rtc](https://github.com/raspberrypi/pico-examples/tree/master/rtc/hello_rtc) | $${\color{red}✗}$$ |
| [rtc_alarm](https://github.com/raspberrypi/pico-examples/tree/master/rtc/rtc_alarm) | $${\color{red}✗}$$ |
| [rtc_alarm_repeat](https://github.com/raspberrypi/pico-examples/tree/master/rtc/rtc_alarm_repeat) | $${\color{red}✗}$$ |

## SPI
| Example | Passed |
| :---: | :---:    |
| [bme280_spi](https://github.com/raspberrypi/pico-examples/tree/master/spi/bme280_spi) | $${\color{red}✗}$$ |
| [max7219_32x8_spi](https://github.com/raspberrypi/pico-examples/tree/master/spi/max7219_32x8_spi) | $${\color{red}✗}$$ |
| [max7219_8x7seg_spi](https://github.com/raspberrypi/pico-examples/tree/master/spi/max7219_8x7seg_spi) | $${\color{red}✗}$$ |
| [mpu9250_spi](https://github.com/raspberrypi/pico-examples/tree/master/spi/mpu9250_spi) | $${\color{red}✗}$$ |
| [spi_dma](https://github.com/raspberrypi/pico-examples/tree/master/spi/spi_dma) | $${\color{red}✗}$$ |
| [spi_flash](https://github.com/raspberrypi/pico-examples/tree/master/spi/spi_flash) | $${\color{red}✗}$$ |
| [spi_master_slave](https://github.com/raspberrypi/pico-examples/tree/master/spi/spi_master_slave) | $${\color{red}✗}$$ |

## System
| Example | Passed |
| :---: | :---:    |
| [hello_double_tap](https://github.com/raspberrypi/pico-examples/tree/master/system/hello_double_tap) | $${\color{red}✗}$$ |
| [narrow_io_write](https://github.com/raspberrypi/pico-examples/tree/master/system/narrow_io_write) | $${\color{red}✗}$$ |
| [unique_board_id](https://github.com/raspberrypi/pico-examples/tree/master/system/unique_board_id) | $${\color{red}✗}$$ |

# Timer
| Example | Passed |
| :---: | :---:    |
| [hello_timer](https://github.com/raspberrypi/pico-examples/tree/master/timer/hello_timer) | $${\color{green}✓}$$ |
| [periodic_sampler](https://github.com/raspberrypi/pico-examples/tree/master/timer/periodic_sampler) | $${\color{red}✗}$$ |
| [timer_lowlevel](https://github.com/raspberrypi/pico-examples/tree/master/timer/timer_lowlevel) | $${\color{green}✓}$$ |

# USB 
| Example | Passed |
| :---: | :---:    |
| [device](https://github.com/raspberrypi/pico-examples/tree/master/usb/device) | $${\color{red}✗}$$ |
| [dual](https://github.com/raspberrypi/pico-examples/tree/master/usb/dual) | $${\color{red}✗}$$ |
| [host](https://github.com/raspberrypi/pico-examples/tree/master/usb/host) | $${\color{red}✗}$$ | 

# Watchdog 
| Example | Passed |
| :---: | :---:    |
| [hello_watchdog](https://github.com/raspberrypi/pico-examples/tree/master/watchdog/hello_watchdog) | $${\color{red}✗}$$ |

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
