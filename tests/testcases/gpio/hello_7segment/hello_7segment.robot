*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    160 seconds

Library         ${CURDIR}/DisplayTester.py

*** Test Cases ***
Run successfully 'hello_7segment' example
    Execute Command             include @${CURDIR}/hello_7segment.resc
    HelloKeyword
