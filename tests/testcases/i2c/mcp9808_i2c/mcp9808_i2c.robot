*** Settings ***

Resource        ../../../common.resource
Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    60 seconds

*** Variables ***
${UART_TIMEOUT}              5
${TEMP_TOLERANCE}            0.1

*** Test Cases ***
Run successfully 'mcp9808_i2c' example with multiple temperatures
    Start MCP9808 Example
    
    # Reading 1: 16.0°C (set in .resc file)
    MCP9808 Should Report Temperature    1    16.0    ${TEMP_TOLERANCE}
    
    # Update to Reading 2: 24.5°C
    Execute Command             sysbus.i2c0.mcp9808 SetAmbientTemperature 24.5
    MCP9808 Should Report Temperature    2    24.5    ${TEMP_TOLERANCE}
    
    # Update to Reading 3: 8.25°C (tests fractional part)
    Execute Command             sysbus.i2c0.mcp9808 SetAmbientTemperature 8.25
    MCP9808 Should Report Temperature    3    8.25    ${TEMP_TOLERANCE}
    
    # Update to Reading 4: 31.75°C (near upper limit)
    Execute Command             sysbus.i2c0.mcp9808 SetAmbientTemperature 31.75
    MCP9808 Should Report Temperature    4    31.75    ${TEMP_TOLERANCE}
    
    Wait For Line On Uart       Test completed.    timeout=${UART_TIMEOUT}

*** Keywords ***
Start MCP9808 Example
    Execute Command             include @${CURDIR}/mcp9808_i2c.resc
    Create Terminal Tester      sysbus.uart0
    Wait For Line On Uart       Hello, MCP9808! Reading raw data from registers...    timeout=${UART_TIMEOUT}

MCP9808 Should Report Temperature
    [Arguments]    ${reading_num}    ${expected}    ${tolerance}
    ${line}=    Wait For Line On Uart    Reading ${reading_num}:    timeout=${UART_TIMEOUT}
    ${text}=    Set Variable    ${line['Line']}
    ${value}=    Extract Numeric Value From Text    ${text}    Ambient temperature:
    Should Be Equal As Numbers With Tolerance    ${value}    ${expected}    ${tolerance}
