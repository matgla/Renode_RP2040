# Renode RP2040 Simulation - Agent Guide

## Project Overview

This repository contains a **RP2040 MCU simulation** for the [Renode](https://github.com/renode/renode) emulation framework. It provides a complete simulation of the Raspberry Pi RP2040 microcontroller, including support for various peripherals, multicore execution, and PIO (Programmable I/O) simulation.

**Project Status**: Work in progress (WIP) and currently frozen due to lack of time. Some peripherals may contain bugs or incomplete implementations.

### Supported Peripherals

| Peripheral | Status | Notes |
|------------|--------|-------|
| **SIO** | 🟡 Partial | Multicore, dividers partially supported |
| **IRQ** | 🟡 Partial | Propagation from some peripherals implemented |
| **DMA** | 🟢 Full | Ringing and control blocks supported |
| **Clocks** | 🟡 Partial | Mostly stubs with tree propagation |
| **GPIO** | 🟢 Full | Pins manipulation with interrupts |
| **XOSC** | 🟢 Full | Crystal oscillator |
| **ROSC** | 🟢 Full | Ring oscillator |
| **PLL** | 🟢 Full | System and USB PLL |
| **PIO** | 🟡 Partial | External C++ simulator, manual sync needed |
| **UART** | 🟢 Full | PL011-based with DREQ generation |
| **SPI** | 🟡 Partial | Master mode only, clock config not supported |
| **Timers** | 🟡 Partial | Alarms implemented |
| **Watchdog** | 🟢 Full | With LimitTimer tick generator |
| **ADC** | 🟡 Partial | Implemented, RESD files not verified |
| **SSI/XIP** | 🟡 Partial | XIP support/caches stubbed |
| **Resets** | 🟢 Full | Device resetting works |
| **I2C** | 🟢 Full | Master mode, interrupts, DMA support |
| **USB** | 🔴 None | Not implemented |
| **PWM** | 🔴 None | Not implemented |
| **RTC** | 🔴 None | Not implemented |

## Technology Stack

- **Primary Language**: C# (for Renode peripherals)
- **PIO Simulator**: C++ (external library)
- **Testing**: Robot Framework + Python
- **Build System**: .NET SDK (for C#), CMake (for PIO simulator)
- **Visualization**: Python (websockets, aiohttp)

## Project Structure

```
.
├── boards/                    # Board configuration files
│   ├── raspberry_pico.repl   # Raspberry Pi Pico board definition
│   ├── initialize_raspberry_pico.resc  # Initialization script
│   └── initialize_custom_board.resc    # Custom board template
├── bootroms/                  # RP2040 bootrom binaries
│   └── rp2040/
├── cores/                     # MCU core definitions
│   ├── rp2040.repl           # RP2040 peripheral description
│   └── initialize_peripherals.resc     # Peripheral loading script
├── emulation/                 # C# peripheral implementations
│   ├── peripherals/          # Main peripheral implementations
│   │   ├── adc/              # ADC peripheral
│   │   ├── clocks/           # Clock system (XOSC, ROSC, PLL)
│   │   ├── dma/              # DMA controller
│   │   ├── gpio/             # GPIO and pads
│   │   ├── pio/              # PIO integration
│   │   ├── spi/              # SPI controllers
│   │   ├── timer/            # Timers and watchdog
│   │   ├── uart/             # UART
│   │   └── ...
│   ├── externals/            # External device implementations
│   ├── Peripherals.csproj    # .NET project file
│   └── emulation.sln         # Visual Studio solution
├── piosim/                    # PIO simulator (C++ shared library)
│   ├── libpiosim.so          # Linux shared library
│   ├── libpiosim.dll         # Windows DLL
│   ├── libpiosim.dylib       # macOS library
│   ├── fetch_piosim.py       # Download script for libraries
│   └── version               # Version specifier
├── tests/                     # Test suite
│   ├── testcases/            # Robot Framework test files
│   ├── pico-examples/        # Pico SDK examples (cloned)
│   ├── pico_examples_patches/# Patches for pico-examples
│   ├── build_pico_examples.sh# Build script for examples
│   ├── run_tests.py          # Parallel test runner
│   ├── tests.yaml            # Test list
│   └── requirements.txt      # Python dependencies
├── visualization/             # Web-based board visualization
│   ├── visualization.py      # Renode integration script
│   ├── visualization_server.py
│   └── requirements.txt
├── images/                    # Documentation images
├── logs/                      # Log output directory
├── snapshots/                 # Emulation snapshots
├── run_firmware.resc         # Quick firmware execution script
└── setup_venv.sh             # Virtual environment setup
```

## Build and Test Commands

### Prerequisites

- **Renode Version**: 1.15.3 (highly coupled, use exactly this version)
- **.NET SDK**: For building C# peripherals (optional for normal use)
- **Python 3**: For tests and visualization
- **CMake + GCC**: For building PIO simulator from source (optional)

### Setup

```bash
# Setup Python virtual environment and install dependencies
./setup_venv.sh

# Or manually:
python3 -m venv venv
source venv/bin/activate
pip install -r tests/requirements.txt
```

### Running Tests

```bash
# Run all tests (builds pico-examples and runs test suite)
./run_tests.sh

# Run with custom renode-test path
python3 tests/run_tests.py -r 3 -f tests/tests.yaml -e /path/to/renode-test

# Run with specific thread count
python3 tests/run_tests.py -j 4
```

### Using the Simulation

```bash
# Run with specific firmware
renode --console run_firmware.resc

# Inside Renode console:
(monitor) $global.FIRMWARE=your_firmware.elf
(monitor) include @run_firmware.resc

# Or for Raspberry Pico specifically:
(monitor) path add @/path/to/Renode_RP2040
(monitor) include @boards/initialize_raspberry_pico.resc
(monitor) sysbus LoadELF @your_firmware.elf
(monitor) showAnalyzer sysbus.uart0
(monitor) start
```

## Code Style Guidelines

### C# Peripherals

1. **File Header**: All files must include the standard MIT license header:
```csharp
/**
 * filename.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */
```

2. **Namespace**: Use `Antmicro.Renode.Peripherals` namespace
3. **Base Classes**: Inherit from `RP2040PeripheralBase` for RP2040-specific peripherals
4. **Register Access**: Implement XOR/SET/CLEAR aliases using `ConnectionRegion` attributes
5. **Interface Implementation**: Implement `IRP2040Peripheral` for standard RP2040 register behavior

### RESC Scripts (Renode Commands)

- Use `$ORIGIN` for relative paths within scripts
- Set default values with `?=` operator: `$variable?="default"`
- Use `$global.` prefix for global variables that should persist

### REPL Files (Platform Description)

- Reference other files with `using "path/to/file.repl"`
- Register peripherals at specific bus addresses using `@ sysbus 0xADDRESS`
- Connect IRQs with `->` syntax: `IRQ -> nvic0@IRQ_NUMBER`

## Testing Instructions

### Test Structure

Tests use **Robot Framework** with custom Renode keywords:

```robot
*** Settings ***
Suite Setup     Setup
Suite Teardown  Teardown
Test Timeout    270 seconds

*** Test Cases ***
Run successfully 'example' example
    Execute Command             include @${CURDIR}/example.resc
    Create LED Tester           sysbus.gpio.led
    Assert LED Is Blinking      testDuration=2  onDuration=0.25  tolerance=0.05
```

### Creating New Tests

1. Create a `.resc` script in `tests/testcases/<category>/<testname>/`
2. Create a `.robot` file with the same base name
3. Add the test to `tests/tests.yaml`
4. If using pico-examples, add patches to `tests/pico_examples_patches/`

### Test Resources

- `tests/common.resource` - Shared keywords
- `tests/prepare.resc` - Common test initialization
- `tests/testers/load_testers.resc` - LED tester and other utilities

## Key Architecture Details

### PIO Simulation

PIO is implemented as an **external C++ simulator** (`piosim`) due to performance issues with C# implementation. PIO is modeled as an additional CPU.

- **Synchronization**: Renode executes multiple steps at once per CPU, so manual synchronization is necessary for PIO interworking
- **Download**: Use `piosim/fetch_piosim.py` to download pre-built libraries
- **Build from source**: Requires MinGW-GCC on Windows (MSYS environment), standard GCC on Linux/macOS

### Peripheral Implementation Pattern

```csharp
public class RP2040XXX : RP2040PeripheralBase, IRP2040Peripheral
{
    public RP2040XXX(IMachine machine, ulong address) : base(machine, address)
    {
        registers = CreateRegisters();
        // Register XOR/SET/CLEAR aliases are handled by base class
    }
    
    private DoubleWordRegisterCollection CreateRegisters()
    {
        var registersMap = new Dictionary<long, DoubleWordRegister>();
        // Define registers here
        return new DoubleWordRegisterCollection(this, registersMap);
    }
    
    public override void Reset()
    {
        base.Reset();
        // Custom reset logic
    }
}
```

### Emulation Accuracy

Renode uses optimizations that execute large numbers of instructions at once per core. This can cause accuracy issues for timing-sensitive scenarios. To improve accuracy:

```renode
emulation SetGlobalQuantum "0.000001"
```

## Security Considerations

1. **PIO Simulator**: Downloads binary libraries from GitHub releases. Verify checksums if security is critical.
2. **ELF Loading**: Simulation loads untrusted ELF files - ensure source is trusted
3. **Network**: Visualization server opens a local web server - bind to localhost only

## Common Issues

1. **Segmentation faults on Windows**: Ensure `piosim.dll` is compiled in MSYS environment with matching compiler
2. **IronPython errors with .NET version**: Use mono version of Renode on Linux
3. **PIO sync issues**: Manual reevaluation may be needed - look at SPI/PIO interworking examples
4. **7-segment display rendering**: Sometimes requires refresh/zoom in visualization

## Development Workflow

1. Modify C# peripherals in `emulation/peripherals/`
2. Build with `dotnet build emulation/Peripherals.csproj` (optional for testing)
3. Add tests in `tests/testcases/`
4. Run `./run_tests.sh` to verify
5. Update README.md peripheral status table if needed

## License

MIT License - Copyright (c) 2024 Mateusz Stadnik
