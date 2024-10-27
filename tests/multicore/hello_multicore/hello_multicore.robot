*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    90 seconds

*** Test Cases ***
Run successfully 'hello_multicore' example
    Execute Command             include @${CURDIR}/hello_multicore.resc

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Hello, multicore!    timeout=4
    Wait For Line On Uart       It's all gone well on core 0!    timeout=1
    Wait For Line On Uart       Its all gone well on core 1!    timeout=1
