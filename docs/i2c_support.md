# RP2040 I2C Peripheral Implementation

## Status: IMPLEMENTED (Beta)

The I2C peripheral has been fully implemented according to the RP2040 datasheet. The implementation supports master mode operation, interrupts, DMA, and connected I2C devices.

## Overview

This document provides a comprehensive implementation plan for the RP2040 I2C (Inter-Integrated Circuit) peripheral for the Renode simulation framework. The RP2040 has two I2C controllers (I2C0 and I2C1) that are compatible with the Synopsys DesignWare I2C (DW_apb_i2c) controller.

## Current State Analysis

### Existing Stub Implementation

The file `emulation/peripherals/i2c/rp2040_i2c.cs` contains a minimal stub:

- Basic register definitions (only IC_CON, IC_TAR, IC_SAR, IC_DATA_CMD, IC_SS_SCL_HCNT, IC_SS_SCL_LCNT, IC_FS_SCL_HCNT, IC_FS_SCL_LCNT)
- Empty implementations of II2CPeripheral interface methods (`ReadByte`, `WriteByte`, `Write`, `Read`, `FinishTransmission`)
- No interrupt support
- No FIFO implementation
- No DMA support
- No actual I2C protocol implementation

### Integration Points

1. **GPIO**: GPIO functions `I2C0_SDA`, `I2C0_SCL`, `I2C1_SDA`, `I2C1_SCL` already defined in `rp2040_gpio.cs`
2. **REPL**: I2C0 already registered at `0x40044000` in `rp2040.repl`
3. **Clocks**: Depends on `RP2040Clocks` for peripheral clock frequency
4. **IRQ**: Should connect to NVIC (IRQ numbers: 23 for I2C0, 24 for I2C1)
5. **DMA**: Should support DREQ signals for DMA transfers

## Implementation Phases

### Phase 1: Register Definitions and Basic Structure

**Goal**: Complete all register definitions from RP2040 datasheet

#### Registers to Implement

| Offset | Register | Description | Status |
|--------|----------|-------------|--------|
| 0x00 | IC_CON | Control Register | ✓ Exists |
| 0x04 | IC_TAR | Target Address | ✓ Exists |
| 0x08 | IC_SAR | Slave Address | ✓ Exists |
| 0x10 | IC_DATA_CMD | Data Buffer and Command | ✓ Exists |
| 0x14 | IC_SS_SCL_HCNT | Standard Speed SCL High Count | ✓ Exists |
| 0x18 | IC_SS_SCL_LCNT | Standard Speed SCL Low Count | ✓ Exists |
| 0x1C | IC_FS_SCL_HCNT | Fast Speed SCL High Count | ✓ Exists |
| 0x20 | IC_FS_SCL_LCNT | Fast Speed SCL Low Count | ✓ Exists |
| 0x2C | IC_INTR_STAT | Interrupt Status | **NEW** |
| 0x30 | IC_INTR_MASK | Interrupt Mask | **NEW** |
| 0x34 | IC_RAW_INTR_STAT | Raw Interrupt Status | **NEW** |
| 0x38 | IC_RX_TL | Receive FIFO Threshold | **NEW** |
| 0x3C | IC_TX_TL | Transmit FIFO Threshold | **NEW** |
| 0x40 | IC_CLR_INTR | Clear Combined and Individual Interrupts | **NEW** |
| 0x44 | IC_CLR_RX_UNDER | Clear RX_UNDER Interrupt | **NEW** |
| 0x48 | IC_CLR_RX_OVER | Clear RX_OVER Interrupt | **NEW** |
| 0x4C | IC_CLR_TX_OVER | Clear TX_OVER Interrupt | **NEW** |
| 0x50 | IC_CLR_RD_REQ | Clear RD_REQ Interrupt | **NEW** |
| 0x54 | IC_CLR_TX_ABRT | Clear TX_ABRT Interrupt | **NEW** |
| 0x58 | IC_CLR_RX_DONE | Clear RX_DONE Interrupt | **NEW** |
| 0x5C | IC_CLR_ACTIVITY | Clear ACTIVITY Interrupt | **NEW** |
| 0x60 | IC_CLR_STOP_DET | Clear STOP_DET Interrupt | **NEW** |
| 0x64 | IC_CLR_START_DET | Clear START_DET Interrupt | **NEW** |
| 0x68 | IC_CLR_GEN_CALL | Clear GEN_CALL Interrupt | **NEW** |
| 0x6C | IC_ENABLE | Enable Register | **NEW** |
| 0x70 | IC_STATUS | Status Register | **NEW** |
| 0x74 | IC_TXFLR | Transmit FIFO Level | **NEW** |
| 0x78 | IC_RXFLR | Receive FIFO Level | **NEW** |
| 0x7C | IC_SDA_HOLD | SDA Hold Time | **NEW** |
| 0x80 | IC_TX_ABRT_SOURCE | Transmit Abort Source | **NEW** |
| 0x88 | IC_DMA_CR | DMA Control | **NEW** |
| 0x8C | IC_DMA_TDLR | DMA Transmit Data Level | **NEW** |
| 0x90 | IC_DMA_RDLR | DMA Receive Data Level | **NEW** |
| 0xF4 | IC_COMP_PARAM_1 | Component Parameter | **NEW** |
| 0xF8 | IC_COMP_VERSION | Component Version | **NEW** |
| 0xFC | IC_COMP_TYPE | Component Type | **NEW** |

#### Key Bit Fields

**IC_CON (0x00)**:
- MASTER_MODE (bit 0): Enable master mode
- SPEED (bits 1-2): Speed mode (1=standard 100kHz, 2=fast 400kHz)
- IC_10BITADDR_SLAVE (bit 3): 10-bit addressing for slave mode
- IC_10BITADDR_MASTER (bit 4): 10-bit addressing for master mode
- IC_RESTART_EN (bit 5): Enable restart conditions
- IC_SLAVE_DISABLE (bit 6): Disable slave mode
- TX_EMPTY_CTRL (bit 8): TX_EMPTY interrupt behavior
- RX_FIFO_FULL_HLD_CTRL (bit 9): Hold bus when RX FIFO full

**IC_DATA_CMD (0x10)**:
- DAT (bits 0-7): Data byte
- CMD (bit 8): Command (0=write, 1=read)
- STOP (bit 9): Generate STOP after this byte
- RESTART (bit 10): Generate RESTART before this byte
- FIRST_DATA_BYTE (bit 11): First byte after address (read-only)

**IC_ENABLE (0x6C)**:
- ENABLE (bit 0): Enable I2C controller
- ABORT (bit 1): Abort current transfer

**IC_STATUS (0x70)**:
- ACTIVITY: Bus activity status
- TFNF: TX FIFO not full
- TFE: TX FIFO empty
- RFNE: RX FIFO not empty
- RFF: RX FIFO full
- MST_ACTIVITY: Master activity
- SLV_ACTIVITY: Slave activity

### Phase 2: FIFO Implementation

**Goal**: Implement TX and RX FIFOs for data buffering

#### Requirements

- FIFO depth: 16 entries (RP2040 specific)
- FIFO width: 8 bits for data + metadata for command bits
- Watermark/threshold support for interrupts and DMA

#### Implementation Structure

```csharp
private Queue<I2CDataCommand> txFifo;
private Queue<I2CDataCommand> rxFifo;
private const int FIFO_DEPTH = 16;

private class I2CDataCommand 
{
    public byte Data { get; set; }
    public bool IsRead { get; set; }  // CMD bit
    public bool Stop { get; set; }
    public bool Restart { get; set; }
}
```

#### Register Integration

- `IC_TXFLR` - Returns current TX FIFO level (0-16)
- `IC_RXFLR` - Returns current RX FIFO level (0-16)
- `IC_RX_TL` - Threshold for RX FIFO full interrupt (RX_FULL triggered when RX FIFO >= threshold)
- `IC_TX_TL` - Threshold for TX FIFO empty interrupt (TX_EMPTY triggered when TX FIFO <= threshold)

### Phase 3: Interrupt System

**Goal**: Implement complete interrupt generation logic

#### Interrupt Sources (IC_INTR_STAT)

| Bit | Name | Description |
|-----|------|-------------|
| 0 | RX_UNDER | RX FIFO underflow |
| 1 | RX_OVER | RX FIFO overflow |
| 2 | RX_FULL | RX FIFO full |
| 3 | TX_OVER | TX FIFO overflow |
| 4 | TX_EMPTY | TX FIFO empty |
| 5 | RD_REQ | Read request (slave mode) |
| 6 | TX_ABRT | Transmit abort |
| 7 | RX_DONE | RX done (slave mode) |
| 8 | ACTIVITY | Bus activity detected |
| 9 | STOP_DET | STOP condition detected |
| 10 | START_DET | START condition detected |
| 11 | GEN_CALL | General call received |
| 12 | RESTART_DET | RESTART condition detected |
| 13 | MST_ON_HOLD | Master on hold |

#### Implementation Approach

1. Create `GPIO IRQ` property for connecting to NVIC
2. Implement mask logic (IC_INTR_MASK)
3. Implement status registers (IC_RAW_INTR_STAT, IC_INTR_STAT)
4. Implement clear registers (write-to-clear)
5. Add helper methods to trigger specific interrupts

#### IRQ Connection in REPL

```
i2c0:
    IRQ -> nvic0@23

i2c1:
    IRQ -> nvic0@24
```

### Phase 4: Master Mode Implementation

**Goal**: Implement I2C master mode protocol

#### State Machine

```
IDLE -> START -> ADDRESS -> DATA -> STOP -> IDLE
                |           |
                v           v
            (wait for ACK) (repeat for each byte)
```

#### Key Behaviors

1. **START Generation**: When first command written to TX FIFO and bus is idle
2. **Address Phase**: Send 7-bit or 10-bit address based on IC_TAR configuration
3. **Write Operation** (CMD=0): 
   - Shift out data bits on SDA, clock on SCL
   - Read ACK from slave after each byte
   - Generate STOP if STOP bit set in command
4. **Read Operation** (CMD=1):
   - Send address with R/W=1
   - Shift in data bits from SDA
   - Send ACK/NACK based on command bits
   - Generate STOP if STOP bit set
5. **Clock Stretching**: Slave can hold SCL low to pause transfer

#### Timing

- Use `IManagedThread` for bit-banged I2C clock generation (like SPI implementation)
- Frequency calculated from peripheral clock and HCNT/LCNT registers:
  - SCL high period = (IC_xS_SCL_HCNT + 1) / peripheral_clock
  - SCL low period = (IC_xS_SCL_LCNT + 1) / peripheral_clock
- Standard mode: 100 kHz
- Fast mode: 400 kHz

#### Implementation

```csharp
private IManagedThread i2cThread;
private I2CState currentState;
private int bitCounter;
private byte currentByte;
private bool awaitingAck;

private enum I2CState 
{
    Idle,
    GeneratingStart,
    SendingAddress,
    DataWrite,
    DataRead,
    GeneratingStop,
    WaitForAck,
    ClockStretch
}

private void I2CStep() 
{
    switch (currentState) 
    {
        case I2CState.Idle:
            if (txFifo.Count > 0 && i2cEnabled.Value) 
            {
                GenerateStart();
                currentState = I2CState.GeneratingStart;
            }
            break;
        case I2CState.GeneratingStart:
            // SDA goes low while SCL is high
            // Then SCL goes low
            // Transition to address phase
            break;
        case I2CState.SendingAddress:
            // Send 7-bit address + R/W bit
            break;
        case I2CState.DataWrite:
            // Shift out bits on SDA, toggle SCL
            break;
        case I2CState.DataRead:
            // Shift in bits from SDA, toggle SCL
            break;
        case I2CState.WaitForAck:
            // Read ACK from slave
            break;
        case I2CState.GeneratingStop:
            // SDA goes high while SCL is high
            break;
    }
}
```

### Phase 5: GPIO Integration

**Goal**: Connect I2C peripheral to GPIO pins for SDA/SCL

#### GPIO Function Handling

1. Subscribe to GPIO function changes (like SPI does)
2. Track which pins are assigned to I2C0/I2C1 SDA/SCL
3. Implement open-drain output behavior (I2C requirement)
4. Implement SDA/SCL line reading

#### Implementation

```csharp
private List<int> sdaPins;
private List<int> sclPins;

private void OnGpioFunctionChange(int pin, RP2040GPIO.GpioFunction function) 
{
    if (id == 0) 
    {
        switch (function) 
        {
            case RP2040GPIO.GpioFunction.I2C0_SDA:
                sdaPins.Add(pin);
                return;
            case RP2040GPIO.GpioFunction.I2C0_SCL:
                sclPins.Add(pin);
                return;
            case RP2040GPIO.GpioFunction.NONE:
                sdaPins.Remove(pin);
                sclPins.Remove(pin);
                return;
        }
    }
    else if (id == 1)
    {
        // Similar for I2C1
    }
}

// Open-drain: Drive low for 0, high-impedance (input) for 1
private void SetSda(bool value) 
{
    foreach (var pin in sdaPins) 
    {
        if (value) 
        {
            // Set as input (high-impedance, pulled up externally)
            gpio.SetPinAsInput(pin);
        } 
        else 
        {
            // Drive low
            gpio.WritePin(pin, false);
        }
    }
}

private void SetScl(bool value) 
{
    foreach (var pin in sclPins) 
    {
        if (value) 
        {
            gpio.SetPinAsInput(pin);
        } 
        else 
        {
            gpio.WritePin(pin, false);
        }
    }
}

private bool ReadSda()
{
    if (sdaPins.Count == 0) return true; // Pull-up default
    return gpio.GetGpioState((uint)sdaPins[0]);
}

private bool ReadScl()
{
    if (sclPins.Count == 0) return true;
    return gpio.GetGpioState((uint)sclPins[0]);
}
```

### Phase 6: Slave Mode Implementation (Future Phase)

**Goal**: Support I2C slave mode for multi-master scenarios

#### Requirements

- Respond to own address (IC_SAR)
- Handle general call address (0x00)
- Support clock stretching
- Generate RD_REQ interrupt when master wants to read

**Note**: Slave mode is lower priority since most pico-examples use master mode.

### Phase 7: DMA Support

**Goal**: Implement DREQ signals for DMA transfers

#### DMA Control Register (IC_DMA_CR)

- TDMAE (bit 1): Transmit DMA enable
- RDMAE (bit 0): Receive DMA enable

#### DREQ Generation

- Transmit DREQ: Trigger when TX FIFO level below IC_DMA_TDLR threshold
- Receive DREQ: Trigger when RX FIFO level above IC_DMA_RDLR threshold

#### Implementation

```csharp
public GPIO DmaTransmitRequest { get; }
public GPIO DmaReceiveRequest { get; }

private void UpdateDreqSignals() 
{
    if (dmaTxEnable.Value && txFifo.Count <= dmaTxThreshold.Value) 
    {
        DmaTransmitRequest.Set();
    } 
    else 
    {
        DmaTransmitRequest.Unset();
    }
    
    if (dmaRxEnable.Value && rxFifo.Count > dmaRxThreshold.Value) 
    {
        DmaReceiveRequest.Set();
    } 
    else 
    {
        DmaReceiveRequest.Unset();
    }
}
```

#### REPL Connection

```
i2c0:
    DmaTransmitRequest -> dma@?
    DmaReceiveRequest -> dma@?
```

### Phase 8: Connected I2C Device Support

**Goal**: Enable connection to simulated I2C peripherals

#### II2CPeripheral Interface

The class already implements `II2CPeripheral` interface. Need to implement:

- `Write(byte[] data)` - Handle data from master
- `Read(int count)` - Provide data to master
- `WriteByte(long offset, byte value)` - Not typically used for I2C
- `FinishTransmission()` - Called when STOP or repeated START detected

#### SimpleContainer Base Class

Already extends `SimpleContainer<II2CPeripheral>` for managing connected devices.

#### Connected Device Flow

```csharp
public void Write(byte[] data)
{
    // Called when master sends data to a slave device
    // This is actually handled by the slave device, not the I2C controller
}

public byte[] Read(int count = 1)
{
    // Called when master reads data from a slave device
    // This is handled by the slave device
    return new byte[0];
}

public void FinishTransmission()
{
    // Called when STOP condition detected
}
```

### Phase 9: Testing and Validation

#### Unit Tests

1. Register read/write tests
2. FIFO operation tests
3. Interrupt generation tests
4. Master mode write sequence
5. Master mode read sequence

#### Integration Tests (pico-examples)

| Test | Description | Priority |
|------|-------------|----------|
| i2c/bus_scan | Scan for devices on bus | High |
| i2c/bmp280_i2c | Barometric pressure sensor | High |
| i2c/lcd_1602_i2c | LCD display | Medium |
| i2c/mpu6050_i2c | IMU sensor | Medium |
| i2c/ssd1306_i2c | OLED display | Medium |

#### REPL Test Configuration Example

```repl
// raspberry_pico_with_i2c_devices.repl
i2c0: I2C.RP2040I2C @ sysbus 0x40044000 {
    clocks: clocks
}

bmp280: Sensors.BMP280 @ i2c0 0x76

i2c0:
    IRQ -> nvic0@23
```

## Technical Design Decisions

### 1. Threading Model

**Decision**: Use `IManagedThread` for I2C clock generation, similar to SPI implementation.

**Rationale**:
- I2C is a slow protocol (100-400 kHz) compared to system clock (125 MHz)
- Need precise timing for SCL generation
- Must coordinate with GPIO for open-drain behavior

**Alternative**: Event-driven approach with delays - rejected due to complexity with multi-byte transfers.

### 2. Open-Drain Implementation

**Decision**: Use GPIO input mode for '1' (high-impedance), output low for '0'.

**Rationale**:
- True open-drain behavior requires external pull-ups
- Simulation can approximate by setting pin as input (high-impedance)
- Matches real hardware where SDA/SCL are never actively driven high

### 3. FIFO Depth

**Decision**: Fixed 16-entry FIFO (RP2040 specific).

**Rationale**:
- RP2040 datasheet specifies 16-entry FIFO
- Some variants of DW_apb_i2c have configurable depth
- Simplifies implementation

### 4. 10-bit Address Support

**Decision**: Implement in Phase 2 (after 7-bit address working).

**Rationale**:
- Most devices use 7-bit addressing
- 10-bit adds complexity to state machine
- Can be added later without breaking existing functionality

### 5. Clock Synchronization

**Decision**: Simulate clock stretching by checking SCL state before proceeding.

**Rationale**:
- Allows slave devices to pause communication
- Essential for proper I2C protocol compliance

## Implementation Checklist

### Phase 1: Registers
- [ ] Define all register enums
- [ ] Implement IC_ENABLE register
- [ ] Implement IC_STATUS register  
- [ ] Implement interrupt registers (IC_INTR_STAT, IC_INTR_MASK, IC_RAW_INTR_STAT)
- [ ] Implement FIFO level registers (IC_TXFLR, IC_RXFLR, IC_RX_TL, IC_TX_TL)
- [ ] Implement clear interrupt registers
- [ ] Implement IC_TX_ABRT_SOURCE
- [ ] Implement IC_DMA_CR, IC_DMA_TDLR, IC_DMA_RDLR
- [ ] Implement component ID registers

### Phase 2: FIFO
- [ ] Create I2CDataCommand class
- [ ] Implement TX FIFO (16 entries)
- [ ] Implement RX FIFO (16 entries)
- [ ] Implement FIFO watermark logic
- [ ] Handle FIFO overflow/underflow conditions

### Phase 3: Interrupts
- [ ] Create IRQ GPIO property
- [ ] Implement interrupt masking logic
- [ ] Implement TX_EMPTY interrupt
- [ ] Implement RX_FULL interrupt
- [ ] Implement ERROR interrupts (TX_ABRT, RX_OVER, TX_OVER)
- [ ] Implement BUS interrupts (START_DET, STOP_DET)

### Phase 4: Master Mode
- [ ] Create I2C state machine
- [ ] Implement START/STOP generation
- [ ] Implement 7-bit address transmission
- [ ] Implement write operation
- [ ] Implement read operation
- [ ] Implement ACK/NACK handling
- [ ] Implement clock generation from HCNT/LCNT
- [ ] Create IManagedThread for I2C execution

### Phase 5: GPIO
- [ ] Subscribe to GPIO function changes
- [ ] Track SDA/SCL pin assignments
- [ ] Implement open-drain output
- [ ] Implement SDA/SCL reading
- [ ] Handle START/STOP detection

### Phase 6: DMA
- [ ] Create DREQ GPIO properties
- [ ] Implement IC_DMA_CR register
- [ ] Implement transmit DREQ logic
- [ ] Implement receive DREQ logic
- [ ] Connect DREQ to DMA controller

### Phase 7: Connected Devices
- [ ] Implement II2CPeripheral.Write
- [ ] Implement II2CPeripheral.Read
- [ ] Implement FinishTransmission
- [ ] Test with BMP280 sensor model

### Phase 8: REPL/Integration
- [ ] Update rp2040.repl with IRQ connections
- [ ] Add DREQ connections
- [ ] Create test platform description
- [ ] Verify both I2C0 and I2C1 work

### Phase 9: Testing
- [ ] Unit test: Register access
- [ ] Unit test: FIFO operations
- [ ] Unit test: Interrupt generation
- [ ] Integration test: bus_scan example
- [ ] Integration test: bmp280_i2c example
- [ ] Integration test: lcd_1602_i2c example

## Files to Modify/Create

### Modified Files

1. `emulation/peripherals/i2c/rp2040_i2c.cs` - Main implementation
2. `cores/rp2040.repl` - Add IRQ and DREQ connections
3. `tests/tests.yaml` - Add I2C tests

### New Files

1. `tests/testcases/i2c/bus_scan/` - Bus scan test
2. `tests/testcases/i2c/bmp280_i2c/` - BMP280 sensor test
3. `boards/raspberry_pico_with_i2c_devices.repl` - Test platform

## References

1. **RP2040 Datasheet**: Chapter 4.3 - I2C Controller
   - Register descriptions
   - Timing requirements
   - FIFO behavior

2. **DW_apb_i2c Datasheet**: Synopsys DesignWare I2C specification
   - Complete register bit definitions
   - State machine details
   - Interrupt behavior

3. **I2C Specification**: NXP I2C-bus specification (UM10204)
   - START/STOP condition timing
   - ACK/NACK protocol
   - Clock stretching

4. **Renode Documentation**: Co-simulation and peripheral development
   - `IManagedThread` usage
   - GPIO integration patterns
   - DMA request generation

5. **Pico SDK**: pico-sdk/src/rp2_common/hardware_i2c
   - SDK usage patterns
   - Example code

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| Open-drain GPIO behavior incorrect | High | Test with actual I2C device models |
| Timing issues with fast mode (400kHz) | Medium | Use managed thread with accurate frequency |
| Slave mode complexity | Medium | Defer to Phase 2 |
| DMA integration issues | Low | Follow UART DMA implementation pattern |
| Multiple I2C instances conflict | Low | Test both I2C0 and I2C1 simultaneously |

## Success Criteria

1. All pico-examples I2C tests pass
2. Bus scan detects connected devices correctly
3. BMP280 sensor reads temperature/pressure accurately
4. LCD display shows correct characters
5. Interrupts fire at correct times
6. DMA transfers complete without errors
7. Both I2C0 and I2C1 work independently

## Timeline Estimate

| Phase | Effort |
|-------|--------|
| Phase 1 (Registers) | 2 days |
| Phase 2 (FIFO) | 1 day |
| Phase 3 (Interrupts) | 2 days |
| Phase 4 (Master Mode) | 5 days |
| Phase 5 (GPIO) | 2 days |
| Phase 6 (DMA) | 2 days |
| Phase 7 (Devices) | 2 days |
| Phase 8 (Testing) | 3 days |
| **Total** | **~3 weeks** |

## Appendix: Register Reset Values

| Register | Reset Value | Description |
|----------|-------------|-------------|
| IC_CON | 0x0000007D | Master mode, fast speed, slave disabled |
| IC_TAR | 0x00000055 | Default target address |
| IC_SAR | 0x00000055 | Default slave address |
| IC_DATA_CMD | 0x00000000 | Empty data, write command |
| IC_SS_SCL_HCNT | 0x00000028 | Standard mode high count |
| IC_SS_SCL_LCNT | 0x0000002F | Standard mode low count |
| IC_FS_SCL_HCNT | 0x00000006 | Fast mode high count |
| IC_FS_SCL_LCNT | 0x0000000D | Fast mode low count |
| IC_INTR_MASK | 0x000008FF | All interrupts masked except STOP_DET |
| IC_RX_TL | 0x00000000 | RX threshold = 0 |
| IC_TX_TL | 0x00000000 | TX threshold = 0 |
| IC_ENABLE | 0x00000000 | I2C disabled |
| IC_DMA_CR | 0x00000000 | DMA disabled |
| IC_COMP_PARAM_1 | 0x00030206 | FIFO depth = 16, max speed = fast |
| IC_COMP_VERSION | 0x00000000 | Version (implementation specific) |
| IC_COMP_TYPE | 0x44570140 | "DW I2C" type |

## Appendix: Interrupt Mapping

| Interrupt | NVIC IRQ | Description |
|-----------|----------|-------------|
| I2C0 | 23 | I2C0 combined interrupt |
| I2C1 | 24 | I2C1 combined interrupt |

## Appendix: GPIO Pin Mapping

I2C pins are available on multiple GPIO pins:

| GPIO | Function | I2C Instance |
|------|----------|--------------|
| 0 | I2C0_SDA | I2C0 |
| 1 | I2C0_SCL | I2C0 |
| 2 | I2C1_SDA | I2C1 |
| 3 | I2C1_SCL | I2C1 |
| 4 | I2C0_SDA | I2C0 |
| 5 | I2C0_SCL | I2C0 |
| 6 | I2C1_SDA | I2C1 |
| 7 | I2C1_SCL | I2C1 |
| ... | ... | ... |

All even GPIOs (0, 4, 8, ...) support I2C0_SDA
All odd GPIOs (1, 5, 9, ...) support I2C0_SCL

---

*This document is a living specification. Updates should be made as implementation progresses.*
