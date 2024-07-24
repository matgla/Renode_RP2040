*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    90 seconds

*** Test Cases ***
Run successfully 'differential_machester' example
    Execute Command             include @${CURDIR}/differential_manchester.resc


    
