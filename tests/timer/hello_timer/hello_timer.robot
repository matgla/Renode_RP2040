*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    150 seconds

*** Test Cases ***
Run successfully 'hello_timer' example
    Execute Command             include @${CURDIR}/hello_timer.resc

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Hello Timer!    timeout=5
    Wait For Line On Uart       Timer 983041 fired!  timeout=5
    Wait For Line On Uart       Repeat at 25(.....)$  timeout=5   treatAsRegex=true
    Wait For Line On Uart       Repeat at 30(.....)$  timeout=5   treatAsRegex=true
    Wait For Line On Uart       Repeat at 35(.....)$  timeout=5   treatAsRegex=true
    Wait For Line On Uart       Repeat at 40(.....)$  timeout=5   treatAsRegex=true
    Wait For Line On Uart       Repeat at 45(.....)$  timeout=5   treatAsRegex=true
    Wait For Line On Uart       cancelled... 1  timeout=5
    Wait For Line On Uart       Repeat at 75(.....)$  timeout=5   treatAsRegex=true
    Wait For Line On Uart       Repeat at 80(.....)$  timeout=5   treatAsRegex=true
    Wait For Line On Uart       Repeat at 85(.....)$  timeout=5   treatAsRegex=true
    Wait For Line On Uart       Repeat at 90(.....)$  timeout=5   treatAsRegex=true
    Wait For Line On Uart       Repeat at 95(.....)$  timeout=5   treatAsRegex=true
    Wait For Line On Uart       Repeat at 100(.....)$  timeout=5   treatAsRegex=true
    Wait For Line On Uart       cancelled... 1  timeout=5
    Wait For Line On Uart       Done  timeout=5
