*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    90 seconds

*** Test Cases ***
Run successfully 'pio_clocked_input' example
    Execute Command             include @${CURDIR}/pio_clocked_input.resc

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Doing some random additions:
    FOR  ${i}  IN RANGE  10
    ${p}    Wait For Next Line On Uart    
    @{words} =  Split String    ${p.line}           
    ${result}  evaluate  ${words}[0] + ${words}[2] 
    Should Be Equal As Numbers  ${result}  ${words}[4]
    END

