*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    100 seconds

Resource    ${CURDIR}/../../common.resource

*** Test Cases ***
Run successfully 'adc_console' example
    Execute Command             include @${CURDIR}/adc_console.resc
    Execute Command             logLevel -1
    
    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart   RP2040 ADC and Test Console
    Measure Sample On Channel   1      0x4d9    1 
    Measure Sample On Channel   2      0x9b2    2 
    @{expectedSamples}=   Create List   000  00c  019  025  032  03e  04a  057  063  070  07c  088  095  0a1  0ae  0ba  0c7  0d3  0df  0ec  0f8  105  111  11d  12a  136  143  14f  15b  168  174  181  18d  19a  1a6  1b2  1bf  1cb  1d8  1e4  1f0  1fd  209  216  222  22e  23b  247  254  260   
    Capture Samples On Channel  0      ${expectedSamples}  20 

*** Keywords ***

Measure Sample On Channel
    [Arguments]     ${channel}    ${expected}   ${expectedVolts}
    
    Write Line To Uart      c${channel}
    Wait For Line On Uart   Switched to channel ${channel}      timeout=1
    Write Char On Uart      s 
    Wait For Line On Uart   s   timeout=1 
    ${l}    Wait For Next Line On Uart      timeout=1 
    @{words}=   Split String    ${l.line}
    Should Be Equal As Strings      ${expected}   ${words}[0]
    Should Be Equal As Numbers With Tolerance   ${words}[2]    ${expectedVolts} 

Capture Samples On Channel
    [Arguments]     ${channel}    ${samples}  ${repeats} 
    
    Write Line To Uart      c${channel}
    Wait For Line On Uart   Switched to channel ${channel}      timeout=1
    Write Char On Uart      S 
    Wait For Line On Uart   Starting capture                    timeout=1 
    Wait For Line On Uart   Done                                timeout=10
    FOR    ${sample}    IN   @{samples}   
        FOR    ${repeat}    IN RANGE   ${repeats} 
            ${l}    Wait For Next Line On Uart                  timeout=1 
            Should Be Equal As Strings   ${sample}    ${l.line}
        END
    END    
    






 
