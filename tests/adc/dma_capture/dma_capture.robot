*** Settings ***
Resource            ${CURDIR}/../../common.resource
Library             Collections
Library             String

Suite Setup         Setup
Suite Teardown      Teardown
Test Teardown       Test Teardown
Test Timeout        200 seconds


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
    ${allSamples}    Remove Leading Zeros    ${allSamples}
    Log    List Without Leading Zeros: ${allSamples}


*** Keywords ***
Remove Leading Zeros
    [Arguments]    ${list}
    ${index}    Get Length    ${list}
    FOR    ${element}    IN RANGE    ${index}
        IF    '${list}[${element}]' != '0'
            RETURN    Slice List    ${list}    ${element}
        END
    END
