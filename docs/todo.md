# RP2040 Simulation TODO

This document tracks missing features, unimplemented peripherals, and pico-examples tests that are not yet covered by the test suite.

## Unimplemented Peripherals

Peripherals marked as 🔴 **None** (not implemented) in the peripheral status table:

| Peripheral | Status | Notes |
|------------|--------|-------|
| **I2C** | 🟢 Implemented | Full implementation with master mode, interrupts, FIFO, DMA, and GPIO integration. Slave mode not yet implemented. |
| **USB** | 🔴 Stub Only | File exists (`emulation/peripherals/usb/rp2040_usb.cs`) but only 154 bytes - minimal stub. |
| **PWM** | 🔴 Missing | No directory or implementation files exist. |
| **RTC** | 🔴 Missing | No directory or implementation files exist. |

### Implementation Priority

1. **PWM** - High priority, needed for many hardware interfacing examples
2. **RTC** - Medium priority, needed for time-based applications
3. **USB** - Low priority, complex implementation, many examples depend on it

---

## Missing pico-examples Test Coverage

### Summary
- **Total pico-examples available**: ~125 examples
- **Testcases in repository**: 46 test directories
- **Tests in tests.yaml**: 43 tests (some testcases exist but aren't registered)

### Testcases Existing But Not Enabled in tests.yaml

These testcases have `.robot` files but are not registered in `tests/tests.yaml`:

| Testcase | Category | Notes |
|----------|----------|-------|
| `adc/adc_capture` | ADC | Test exists, needs to be added to tests.yaml |
| `i2c/pcf8523_i2c` | I2C | I2C not fully implemented, test may fail |
| `pio/pwm` | PIO | PWM via PIO - may work with PIO simulation |
| `pio/quadrature_encoder` | PIO | Quadrature encoder via PIO |

### pico-examples Without Tests (Categorized)

#### ADC (1 missing)
| Example | Notes |
|---------|-------|
| `adc/adc_capture` | ⚠️ Test exists but not in tests.yaml |

#### Binary Info (2 missing)
| Example | Notes |
|---------|-------|
| `binary_info/blink_any` | Binary metadata example |
| `binary_info/hello_anything` | Binary metadata example |

#### Bootloaders (2 missing)
| Example | Notes |
|---------|-------|
| `bootloaders/encrypted` | Encrypted bootloader |
| `bootloaders/uart` | UART bootloader |

#### DCP (1 missing)
| Example | Notes |
|---------|-------|
| `dcp/hello_dcp` | Data Copy Peripheral (RP2350?) |

#### DMA (1 missing)
| Example | Notes |
|---------|-------|
| `dma/channel_irq` | Channel IRQ handling |

#### Encrypted (1 missing)
| Example | Notes |
|---------|-------|
| `encrypted/hello_encrypted` | Encrypted application example |

#### Flash (3 missing)
| Example | Notes |
|---------|-------|
| `flash/cache_perfctr` | Cache performance counters |
| `flash/partition_info` | Flash partition info |
| `flash/runtime_flash_permissions` | Flash permissions |
| `flash/xip_stream` | XIP streaming |

#### FreeRTOS (1 missing)
| Example | Notes |
|---------|-------|
| `freertos/hello_freertos` | FreeRTOS integration |

#### GPIO (1 missing)
| Example | Notes |
|---------|-------|
| `gpio/dht_sensor` | DHT sensor (1-Wire protocol) |

#### Hello World (1 missing)
| Example | Notes |
|---------|-------|
| `hello_world/usb` | ❌ Requires USB peripheral |

#### HSTX (2 missing - RP2350 specific)
| Example | Notes |
|---------|-------|
| `hstx/dvi_out_hstx_encoder` | High-speed TX (RP2350) |
| `hstx/spi_lcd` | HSTX SPI LCD |

#### I2C (13 missing)
| Example | Notes |
|---------|-------|
| `i2c/bmp280_i2c` | ❌ Requires I2C peripheral |
| `i2c/bus_scan` | ❌ Requires I2C peripheral |
| `i2c/ht16k33_i2c` | ❌ Requires I2C peripheral |
| `i2c/lcd_1602_i2c` | ❌ Requires I2C peripheral |
| `i2c/lis3dh_i2c` | ❌ Requires I2C peripheral |
| `i2c/mcp9808_i2c` | ❌ Requires I2C peripheral |
| `i2c/mma8451_i2c` | ❌ Requires I2C peripheral |
| `i2c/mpl3115a2_i2c` | ❌ Requires I2C peripheral |
| `i2c/mpu6050_i2c` | ❌ Requires I2C peripheral |
| `i2c/pa1010d_i2c` | ❌ Requires I2C peripheral |
| `i2c/pcf8523_i2c` | ⚠️ Test exists but I2C not fully implemented |
| `i2c/slave_mem_i2c` | ❌ Requires I2C peripheral |
| `i2c/ssd1306_i2c` | ❌ Requires I2C peripheral |

#### Interp (1 missing)
| Example | Notes |
|---------|-------|
| `interp/hello_interp` | Interpolator/SIO feature |

#### Multicore (1 missing)
| Example | Notes |
|---------|-------|
| `multicore/multicore_doorbell` | Doorbell mechanism |

#### OTP (1 missing)
| Example | Notes |
|---------|-------|
| `otp/hello_otp` | One-Time Programmable memory |

#### Pico Board (2 missing)
| Example | Notes |
|---------|-------|
| `picoboard/blinky` | Pico board specific |
| `picoboard/button` | Pico board specific |

#### Pico W (2 missing)
| Example | Notes |
|---------|-------|
| `pico_w/bt` | Bluetooth (CYW43) |
| `pico_w/wifi` | WiFi (CYW43) |

#### PIO (17 missing)
| Example | Notes |
|---------|-------|
| `pio/apa102` | APA102 LED strip |
| `pio/hub75` | HUB75 LED matrix |
| `pio/i2c` | I2C via PIO |
| `pio/ir_nec` | IR NEC protocol |
| `pio/logic_analyser` | Logic analyzer |
| `pio/manchester_encoding` | Manchester encoding |
| `pio/onewire` | 1-Wire protocol |
| `pio/pwm` | ⚠️ Test exists, not in tests.yaml |
| `pio/quadrature_encoder` | ⚠️ Test exists, not in tests.yaml |
| `pio/quadrature_encoder_substep` | Quadrature encoder (substep) |
| `pio/spi` | SPI via PIO |
| `pio/squarewave` | Square wave generation |
| `pio/st7789_lcd` | ST7789 LCD driver |
| `pio/uart_dma` | UART via PIO with DMA |
| `pio/uart_rx` | UART RX via PIO |
| `pio/uart_tx` | UART TX via PIO |
| `pio/ws2812` | WS2812 LED strip |

#### PWM (3 missing)
| Example | Notes |
|---------|-------|
| `pwm/hello_pwm` | ❌ Requires PWM peripheral |
| `pwm/led_fade` | ❌ Requires PWM peripheral |
| `pwm/measure_duty_cycle` | ❌ Requires PWM peripheral |

#### Reset (1 missing)
| Example | Notes |
|---------|-------|
| `reset/hello_reset` | Reset reason detection |

#### RTC (3 missing)
| Example | Notes |
|---------|-------|
| `rtc/hello_rtc` | ❌ Requires RTC peripheral |
| `rtc/rtc_alarm` | ❌ Requires RTC peripheral |
| `rtc/rtc_alarm_repeat` | ❌ Requires RTC peripheral |

#### SHA (2 missing)
| Example | Notes |
|---------|-------|
| `sha/mbedtls_sha256` | SHA256 with mbedtls |
| `sha/sha256` | SHA256 hardware acceleration |

#### SPI (7 missing)
| Example | Notes |
|---------|-------|
| `spi/bme280_spi` | BME280 sensor via SPI |
| `spi/max7219_32x8_spi` | MAX7219 LED matrix |
| `spi/max7219_8x7seg_spi` | MAX7219 7-segment |
| `spi/mpu9250_spi` | MPU9250 sensor |
| `spi/spi_dma` | SPI with DMA |
| `spi/spi_flash` | SPI flash access |
| `spi/spi_master_slave` | SPI master/slave mode |

#### Status LED (2 missing)
| Example | Notes |
|---------|-------|
| `status_led/color_blink` | RGB status LED |
| `status_led/status_blink` | Status LED patterns |

#### System (4 missing)
| Example | Notes |
|---------|-------|
| `system/boot_info` | Boot information |
| `system/hello_double_tap` | Double-tap reset |
| `system/narrow_io_write` | Narrow IO write test |
| `system/rand` | Random number generation |
| `system/unique_board_id` | Board ID reading |

#### Timer (1 missing)
| Example | Notes |
|---------|-------|
| `timer/periodic_sampler` | Periodic sampling |

#### UART (3 missing)
| Example | Notes |
|---------|-------|
| `uart/hello_uart` | Basic UART (different from hello_world/serial) |
| `uart/lcd_uart` | UART LCD |
| `uart/uart_advanced` | Advanced UART features |

#### Universal (2 missing)
| Example | Notes |
|---------|-------|
| `universal/hello_universal` | Universal binary example |
| `universal/wrapper` | Universal wrapper |

#### USB (3 missing)
| Example | Notes |
|---------|-------|
| `usb/device` | ❌ Requires USB peripheral |
| `usb/dual` | ❌ Requires USB peripheral |
| `usb/host` | ❌ Requires USB peripheral |

---

## Blocked Tests (Dependencies on Unimplemented Peripherals)

Tests that cannot be implemented until the respective peripheral is working:

| Peripheral | Blocked Tests | Count |
|------------|---------------|-------|
| **USB** | `hello_world/usb`, `usb/*` | 4 |
| **PWM** | `pwm/*` (3 tests) | 3 |
| **RTC** | `rtc/*` (3 tests) | 3 |
| **I2C via PIO** | `pio/i2c` | 1 |

**Total blocked by missing peripherals: ~11 tests**

### Recently Unblocked (I2C Now Available)

The following I2C tests can now be implemented:
- `i2c/bus_scan`
- `i2c/bmp280_i2c`
- `i2c/lcd_1602_i2c`
- `i2c/mpu6050_i2c`
- `i2c/ssd1306_i2c`
- And 8 more I2C examples

---

## Quick Wins (Tests That Can Be Added Now)

These testcases already have files but just need to be added to `tests/tests.yaml`:

1. `adc/adc_capture` - ADC continuous capture mode
2. `pio/pwm` - PWM generation via PIO (should work)
3. `pio/quadrature_encoder` - Quadrature encoder via PIO

---

## Notes for Contributors

### Adding a New Peripheral

1. Create implementation in `emulation/peripherals/<name>/`
2. Add to `cores/rp2040.repl` with proper memory mapping
3. Add IRQ connections in the REPL file
4. Add DMA request connections if applicable
5. Create tests in `tests/testcases/<category>/<testname>/`
6. Register test in `tests/tests.yaml`

### Adding a New Test for Existing Peripheral

1. Create test directory: `tests/testcases/<category>/<testname>/`
2. Create `.resc` script to setup emulation
3. Create `.robot` file with test assertions
4. Add entry to `tests/tests.yaml`

---

*Last updated: 2026-03-18*
