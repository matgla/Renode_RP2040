*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    20 seconds

Resource    ${CURDIR}/../../common.resource

*** Test Cases ***
Run successfully 'hello_adc' example
    Execute Command             include @${CURDIR}/hello_adc.resc
    Execute Command             logLevel -1
    
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
    
    ${l}    Wait For Next Line On Uart
    @{words}=   Split String    ${l.line}
    Should Be Equal As Strings      ${hex_value},   ${words}[2]
    Should Be Equal As Numbers With Tolerance   ${words}[4]    ${value} 




 
