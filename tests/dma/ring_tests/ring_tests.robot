*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    40 seconds

Resource    ${CURDIR}/../../common.resource

*** Test Cases ***
Run successfully 'ring_tests' example
    Execute Command             include @${CURDIR}/ring_tests.resc
    Execute Command             logLevel -1
    
    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       DMA example with RING    timeout=1
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
    Wait For Line On Uart       Received data from uart: [0, 0, 0, 0, 0, 1         timeout=1
    Wait For Line On Uart       Ring from peripheral read      timeout=1
    Wait For Line On Uart       Received data from uart: [3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0         timeout=1
    Wait For Line On Uart       All Done     timeout=1












   
 
