*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    160 seconds

Library         ${CURDIR}/DisplayTester.py

*** Test Cases ***
Run successfully 'hello_7segment' example
    Execute Command             include @${CURDIR}/hello_7segment.resc

    Create Terminal Tester      sysbus.uart0
    
    Execute Command             RegisterDisplayTester display_tester sysbus.gpio.segment_display
    Wait For Line On Uart       Hello, 7segment - press button to count down!    timeout=4
    Start Emulation
    Execute Command             WaitForSequence display_tester "${CURDIR}/sequence.json" 20
    Execute Command             sysbus.gpio.button Press
    Execute Command             WaitForSequence display_tester "${CURDIR}/reversed_sequence.json" 20

