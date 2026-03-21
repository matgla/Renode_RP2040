*** Settings ***

Resource        ../../../common.resource
Resource        ../../../sensors.resource
Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    60 seconds

*** Variables ***
${UART_TIMEOUT}              5

*** Test Cases ***
Run successfully 'pa1010d_i2c' example with valid GPS data
    Start PA1010D Example
    
    # Test 1: Valid GPS data (London coordinates)
    PA1010D Should Report Valid Data
    PA1010D Latitude Should Be    4807.038    N
    PA1010D Longitude Should Be    01131.000    E
    PA1010D UTC Time Should Be    123519.00
    PA1010D Date Should Be    23/03/94
    PA1010D Speed Should Be    022.4
    PA1010D Course Should Be    084.4
    
    # Test 2: Invalid GPS data (no fix)
    Execute Command             sysbus.i2c0.pa1010d SetDataInvalid
    Execute Command             sysbus.i2c0.pa1010d SetUtcTime "000000.00"
    PA1010D Should Report Invalid Data
    
    # Test 3: Different location (New York coordinates approximately)
    Execute Command             sysbus.i2c0.pa1010d SetUtcTime "153022.50"
    Execute Command             sysbus.i2c0.pa1010d SetDataValid
    Execute Command             sysbus.i2c0.pa1010d SetLatitude "4042.441"
    Execute Command             sysbus.i2c0.pa1010d SetNsIndicator "N"
    Execute Command             sysbus.i2c0.pa1010d SetLongitude "07400.321"
    Execute Command             sysbus.i2c0.pa1010d SetEwIndicator "W"
    Execute Command             sysbus.i2c0.pa1010d SetSpeedOverGround "015.0"
    Execute Command             sysbus.i2c0.pa1010d SetCourseOverGround "270.0"
    Execute Command             sysbus.i2c0.pa1010d SetDate "151023"
    PA1010D Should Report Valid Data
    PA1010D Latitude Should Be    4042.441    N
    PA1010D Longitude Should Be    07400.321    W
    PA1010D UTC Time Should Be    153022.50
    PA1010D Date Should Be    15/10/23
    PA1010D Speed Should Be    015.0
    PA1010D Course Should Be    270.0

*** Keywords ***
Start PA1010D Example
    Execute Command             include @${CURDIR}/pa1010d_i2c.resc
    Create Terminal Tester      sysbus.uart0
    Wait For Line On Uart       Hello, PA1010D! Reading raw data from module...    timeout=${UART_TIMEOUT}

PA1010D Should Report Valid Data
    ${line}=    Wait For Line On Uart    Status:    timeout=${UART_TIMEOUT}
    Should Contain    ${line['Line']}    Data Valid

PA1010D Should Report Invalid Data
    ${line}=    Wait For Line On Uart    Status:    timeout=${UART_TIMEOUT}
    Should Contain    ${line['Line']}    Data invalid

PA1010D Latitude Should Be
    [Arguments]    ${expected_lat}    ${expected_ns}
    ${line}=    Wait For Line On Uart    Latitude:    timeout=${UART_TIMEOUT}
    Should Contain    ${line['Line']}    ${expected_lat}
    ${line_ns}=    Wait For Line On Uart    N/S indicator:    timeout=${UART_TIMEOUT}
    Should Contain    ${line_ns['Line']}    ${expected_ns}

PA1010D Longitude Should Be
    [Arguments]    ${expected_lon}    ${expected_ew}
    ${line}=    Wait For Line On Uart    Longitude:    timeout=${UART_TIMEOUT}
    Should Contain    ${line['Line']}    ${expected_lon}
    ${line_ew}=    Wait For Line On Uart    E/W indicator:    timeout=${UART_TIMEOUT}
    Should Contain    ${line_ew['Line']}    ${expected_ew}

PA1010D UTC Time Should Be
    [Arguments]    ${expected_time}
    ${line}=    Wait For Line On Uart    UTC Time:    timeout=${UART_TIMEOUT}
    Should Contain    ${line['Line']}    ${expected_time}

PA1010D Date Should Be
    [Arguments]    ${expected_date}
    ${line}=    Wait For Line On Uart    Date:    timeout=${UART_TIMEOUT}
    Should Contain    ${line['Line']}    ${expected_date}

PA1010D Speed Should Be
    [Arguments]    ${expected_speed}
    ${line}=    Wait For Line On Uart    Speed over ground:    timeout=${UART_TIMEOUT}
    Should Contain    ${line['Line']}    ${expected_speed}

PA1010D Course Should Be
    [Arguments]    ${expected_course}
    ${line}=    Wait For Line On Uart    Course over ground:    timeout=${UART_TIMEOUT}
    Should Contain    ${line['Line']}    ${expected_course}
