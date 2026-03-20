*** Settings ***

Resource        ${CURDIR}/../../../common.resource

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    180 seconds

*** Test Cases ***
Run successfully 'hello_gpout' example
    Execute Command             include @${CURDIR}/hello_gpout.resc


    Create Terminal Tester      sysbus.uart0
    Wait For Line On Uart           Hello gpout           timeout=1
    ${led1}=     Create LED Tester     sysbus.gpio.led0
    ${led2}=     Create LED Tester     sysbus.gpio.led1
    ${led3}=     Create LED Tester     sysbus.gpio.led2
    ${led4}=     Create LED Tester     sysbus.gpio.led3
    Assert LED Is Blinking    testDuration=0.001    onDuration=0.000004    offDuration=0.000004    tolerance=0.2    testerId=${led1}
    Assert LED Is Blinking    testDuration=0.001    onDuration=0.00001042    offDuration=0.00001042    tolerance=0.1    testerId=${led2}
    Assert LED Is Blinking    testDuration=0.001    onDuration=0.00001042    offDuration=0.00001042    tolerance=0.1    testerId=${led3}
    Assert LED Is Blinking    testDuration=0.001    onDuration=0.000107    offDuration=0.000107    tolerance=0.05    testerId=${led4}

