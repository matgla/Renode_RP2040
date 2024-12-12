*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    300 seconds

Library         ${CURDIR}/DisplayTester.py

*** Test Cases ***
Run successfully 'hello_gpio_irq' example
    Execute Command             include @${CURDIR}/hello_gpio_irq.resc

    Create Terminal Tester      sysbus.uart0
    
    Wait For Line On Uart       Hello GPIO IRQ    timeout=4
    Execute Command             sysbus.gpio WritePin 2 true 
    Wait For Line On Uart       GPIO 2 EDGE_RISE    timeout=4
    Execute Command             sysbus.gpio WritePin 2 false 
    Wait For Line On Uart       GPIO 2 EDGE_FALL    timeout=4

    


