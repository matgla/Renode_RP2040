*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    80 seconds

Resource    ${CURDIR}/../../common.resource

*** Test Cases ***
Run successfully 'dreq_with_ring' example
    Execute Command             include @${CURDIR}/dreq_with_ring.resc
    Execute Command             logLevel -1
    
    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       DMA example with both DREQ and RING    timeout=1
    Wait For Line On Uart       DMA finished, INTR: 0    timeout=1
    Wait For Line On Uart       TO: [24, 25, 26, 27, 28, 29, 30, 31, 0, 0, 0, 0, 0, 0, 0, 0, ]      timeout=1
    Wait For Line On Uart       Ring from      timeout=1
    Wait For Line On Uart       TO: [0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, ]     timeout=1
    Wait For Line On Uart       Ring to peripheral    timeout=1
    Wait For Line On Uart       abcdabcdabcdabcd    timeout=1
    Wait For Line On Uart       Ring on peripheral write     timeout=1
    ${l}    Wait For Next Line On Uart      timeout=1
    LOG   ${l.line}
    Should Be Equal As Strings   ${l.line}   ac
    Wait For Line On Uart       Ring to data from peripheral read     timeout=1
    Write To Uart               abcdefgh    
    Wait For Line On Uart       Received data from uart: [101(e), 102(f), 103(g), 104(h), 0()         timeout=1
    Wait For Line On Uart       Ring from peripheral read      timeout=1
    Write To Uart               xyzw
    ${l}    Wait For Next Line On Uart      timeout=1
    Should Be Equal As Strings   ${l.line}      Received data from uart: [120(x), 0(), 122(z), 0(), 0(), 1(), 2(), 3(), ]      timeout=1
    Wait For Line On Uart       All Done     timeout=1












   
 
