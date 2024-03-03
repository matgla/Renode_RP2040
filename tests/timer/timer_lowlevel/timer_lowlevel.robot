*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    90 seconds

*** Test Cases ***
Run successfully 'timer_lowlevel' example
    Execute Command             include @${CURDIR}/timer_lowlevel.repl

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Timer lowlevel!    timeout=5
    Wait For Line On Uart       Alarm IRQ fired  timeout=5
    Wait For Line On Uart       Alarm IRQ fired  timeout=5
    Wait For Line On Uart       Alarm IRQ fired  timeout=5
    Wait For Line On Uart       Alarm IRQ fired  timeout=5
