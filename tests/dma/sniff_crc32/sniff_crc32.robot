*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    40 seconds

Resource    ${CURDIR}/../../common.resource

*** Test Cases ***
Run successfully 'sniff_crc32' example
    Execute Command             include @${CURDIR}/sniff_crc.resc
    Execute Command             logLevel -1
    
    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Buffer to DMA: 0x31 0x32 0x33 0x34 0x35 0x36 0x37 0x38 0x39 0x00 0x00 0x00 0x00     timeout=1
    Wait For Line On Uart       Completed DMA sniff of 13 byte buffer, DMA sniff accumulator value: 0xd05d3bea      timeout=1
