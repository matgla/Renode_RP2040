*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    90 seconds

*** Test Cases ***
Run successfully 'pio_blink' example
    Execute Command             include @${CURDIR}/pio_blink.resc
    Execute Command             logLevel -1
    ${led1}=     Create LED Tester           sysbus.gpio.led1   defaultTimeout=5
    ${led2}=     Create LED Tester           sysbus.gpio.led2   defaultTimeout=5
    ${led3}=     Create LED Tester           sysbus.gpio.led3   defaultTimeout=5


    Assert LED Is Blinking      testDuration=2  onDuration=0.17  offDuration=0.17  testerId=${led1}
    Assert LED Is Blinking      testDuration=2  onDuration=0.125  offDuration=0.125  testerId=${led2}
    Assert LED Is Blinking      testDuration=2  onDuration=0.50  offDuration=0.50  testerId=${led3}
 


    
