*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    30 seconds
Library    Collections

*** Test Cases ***
Run successfully 'ssi_dma' example
    Execute Command             include @${CURDIR}/ssi_dma.resc

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Starting DMA     timeout=5
    Wait For Line On Uart       DMA finished     timeout=5
    # Transfer speed in simulation is not accurate
    ${l}  Wait For Next Line On Uart        timeout=1
    @{words}=  Split String    ${l.line}
    Should Be Equal As Strings  ${words}[0]   Transfer
    Should Be Equal As Strings  ${words}[1]   speed:
    Should Be True		${words}[2]>50	
	Should Be True		${words}[2]<70	
    Should Be Equal As Strings   ${words}[3]  MB/s

    Wait For Line On Uart       Data check ok    timeout=1
