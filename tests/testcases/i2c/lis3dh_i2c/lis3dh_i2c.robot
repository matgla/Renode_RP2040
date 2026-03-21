*** Settings ***

Resource        ../../../common.resource
Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    60 seconds

*** Variables ***
${UART_TIMEOUT}              5
${TEMPERATURE_EXPECTED}      42.5
${TEMPERATURE_TOLERANCE}     0.1
${X_ACCELERATION_EXPECTED}   0.5
${Y_ACCELERATION_EXPECTED}   -0.25
${Z_ACCELERATION_EXPECTED}   0.75
${ACCELERATION_TOLERANCE}    0.01

*** Test Cases ***
Run successfully 'lis3dh_i2c' example
    Start LIS3DH Example
    LIS3DH Should Report Temperature    ${TEMPERATURE_EXPECTED}    ${TEMPERATURE_TOLERANCE}
    LIS3DH Should Report Acceleration   X acceleration:    ${X_ACCELERATION_EXPECTED}    ${ACCELERATION_TOLERANCE}
    LIS3DH Should Report Acceleration   Y acceleration:    ${Y_ACCELERATION_EXPECTED}    ${ACCELERATION_TOLERANCE}
    LIS3DH Should Report Acceleration   Z acceleration:    ${Z_ACCELERATION_EXPECTED}    ${ACCELERATION_TOLERANCE}

*** Keywords ***
Start LIS3DH Example
    Execute Command             include @${CURDIR}/lis3dh_i2c.resc
    Create Terminal Tester      sysbus.uart0
    Wait For Line On Uart       Hello, LIS3DH! Reading raw data from registers...    timeout=${UART_TIMEOUT}

LIS3DH Should Report Temperature
    [Arguments]    ${expected}    ${tolerance}
    Uart Numeric Value Should Be    TEMPERATURE:    ${expected}    ${tolerance}    ${UART_TIMEOUT}

LIS3DH Should Report Acceleration
    [Arguments]    ${label}    ${expected}    ${tolerance}
    Uart Numeric Value Should Be    ${label}    ${expected}    ${tolerance}    ${UART_TIMEOUT}
