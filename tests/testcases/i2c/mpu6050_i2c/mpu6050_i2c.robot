*** Settings ***

Resource        ../../../common.resource
Resource        ../../../sensors.resource
Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    60 seconds

*** Variables ***
${UART_TIMEOUT}              5
${ACCEL_TOLERANCE}           100    # Tolerance for accelerometer raw values
${GYRO_TOLERANCE}            10     # Tolerance for gyroscope raw values
${TEMP_TOLERANCE}            0.5    # Tolerance for temperature values (Celsius)

*** Test Cases ***
Run successfully 'mpu6050_i2c' example with multiple readings
    Start MPU6050 Example
    
    # Test 1: Device at rest (Z = 1g = ~16384, gyros = 0, temp = 25C)
    MPU6050 Should Report Acceleration    0    0    16384    ${ACCEL_TOLERANCE}
    MPU6050 Should Report Gyro    0    0    0    ${GYRO_TOLERANCE}
    MPU6050 Temperature Should Be    25.0    ${TEMP_TOLERANCE}
    
    # Test 2: Device tilted (X = 0.5g, Y = 0.5g, Z = 0.707g)
    Execute Command             sysbus.i2c0.mpu6050 SetAcceleration 8192 8192 11585
    Execute Command             sysbus.i2c0.mpu6050 SetGyro 100 -50 25
    Execute Command             sysbus.i2c0.mpu6050 SetTemperature 30.0
    MPU6050 Should Report Acceleration    8192    8192    11585    ${ACCEL_TOLERANCE}
    MPU6050 Should Report Gyro    100    -50    25    ${GYRO_TOLERANCE}
    MPU6050 Temperature Should Be    30.0    ${TEMP_TOLERANCE}
    
    # Test 3: Device inverted (Z = -1g)
    Execute Command             sysbus.i2c0.mpu6050 SetAcceleration 0 0 -16384
    Execute Command             sysbus.i2c0.mpu6050 SetGyro 0 0 0
    Execute Command             sysbus.i2c0.mpu6050 SetTemperature 20.0
    MPU6050 Should Report Acceleration    0    0    -16384    ${ACCEL_TOLERANCE}
    MPU6050 Should Report Gyro    0    0    0    ${GYRO_TOLERANCE}
    MPU6050 Temperature Should Be    20.0    ${TEMP_TOLERANCE}

*** Keywords ***
Start MPU6050 Example
    Execute Command             include @${CURDIR}/mpu6050_i2c.resc
    Create Terminal Tester      sysbus.uart0
    Wait For Line On Uart       Hello, MPU6050! Reading raw data from registers...    timeout=${UART_TIMEOUT}

MPU6050 Should Report Acceleration
    [Arguments]    ${expected_x}    ${expected_y}    ${expected_z}    ${tolerance}
    
    # Read the acceleration line: "Acc. X = %d, Y = %d, Z = %d"
    ${line}=    Wait For Line On Uart    Acc. X =    timeout=${UART_TIMEOUT}
    ${text}=    Set Variable    ${line['Line']}
    
    # Extract and validate each axis using sensors library helpers
    ${x_value}=    Extract Numeric Value After Prefix    ${text}    Acc. X
    Should Be Equal As Numbers With Tolerance    ${x_value}    ${expected_x}    ${tolerance}
    
    ${y_value}=    Extract Numeric Value After Prefix    ${text}    Y
    Should Be Equal As Numbers With Tolerance    ${y_value}    ${expected_y}    ${tolerance}
    
    ${z_value}=    Extract Numeric Value After Prefix    ${text}    Z
    Should Be Equal As Numbers With Tolerance    ${z_value}    ${expected_z}    ${tolerance}

MPU6050 Should Report Gyro
    [Arguments]    ${expected_x}    ${expected_y}    ${expected_z}    ${tolerance}
    
    # Read the gyro line: "Gyro. X = %d, Y = %d, Z = %d"
    ${line}=    Wait For Line On Uart    Gyro. X =    timeout=${UART_TIMEOUT}
    ${text}=    Set Variable    ${line['Line']}
    
    # Extract and validate each axis
    ${x_value}=    Extract Numeric Value After Prefix    ${text}    Gyro. X
    Should Be Equal As Numbers With Tolerance    ${x_value}    ${expected_x}    ${tolerance}
    
    ${y_value}=    Extract Numeric Value After Prefix    ${text}    Y
    Should Be Equal As Numbers With Tolerance    ${y_value}    ${expected_y}    ${tolerance}
    
    ${z_value}=    Extract Numeric Value After Prefix    ${text}    Z
    Should Be Equal As Numbers With Tolerance    ${z_value}    ${expected_z}    ${tolerance}

MPU6050 Temperature Should Be
    [Arguments]    ${expected}    ${tolerance}
    
    # Read the temperature line: "Temp. = %f"
    ${line}=    Wait For Line On Uart    Temp. =    timeout=${UART_TIMEOUT}
    ${text}=    Set Variable    ${line['Line']}
    
    # Extract and validate temperature
    ${value}=    Extract Numeric Value After Prefix    ${text}    Temp.
    Should Be Equal As Numbers With Tolerance    ${value}    ${expected}    ${tolerance}
