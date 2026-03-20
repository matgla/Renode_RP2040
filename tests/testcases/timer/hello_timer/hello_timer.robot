*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    500 seconds

*** Test Cases ***
Run successfully 'hello_timer' example
    Execute Command             include @${CURDIR}/hello_timer.resc

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Hello Timer!    timeout=5
    Wait For Line On Uart       Timer 983041 fired!  timeout=5
    Wait For Line On Uart       Repeat at \\d+$  timeout=5   treatAsRegex=true
    Wait For Line On Uart       cancelled... 1  timeout=5
    Wait For Line On Uart       Repeat at \\d+$  timeout=5   treatAsRegex=true
    Wait For Line On Uart       cancelled... 1  timeout=5
    Wait For Line On Uart       Done  timeout=5
