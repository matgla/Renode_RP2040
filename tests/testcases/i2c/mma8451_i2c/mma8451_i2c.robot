*** Settings ***

Resource        ../../../common.resource
Resource        ../../../sensors.resource
Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    60 seconds

*** Variables ***
${UART_TIMEOUT}              5
${ACCEL_TOLERANCE}           0.15    # Tolerance for acceleration values (m/s^2)

*** Test Cases ***
Run successfully 'mma8451_i2c' example with multiple accelerations
    Start MMA8451 Example
    
    # Test 1: Device at rest on flat surface (Z = 1g, X = Y = 0g)
    # 1g = 9.81 m/s^2
    MMA8451 Should Report Acceleration    0.0    0.0    9.81    ${ACCEL_TOLERANCE}
    
    # Test 2: Device tilted on X axis (X = 0.5g, Y = 0g, Z = 0.866g)
    Execute Command             sysbus.i2c0.mma8451 SetAcceleration 0.5 0.0 0.866
    MMA8451 Should Report Acceleration    4.905    0.0    8.495    ${ACCEL_TOLERANCE}
    
    # Test 3: Device tilted on Y axis (X = 0g, Y = 0.5g, Z = 0.866g)
    Execute Command             sysbus.i2c0.mma8451 SetAcceleration 0.0 0.5 0.866
    MMA8451 Should Report Acceleration    0.0    4.905    8.495    ${ACCEL_TOLERANCE}
    
    # Test 4: Device inverted (Z = -1g)
    Execute Command             sysbus.i2c0.mma8451 SetAcceleration 0.0 0.0 -1.0
    MMA8451 Should Report Acceleration    0.0    0.0    -9.81    ${ACCEL_TOLERANCE}
    
    # Test 5: Device on side (Y = 1g)
    Execute Command             sysbus.i2c0.mma8451 SetAcceleration 0.0 1.0 0.0
    MMA8451 Should Report Acceleration    0.0    9.81    0.0    ${ACCEL_TOLERANCE}

*** Keywords ***
Start MMA8451 Example
    Execute Command             include @${CURDIR}/mma8451_i2c.resc
    Create Terminal Tester      sysbus.uart0
    Wait For Line On Uart       Hello, MMA8451! Reading raw data from registers...    timeout=${UART_TIMEOUT}

MMA8451 Should Report Acceleration
    [Arguments]    ${expected_x}    ${expected_y}    ${expected_z}    ${tolerance}
    
    # Validate all three axes using the sensors library
    Uart Acceleration XYZ Should Be    ${expected_x}    ${expected_y}    ${expected_z}    ${tolerance}    ${UART_TIMEOUT}
