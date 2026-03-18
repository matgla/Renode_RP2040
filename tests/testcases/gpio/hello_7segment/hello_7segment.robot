*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    300 seconds

*** Test Cases ***
Run successfully 'hello_7segment' example
    Execute Command             include @${CURDIR}/hello_7segment.resc

    Create Terminal Tester      sysbus.uart0
    
    Wait For Line On Uart       Hello, 7segment - press button to count down!    timeout=4
    Start Emulation
    # Test runs for a short time to verify basic functionality
    Sleep                       2
    Execute Command             sysbus.gpio.button Press
    Sleep                       2
