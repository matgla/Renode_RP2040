*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    160 seconds

Library         ${CURDIR}/DisplayTester.py

*** Test Cases ***
Run successfully 'hello_7segment' example
    Execute Command             include @${CURDIR}/hello_7segment.resc
    Execute Command             RegisterDisplayTester "display_tester" sysbus.gpio.segment_display 5 
    Execute Command             WaitForSequence "display_tester" "${CURDIR}/sequence.json" 40
