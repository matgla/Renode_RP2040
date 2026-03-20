*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    60 seconds

*** Test Cases ***
Run successfully 'i2c_bus_scan' example
    Execute Command             include @${CURDIR}/bus_scan.resc

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       I2C Bus Scan    timeout=5
    Wait For Line On Uart       Done.           timeout=5
