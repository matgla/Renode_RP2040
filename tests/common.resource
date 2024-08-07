*** Keywords ***

Should Be Equal As Numbers With Tolerance
    [Arguments]   ${number1}  ${number2}  ${tolerance}=0.001
    ${result}=    Evaluate    math.isclose(${number1}, ${number2}, rel_tol=${tolerance})    math
    Should Be True    ${result}


