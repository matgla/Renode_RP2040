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

    Wait For Line On Uart       Starting DMA     timeout=1
    # Transfer speed in simulation is not accurate
    Wait For Line On Uart       Transfer speed:  timeout=1
    Wait For Line On Uart       Starting DMA     timeout=1
    Wait For Line On Uart       Data check ok    timeout=1
