*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    120 seconds

Resource    ${CURDIR}/../../../common.resource

*** Test Cases ***
Run successfully 'hello_dma' example
    Execute Command             include @${CURDIR}/hello_dma.resc
    Execute Command             logLevel -1
    
    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Hello, world! (from DMA)    timeout=4
   
 
