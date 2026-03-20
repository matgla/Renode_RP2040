*** Settings ***

Resource        ../../../common.resource
Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    60 seconds

*** Test Cases ***
Run successfully 'bmp280_i2c' example
    Execute Command             include @${CURDIR}/bmp280_i2c.resc

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Hello, BMP280! Reading temperaure and pressure values from sensor...    timeout=5

    Uart Numeric Value Should Be    Pressure =    101.325    0.01    5

    Uart Numeric Value Should Be    Temp. =    25.0    0.01    5
