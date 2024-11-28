*** Settings ***
Resource            ${CURDIR}/../../common.resource
Library             Collections
Library             String

Suite Setup         Setup
Suite Teardown      Teardown
Test Teardown       Test Teardown
Test Timeout        1000 seconds


*** Test Cases ***
Run successfully 'dma_capture' example
    Execute Command    include @${CURDIR}/dma_capture.resc
    Execute Command    logLevel -1

    Create Terminal Tester    sysbus.uart0

    Wait For Line On Uart    Arming DMA    timeout=1
    Wait For Line On Uart    Starting capture    timeout=40
    Wait For Line On Uart    Capture finished    timeout=40

    ${allSamples}    Create List
    FOR    ${repeat}    IN RANGE    10
        ${l}    Wait For Next Line On Uart    timeout=1
        ${data}    Replace String    ${l.line}    ${space}    ${empty}
        @{samples}    Split String    ${data}    ,
        @{samples}    Evaluate    [x for x in @{samples} if x]
        Append To List    ${allSamples}    @{samples}
    END

    Log    Before: ${allSamples}
    ${index}    Get Length    ${allSamples}
    FOR    ${element}    IN RANGE    ${index}
        IF    '${allSamples}[${element}]' != '0'
            ${allSamples}     Get Slice From List      ${allSamples}    ${element}
            BREAK
        END
    END

    Log    List Without Leading Zeros: ${allSamples}
    ${i}    Set Variable     0
    ${raising}        Set Variable    True 

    FOR    ${element}    IN    @{allSamples}
        Log    ${element}
        IF    ${raising} == True 
            IF   ${i} < 31 
                ${i}    Evaluate     ${i} + 1  
            ELSE
                ${raising}       Set Variable    False
            END 
        ELSE
            IF     ${i} > 0 
                ${i}    Evaluate    ${i} - 1
            ELSE 
                ${raising}      Set Variable     True 
            END
        END
        ${expected}=     Evaluate      round((${i}/ 31) * 255, 0)
        Log    Expected: ${expected} -> ${element}
        ${delta}      Evaluate     abs(${expected}-${element})
        Should Be True        ${delta} < 2 
    END
