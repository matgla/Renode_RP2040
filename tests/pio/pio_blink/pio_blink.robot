*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    90 seconds

*** Test Cases ***
Run successfully 'pio_blink' example
    Execute Command             include @${CURDIR}/pio_blink.resc

    Create LED Tester           sysbus.gpio.led
    Assert LED Is Blinking      testDuration=4  onDuration=0.5  tolerance=0.05  offDuration=0.5

    
