*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    90 seconds

*** Test Cases ***
Run successfully 'hello_serial' example
    Execute Command             include @${CURDIR}/hello_serial.repl

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Hello, world!    timeout=4
    Wait For Line On Uart       Hello, world!    timeout=4
    Wait For Line On Uart       Hello, world!    timeout=4
    Wait For Line On Uart       Hello, world!    timeout=4
    Wait For Line On Uart       Hello, world!    timeout=4
