*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    120 seconds

*** Test Cases ***
Run successfully 'button' example
    Execute Command             include @${CURDIR}/button.resc

    Create LED Tester           sysbus.gpio.led  defaultTimeout=4 
    
    Start Emulation 
    Sleep                3s 
    Execute Command      sysbus.gpio.button Press
    Assert LED State     true 
    Execute Command      sysbus.gpio.button Release 
    Assert LED State     false 
    Execute Command      sysbus.gpio.button Press
    Assert LED State     true 
    Execute Command      sysbus.gpio.button Release 
    Assert LED State     false 

    

 


    
