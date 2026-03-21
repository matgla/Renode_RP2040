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
Run successfully 'pcf8523_i2c' example with time and date reading
    Start PCF8523 Example
    
    # Test 1: Verify initial time is 00:00:00 (firmware writes zeros on init)
    # and date is 00/00/00 Sunday (all zeros in BCD registers)
    PCF8523 Time Should Be    0    0    0
    PCF8523 Date Should Be    0    0    0    Sunday
    
    # Test 2: Alarm should be ringing - set Control_2 bit 3 (alarm flag)
    Execute Command             sysbus.i2c0.pcf8523 WriteDirect 0x01 0x08
    Wait For Line On Uart    ALARM RINGING    timeout=${UART_TIMEOUT}
    
    # Test 3: Set a specific time via simulator
    Execute Command             sysbus.i2c0.pcf8523 SetHours 14
    Execute Command             sysbus.i2c0.pcf8523 SetMinutes 30
    Execute Command             sysbus.i2c0.pcf8523 SetSeconds 45
    Execute Command             sysbus.i2c0.pcf8523 SetDay 15
    Execute Command             sysbus.i2c0.pcf8523 SetMonth 6
    Execute Command             sysbus.i2c0.pcf8523 SetYear 24
    Execute Command             sysbus.i2c0.pcf8523 SetWeekday 6
    PCF8523 Time Should Be    14    30    45
    PCF8523 Date Should Be    15    6    24    Saturday
    
    # Test 4: Set midnight on New Year's Eve
    Execute Command             sysbus.i2c0.pcf8523 SetHours 23
    Execute Command             sysbus.i2c0.pcf8523 SetMinutes 59
    Execute Command             sysbus.i2c0.pcf8523 SetSeconds 55
    Execute Command             sysbus.i2c0.pcf8523 SetDay 31
    Execute Command             sysbus.i2c0.pcf8523 SetMonth 12
    Execute Command             sysbus.i2c0.pcf8523 SetYear 99
    Execute Command             sysbus.i2c0.pcf8523 SetWeekday 5
    PCF8523 Time Should Be    23    59    55
    PCF8523 Date Should Be    31    12    99    Friday

*** Keywords ***
Start PCF8523 Example
    Execute Command             include @${CURDIR}/pcf8523_i2c.resc
    Create Terminal Tester      sysbus.uart0
    Wait For Line On Uart       Hello, PCF8520! Reading raw data from registers...    timeout=${UART_TIMEOUT}

PCF8523 Time Should Be
    [Arguments]    ${expected_hour}    ${expected_min}    ${expected_sec}
    
    # Read the time line - format is "Time: HH : MM : SS"
    ${line}=    Wait For Line On Uart    Time:    timeout=${UART_TIMEOUT}
    ${text}=    Set Variable    ${line['Line']}
    
    # Extract time using pattern "Time: HH : MM : SS"
    ${match}=    Evaluate    re.search(r"Time:\\s*(\\d+)\\s*:\\s*(\\d+)\\s*:\\s*(\\d+)", $text)    re
    Should Not Be Equal    ${match}    ${None}    msg=Could not parse time from: ${text}
    
    ${hour}=    Evaluate    int($match.group(1))
    ${minute}=    Evaluate    int($match.group(2))
    ${second}=    Evaluate    int($match.group(3))
    
    Should Be Equal As Numbers    ${hour}    ${expected_hour}
    Should Be Equal As Numbers    ${minute}    ${expected_min}
    Should Be Equal As Numbers    ${second}    ${expected_sec}

PCF8523 Date Should Be
    [Arguments]    ${expected_day}    ${expected_month}    ${expected_year}    ${expected_weekday}
    
    # Read the date line: "Date: Weekday DD / MM / YY"
    ${line}=    Wait For Line On Uart    Date:    timeout=${UART_TIMEOUT}
    ${text}=    Set Variable    ${line['Line']}
    
    # Verify weekday is in the line
    Should Contain    ${text}    ${expected_weekday}
    
    # Extract day, month, year using pattern "DD / MM / YY"
    ${match}=    Evaluate    re.search(r"(\\d+)\\s*/\\s*(\\d+)\\s*/\\s*(\\d+)", $text)    re
    Should Not Be Equal    ${match}    ${None}    msg=Could not parse date from: ${text}
    
    ${day}=    Evaluate    int($match.group(1))
    ${month}=    Evaluate    int($match.group(2))
    ${year}=    Evaluate    int($match.group(3))
    
    Should Be Equal As Numbers    ${day}    ${expected_day}
    Should Be Equal As Numbers    ${month}    ${expected_month}
    Should Be Equal As Numbers    ${year}    ${expected_year}
