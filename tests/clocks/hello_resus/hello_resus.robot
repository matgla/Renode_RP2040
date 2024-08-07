*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    30 seconds

*** Test Cases ***
Run successfully 'hello_resus' example
    Execute Command             include @${CURDIR}/hello_resus.resc
    Execute Command             logLevel -1

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart           Hello resus           timeout=1
    Wait For Line On Uart           Resus event fired     timeout=1
 
