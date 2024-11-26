*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    270 seconds

*** Test Cases ***
Run successfully 'pio_blink' example
    Execute Command             include @${CURDIR}/pio_blink.resc
    Execute Command             logLevel -1
    ${led1}=     Create LED Tester           sysbus.gpio.led1   
    ${led2}=     Create LED Tester           sysbus.gpio.led2  
    ${led3}=     Create LED Tester           sysbus.gpio.led3 
    ${led4}=     Create LED Tester           sysbus.gpio.led4 

    Assert LED Is Blinking      testDuration=0.6  onDuration=0.125  offDuration=0.125  testerId=${led1}
    Assert LED Is Blinking      testDuration=0.7  onDuration=0.165  offDuration=0.165  testerId=${led2}
    Assert LED Is Blinking      testDuration=1.1  onDuration=0.25  offDuration=0.25  testerId=${led3}
    Assert LED Is Blinking      testDuration=2.1  onDuration=0.5     offDuration=0.5     testerId=${led4}


 


    
