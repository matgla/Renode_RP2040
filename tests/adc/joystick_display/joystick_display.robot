*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    80 seconds

Resource    ${CURDIR}/../../common.resource

*** Test Cases ***
Run successfully 'joystick_display' example
    Execute Command             include @${CURDIR}/joystick_display.resc
    Execute Command             logLevel -1
    
    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart        X: [${SPACE * 14}o${SPACE * 25}]${SPACE} Y: [${SPACE * 26}o${SPACE * 13}]       timeout=1
    Wait For Line On Uart        X: [${SPACE * 26}o${SPACE * 13}]${SPACE} Y: [${SPACE * 12}o${SPACE * 27}]       timeout=1
    Wait For Line On Uart        X: [${SPACE * 26}o${SPACE * 13}]${SPACE} Y: [${SPACE * 12}o${SPACE * 27}]       timeout=1
    Wait For Line On Uart        X: [o${SPACE * 39}]${SPACE} Y: [o${SPACE * 39}]                                  timeout=1
     

  
