*** Settings ***
Documentation    Helper keywords for testing sensor readings from UART output.
...              Provides clean abstractions for extracting and validating temperature,
...              acceleration, altitude, and other sensor values.

*** Keywords ***

# =============================================================================
# Temperature Keywords
# =============================================================================

Uart Temperature Should Be
    [Documentation]    Reads temperature from UART line containing the given prefix.
    ...    Example: "Temperature: 25.5 C" with prefix "Temperature:"
    [Arguments]    ${expected}    ${prefix}=Temperature    ${tolerance}=0.1    ${timeout}=5
    ${value}=    Read Sensor Value By Prefix    ${prefix}    ${timeout}
    Should Be Equal As Numbers With Tolerance    ${value}    ${expected}    ${tolerance}

Uart Temperature In Celsius Should Be
    [Documentation]    Reads temperature in Celsius (looks for "°C" or "C" suffix).
    [Arguments]    ${expected}    ${prefix}=${EMPTY}    ${tolerance}=0.1    ${timeout}=5
    ${line}=    Wait For Line Matching    ${prefix}    ${timeout}
    ${value}=    Extract Value With Unit    ${line}    C    ${prefix}
    Should Be Equal As Numbers With Tolerance    ${value}    ${expected}    ${tolerance}

Uart Temperature In Fahrenheit Should Be
    [Documentation]    Reads temperature in Fahrenheit (looks for "°F" or "F" suffix).
    [Arguments]    ${expected}    ${prefix}=${EMPTY}    ${tolerance}=0.1    ${timeout}=5
    ${line}=    Wait For Line Matching    ${prefix}    ${timeout}
    ${value}=    Extract Value With Unit    ${line}    F    ${prefix}
    Should Be Equal As Numbers With Tolerance    ${value}    ${expected}    ${tolerance}

Sensor Temperature Should Be
    [Documentation]    Validates temperature from a sensor-specific output format.
    ...    Supports formats like "t: 25.5 C", "Temp: 25°C", "25.5 C"
    [Arguments]    ${expected}    ${tolerance}=0.1    ${timeout}=5
    ${line}=    Wait For Line On Uart    t:\\s*    timeout=${timeout}
    ${text}=    Set Variable    ${line['Line']}
    ${value}=    Extract Temperature From Text    ${text}
    Should Be Equal As Numbers With Tolerance    ${value}    ${expected}    ${tolerance}

# =============================================================================
# Acceleration Keywords
# =============================================================================

Uart Acceleration X Should Be
    [Documentation]    Validates X-axis acceleration in m/s^2.
    [Arguments]    ${expected}    ${tolerance}=0.15    ${timeout}=5
    ${value}=    Read Sensor Value By Prefix    X acceleration    ${timeout}
    Should Be Equal As Numbers With Tolerance    ${value}    ${expected}    ${tolerance}

Uart Acceleration Y Should Be
    [Documentation]    Validates Y-axis acceleration in m/s^2.
    [Arguments]    ${expected}    ${tolerance}=0.15    ${timeout}=5
    ${value}=    Read Sensor Value By Prefix    Y acceleration    ${timeout}
    Should Be Equal As Numbers With Tolerance    ${value}    ${expected}    ${tolerance}

Uart Acceleration Z Should Be
    [Documentation]    Validates Z-axis acceleration in m/s^2.
    [Arguments]    ${expected}    ${tolerance}=0.15    ${timeout}=5
    ${value}=    Read Sensor Value By Prefix    Z acceleration    ${timeout}
    Should Be Equal As Numbers With Tolerance    ${value}    ${expected}    ${tolerance}

Uart Acceleration XYZ Should Be
    [Documentation]    Validates all three acceleration axes at once.
    [Arguments]    ${expected_x}    ${expected_y}    ${expected_z}    ${tolerance}=0.15    ${timeout}=5
    Uart Acceleration X Should Be    ${expected_x}    ${tolerance}    ${timeout}
    Uart Acceleration Y Should Be    ${expected_y}    ${tolerance}    ${timeout}
    Uart Acceleration Z Should Be    ${expected_z}    ${tolerance}    ${timeout}

# =============================================================================
# Altitude/Pressure Keywords
# =============================================================================

Uart Altitude Should Be
    [Documentation]    Validates altitude in meters.
    ...    Supports formats like "h: 123.45 m", "Altitude: 123m"
    [Arguments]    ${expected}    ${tolerance}=0.5    ${timeout}=5
    ${line}=    Wait For Line On Uart    h:\\s*    timeout=${timeout}
    ${text}=    Set Variable    ${line['Line']}
    ${value}=    Extract Altitude From Text    ${text}
    Should Be Equal As Numbers With Tolerance    ${value}    ${expected}    ${tolerance}

Uart Altitude In Meters Should Be
    [Documentation]    Validates altitude by looking for "m" suffix.
    [Arguments]    ${expected}    ${prefix}=${EMPTY}    ${tolerance}=0.5    ${timeout}=5
    ${line}=    Wait For Line Matching    ${prefix}    ${timeout}
    ${value}=    Extract Value With Unit    ${line}    m    ${prefix}
    Should Be Equal As Numbers With Tolerance    ${value}    ${expected}    ${tolerance}

# =============================================================================
# Generic Sensor Value Keywords
# =============================================================================

Read Sensor Value By Prefix
    [Documentation]    Reads a numeric value following a specific prefix from UART.
    ...    Handles formats like "Prefix: 123.45" or "Prefix = 123.45"
    [Arguments]    ${prefix}    ${timeout}=5
    ${line}=    Wait For Line On Uart    ${prefix}    timeout=${timeout}
    ${text}=    Set Variable    ${line['Line']}
    ${value}=    Extract Numeric Value After Prefix    ${text}    ${prefix}
    RETURN    ${value}

Sensor Value Should Be
    [Documentation]    Generic validation of a sensor value with prefix.
    [Arguments]    ${prefix}    ${expected}    ${tolerance}=0.1    ${timeout}=5
    ${value}=    Read Sensor Value By Prefix    ${prefix}    ${timeout}
    Should Be Equal As Numbers With Tolerance    ${value}    ${expected}    ${tolerance}

# =============================================================================
# Text Extraction Helpers (hide regex implementation details)
# =============================================================================

Extract Temperature From Text
    [Documentation]    Extracts temperature value from text. Handles formats like "t: 25.5 C"
    [Arguments]    ${text}
    ${pattern}=    Set Variable    t:\\s*([-+]?[0-9]+(?:\\.[0-9]+)?)
    ${match}=    Evaluate    re.search(r"${pattern}", $text)    re
    Should Not Be Equal    ${match}    ${None}    msg=Could not extract temperature from: ${text}
    ${value}=    Evaluate    float($match.group(1))
    RETURN    ${value}

Extract Altitude From Text
    [Documentation]    Extracts altitude value from text. Handles various formats.
    ...    Examples: "h: 123.45 m", "Altitude: 123m", "Alt = 100"
    [Arguments]    ${text}
    ${pattern}=    Set Variable    (?i)(?:h|alt(?:itude)?)[:\\s=]+\\s*([-+]?[0-9]+(?:\\.[0-9]+)?)
    ${match}=    Evaluate    re.search(r"${pattern}", $text)    re
    Should Not Be Equal    ${match}    ${None}    msg=Could not extract altitude from: ${text}
    ${value}=    Evaluate    float($match.group(1))
    RETURN    ${value}

Extract Acceleration From Text
    [Documentation]    Extracts acceleration value from text.
    ...    Examples: "X acceleration: 9.81", "Accel X = 9.81"
    [Arguments]    ${text}    ${axis}
    ${pattern}=    Set Variable    (?i)(?:${axis})[^0-9-]*([-+]?[0-9]+(?:\\.[0-9]+)?)
    ${match}=    Evaluate    re.search(r"${pattern}", $text)    re
    Should Not Be Equal    ${match}    ${None}    msg=Could not extract ${axis} acceleration from: ${text}
    ${value}=    Evaluate    float($match.group(1))
    RETURN    ${value}

Extract Value With Unit
    [Documentation]    Extracts a numeric value followed by a specific unit.
    ...    Examples: "25.5 C", "100 m", "9.81 m/s^2"
    [Arguments]    ${text}    ${unit}    ${prefix}=${EMPTY}
    ${escaped_unit}=    Evaluate    re.escape($unit)    re
    ${pattern}=    Set Variable    (?i)(?:${prefix})?\\s*([-+]?[0-9]+(?:\\.[0-9]+)?)\\s*${escaped_unit}
    ${match}=    Evaluate    re.search(r"${pattern}", $text)    re
    Should Not Be Equal    ${match}    ${None}    msg=Could not extract value with unit '${unit}' from: ${text}
    ${value}=    Evaluate    float($match.group(1))
    RETURN    ${value}

Extract Numeric Value After Prefix
    [Documentation]    Extracts numeric value after a prefix, handling colon/equals separators.
    ...    Examples: "Prefix: 123.45" or "Prefix = 123.45" -> 123.45
    [Arguments]    ${text}    ${prefix}
    ${escaped_prefix}=    Evaluate    re.escape($prefix)    re
    # Build pattern and search - note: need 4 backslashes to get 1 in Python regex through Robot
    ${pattern}=    Evaluate    r"(?i)" + $escaped_prefix + r"[:= \t]+\s*([-+]?[0-9]+(?:\.[0-9]+)?)"    re
    ${match}=    Evaluate    re.search($pattern, $text)    re
    Should Not Be Equal    ${match}    ${None}    msg=Could not extract value after prefix '${prefix}' from: ${text}
    ${value}=    Evaluate    float($match.group(1))
    RETURN    ${value}

Wait For Line Matching
    [Documentation]    Waits for a UART line containing the given text.
    [Arguments]    ${text}    ${timeout}=5
    ${line}=    Wait For Line On Uart    ${text}    timeout=${timeout}
    ${line_text}=    Set Variable    ${line['Line']}
    RETURN    ${line_text}
