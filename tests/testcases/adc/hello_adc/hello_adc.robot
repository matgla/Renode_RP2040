*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    120 seconds

Resource    ${CURDIR}/../../../common.resource

*** Test Cases ***
Run successfully 'hello_adc' example
    Execute Command             include @${CURDIR}/hello_adc.resc


    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       ADC Example, measuring GPIO26

    Line Should Contain ADC Print     0x5d1  1.2
    Line Should Contain ADC Print     0xaaa  2.2
    Line Should Contain ADC Print     0xaaa  2.2
    Line Should Contain ADC Print     0xfff  3.3
    Line Should Contain ADC Print     0x000  0.0


*** Keywords ***

Line Should Contain ADC Print
    [Arguments]     ${hex_value}    ${value}

    Uart Numeric Value Should Be    Raw value: ${hex_value}, voltage:    ${value}    0.01





