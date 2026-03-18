*** Settings ***

Resource        ../../../common.resource
Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    60 seconds

*** Test Cases ***
Run successfully 'bmp280_i2c' example
    Execute Command             include @${CURDIR}/bmp280_i2c.resc

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Hello, BMP280! Reading temperaure and pressure values from sensor...    timeout=5

    ${pressure_line}=           Wait For Line On Uart       Pressure =                                                    timeout=5
    Log                         Pressure line: ${pressure_line}
    ${pressure_text}=           Set Variable    ${pressure_line['Line']}
    ${pressure_value}=          Extract Number From String    ${pressure_text}    Pressure =
    Log                         Extracted pressure: ${pressure_value} kPa
    Should Be Close             ${pressure_value}    101.325    0.01

    ${temp_line}=               Wait For Line On Uart       Temp. =                                                       timeout=5
    Log                         Temperature line: ${temp_line}
    ${temp_text}=               Set Variable    ${temp_line['Line']}
    ${temp_value}=              Extract Number From String    ${temp_text}    Temp. =
    Log                         Extracted temperature: ${temp_value} C
    Should Be Close             ${temp_value}    25.0    0.01

*** Keywords ***
Extract Number From String
    [Arguments]    ${text}    ${prefix}
    [Documentation]    Extracts a floating point number from a string after the given prefix.
    ...    Example: "Pressure = 1013.250 kPa" with prefix "Pressure =" returns 1013.250
    # Single Python expression to extract and convert the number
    ${value}=    Evaluate    float(__import__('re').search(r'${prefix}\\s*([0-9.]+)', '''${text}''').group(1))
    RETURN       ${value}

Should Be Close
    [Arguments]    ${actual}    ${expected}    ${tolerance}=0.01
    ${difference}=    Evaluate    abs(${actual} - ${expected})
    Should Be True    ${difference} <= ${tolerance}
