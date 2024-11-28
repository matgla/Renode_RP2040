*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    80 seconds

Resource    ${CURDIR}/../../common.resource

*** Test Cases ***
Run successfully 'microphone_adc' example
    Execute Command             include @${CURDIR}/microphone_adc.resc
    Execute Command             logLevel -1
    
    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart     Beep boop, listening...    timeout=1
    FOR    ${counter}    IN RANGE    20
        ${l}     Wait For Next Line On Uart      timeout=1
        Should Be Equal As Numbers With Tolerance     ${l.line}    ${counter} * 0.1      0.01 
    END


  
