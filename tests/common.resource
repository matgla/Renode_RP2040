*** Keywords ***

Should Be Equal As Numbers With Tolerance
    [Arguments]   ${number1}  ${number2}  ${tolerance}=0.001
    ${result}=    Evaluate    math.isclose(${number1}, ${number2}, rel_tol=${tolerance})    math
    Should Be True    ${result}

Read Numeric Value From Uart
    [Arguments]    ${prefix}    ${timeout}=5
    ${line}=    Wait For Line On Uart    ${prefix}    timeout=${timeout}
    ${text}=    Set Variable    ${line['Line']}
    ${value}=    Extract Numeric Value From Text    ${text}    ${prefix}
    RETURN    ${value}

Read Numeric Value From Next Uart Line
    [Arguments]    ${prefix}=${EMPTY}    ${timeout}=5
    ${line}=    Wait For Next Line On Uart    timeout=${timeout}
    ${text}=    Set Variable    ${line['Line']}
    ${value}=    Extract Numeric Value From Text    ${text}    ${prefix}
    RETURN    ${value}

Extract Numeric Value From Text
    [Arguments]    ${text}    ${prefix}=${EMPTY}
    ${pattern}=    Evaluate    re.escape($prefix) + r"\\s*([-+]?[0-9]+(?:\\.[0-9]+)?)" if $prefix else r"([-+]?[0-9]+(?:\\.[0-9]+)?)"    re
    ${match}=    Evaluate    re.search($pattern, $text)    re
    Should Not Be Equal    ${match}    ${None}
    ${value}=    Evaluate    float($match.group(1))
    RETURN    ${value}

Text Numeric Value Should Be
    [Arguments]    ${text}    ${prefix}    ${expected}    ${tolerance}=0.001
    ${value}=    Extract Numeric Value From Text    ${text}    ${prefix}
    Should Be Equal As Numbers With Tolerance    ${value}    ${expected}    ${tolerance}

Text Numeric Value Should Be In Range
    [Arguments]    ${text}    ${prefix}    ${minimum}    ${maximum}
    ${value}=    Extract Numeric Value From Text    ${text}    ${prefix}
    Should Be True    ${value} >= ${minimum} and ${value} <= ${maximum}

Uart Numeric Value Should Be
    [Arguments]    ${prefix}    ${expected}    ${tolerance}=0.001    ${timeout}=5
    ${value}=    Read Numeric Value From Uart    ${prefix}    ${timeout}
    Should Be Equal As Numbers With Tolerance    ${value}    ${expected}    ${tolerance}

Uart Numeric Value Should Be In Range
    [Arguments]    ${prefix}    ${minimum}    ${maximum}    ${timeout}=5
    ${value}=    Read Numeric Value From Uart    ${prefix}    ${timeout}
    Should Be True    ${value} >= ${minimum} and ${value} <= ${maximum}

Uart Next Numeric Value Should Be
    [Arguments]    ${expected}    ${tolerance}=0.001    ${timeout}=5    ${prefix}=${EMPTY}
    ${value}=    Read Numeric Value From Next Uart Line    ${prefix}    ${timeout}
    Should Be Equal As Numbers With Tolerance    ${value}    ${expected}    ${tolerance}

Uart Next Numeric Value Should Be In Range
    [Arguments]    ${minimum}    ${maximum}    ${timeout}=5    ${prefix}=${EMPTY}
    ${value}=    Read Numeric Value From Next Uart Line    ${prefix}    ${timeout}
    Should Be True    ${value} >= ${minimum} and ${value} <= ${maximum}


