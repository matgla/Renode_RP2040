*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    30 seconds
Library    Collections

*** Test Cases ***
Run successfully 'flash_program' example
    Execute Command             include @${CURDIR}/flash_program.resc

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart           Generated random data:    timeout=1
    ${generatedData}    Create List
    FOR     ${repeat}    IN RANGE     16 
        ${l}    Wait For Next Line On Uart   timeout=1 
        @{samples}    Split String   ${l.line}
        Append To List   ${generatedData}    @{samples}    
    END
    
    Wait For Line On Uart       Erasing target region...    timeout=1
    Wait For Line On Uart       Done. Read back target region:    timeout=1
    FOR     ${repeat}    IN RANGE     16 
        Wait For Line On Uart     ff ff ff ff ff ff ff ff ff ff ff ff ff ff ff ff     timeout=1  
    END
 
    Wait For Line On Uart       Programming target region...    timeout=1
    Wait For Line On Uart       Done. Read back target region:     timeout=1
        
    ${finalData}    Create List
    FOR     ${repeat}    IN RANGE     16 
        ${l}    Wait For Next Line On Uart   timeout=1 
        @{samples}    Split String   ${l.line}
        Append To List   ${finalData}    @{samples}    
    END

    Lists Should Be Equal   ${finalData}   ${generatedData}
    Wait For Line On Uart      Programming successful!     timeout=1
 
