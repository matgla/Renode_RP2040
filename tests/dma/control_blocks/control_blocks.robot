*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    80 seconds

Resource    ${CURDIR}/../../common.resource

*** Test Cases ***
Run successfully 'dma_control_blocks' example
    Execute Command             include @${CURDIR}/control_blocks.resc
    Execute Command             logLevel -1
    
    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       DMA control block example:              timeout=1
    Wait For Line On Uart       Transferring one word at a time.        timeout=1
    Wait For Line On Uart       DMA finished.                           timeout=1




   
 
