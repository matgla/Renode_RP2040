*** Settings ***

Resource        ../../../common.resource
Resource        ../../../sensors.resource
Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    60 seconds

*** Variables ***
${UART_TIMEOUT}              5
${ALTITUDE_TOLERANCE}        0.5    # Tolerance for altitude values (meters)
${TEMP_TOLERANCE}            0.2    # Tolerance for temperature values (Celsius)

*** Test Cases ***
Run successfully 'mpl3115a2_i2c' example
    Start MPL3115A2 Example
    
    # Wait for first FIFO overflow and average reading (initial values: 0m, 25C)
    Wait For Line On Uart    FIFO overflow!    timeout=10
    MPL3115A2 Should Report Values    0.0    25.0    ${ALTITUDE_TOLERANCE}    ${TEMP_TOLERANCE}

*** Keywords ***
Start MPL3115A2 Example
    Execute Command             include @${CURDIR}/mpl3115a2_i2c.resc
    Create Terminal Tester      sysbus.uart0
    Wait For Line On Uart       Hello, MPL3115A2. Waiting for something to interrupt me!...    timeout=${UART_TIMEOUT}

MPL3115A2 Should Report Values
    [Arguments]    ${expected_alt}    ${expected_temp}    ${alt_tolerance}    ${temp_tolerance}
    
    # Read the average line: "32 sample average -> t: XX.XXXX C, h: XXXX.XXXX m"
    ${line_text}=    Wait For Line Matching    sample average    timeout=10
    
    # Extract and validate temperature (handles "t: XX.XXXX C" format)
    ${temp_value}=    Extract Temperature From Text    ${line_text}
    Should Be Equal As Numbers With Tolerance    ${temp_value}    ${expected_temp}    ${temp_tolerance}
    
    # Extract and validate altitude (handles "h: XXXX.XXXX m" format)
    ${alt_value}=    Extract Altitude From Text    ${line_text}
    Should Be Equal As Numbers With Tolerance    ${alt_value}    ${expected_alt}    ${alt_tolerance}
