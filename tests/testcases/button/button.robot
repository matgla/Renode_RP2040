*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    120 seconds

*** Test Cases ***
Run successfully 'button' example
    Execute Command             include @${CURDIR}/button.resc

    Create LED Tester           sysbus.gpio.led  defaultTimeout=10 
    
    # Boot firmware deterministically (1 virtual second, host-speed independent)
    Execute Command             emulation RunFor "00:00:01"
    
    # Press button while emulation is paused — firmware has booted, GPIO is initialized
    Execute Command      sysbus.gpio.button Press
    Start Emulation
    Assert LED State     true 
    Execute Command      sysbus.gpio.button Release 
    Assert LED State     false 
    Execute Command      sysbus.gpio.button Press
    Assert LED State     true 
    Execute Command      sysbus.gpio.button Release 
    Assert LED State     false 

    

 


    
