*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    90 seconds

*** Test Cases ***
Run successfully 'pio_addition' example
    Execute Command             include @${CURDIR}/addition.resc

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Done  timeout=5
