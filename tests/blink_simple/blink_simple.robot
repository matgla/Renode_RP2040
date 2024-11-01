*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    30 seconds

*** Test Cases ***
Run successfully 'blink' example
    Execute Command             include @${CURDIR}/blink_simple.resc
    Execute Command             logLevel -1
    Create LED Tester           sysbus.gpio.led
    Assert LED Is Blinking      testDuration=1  onDuration=0.25  tolerance=0.05  offDuration=0.25

    
