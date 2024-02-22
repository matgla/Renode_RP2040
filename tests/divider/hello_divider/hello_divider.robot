*** Settings ***
Resource    ../../prepare.robot

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    90 seconds

*** Test Cases ***
Run successfully 'hello_divider' example
    Execute Command             include @${CURDIR}/hello_divider.repl

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Hello, divider!    timeout=1
    Wait For Line On Uart       123456/-321 = -384 remainder 192    timeout=1
    Wait For Line On Uart       Working backwards! Result 123456 should equal 123456!    timeout=1

    Wait For Line On Uart       123456/321 = 384 remainder 192    timeout=1
    Wait For Line On Uart       Working backwards! Result 123456 should equal 123456!    timeout=1

    Wait For Line On Uart       Async result 123456/-321 = -384 remainder 192  timeout=1
    Wait For Line On Uart       123456 / -321 = (by operator -384) (inlined -384)  timeout=1
    Wait For Line On Uart       inner 123 / 7 = 17  timeout=1
    Wait For Line On Uart       outer divide 123456 / -321 = -384  timeout=1
