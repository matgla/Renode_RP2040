*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    60 seconds

*** Test Cases ***
Run successfully 'pcf8523_i2c' example
    Execute Command             include @${CURDIR}/pcf8523_i2c.resc

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Hello, PCF8520! Reading raw data from registers...    timeout=5
    Wait For Line On Uart       ALARM RINGING                                        timeout=5
