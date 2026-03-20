*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    80 seconds

Resource    ${CURDIR}/../../../common.resource

*** Test Cases ***
Run successfully 'microphone_adc' example
    Execute Command             include @${CURDIR}/microphone_adc.resc


    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart     Beep boop, listening...    timeout=1
    FOR    ${counter}    IN RANGE    20
        ${expected}=    Evaluate    ${counter} * 0.1
        Uart Next Numeric Value Should Be    ${expected}    0.01    1
    END



