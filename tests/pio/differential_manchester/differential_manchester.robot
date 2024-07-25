*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    90 seconds

*** Test Cases ***
Run successfully 'differential_machester' example
    Execute Command             include @${CURDIR}/differential_manchester.resc

    Create Terminal Tester          sysbus.uart0

    Wait For Line On Uart       00000000    timeout=1
    Wait For Line On Uart       0ff0a55a    timeout=1
    Wait For Line On Uart       12345678    timeout=1

    
