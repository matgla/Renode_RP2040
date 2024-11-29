*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    270 seconds

*** Test Cases ***
Run successfully 'hello_pio' example
    Execute Command             include @${CURDIR}/hello_pio.resc
    Execute Command             logLevel -1
    Create LED Tester           sysbus.gpio.led
    Assert LED Is Blinking      testDuration=2  onDuration=0.5  tolerance=0.05  offDuration=0.5

    
