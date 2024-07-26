*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    90 seconds

*** Test Cases ***
Run successfully 'pio_clocked_input' example
    Execute Command             include @${CURDIR}/clocked_input.resc

    Create Terminal Tester      sysbus.uart0

    ${data}=    Create List 
    Wait For Line On Uart       Data to transmit:
    FOR  ${i}  IN RANGE  8 
    ${number}    Wait For Next Line On Uart    
    Append To List  ${data}  "${number} OK"
    END

    FOR  ${i}  IN RANGE  8 
    ${number}    Wait For Next Line On Uart    
    Should Contain  ${data}  "${number}"
    END

    


