*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    120 seconds

Resource    ${CURDIR}/../../../common.resource

*** Test Cases ***
Run successfully 'read_vsys' example
    Execute Command             include @${CURDIR}/read_vsys.resc


    Create Terminal Tester      sysbus.uart0

    Execute Command           sysbus.adc SetOnboardTemperature 27.8
    ${l}     Wait For Next Line On Uart    timeout=1
    ${text}=    Set Variable    ${l['Line']}
    Should Start With    ${text}    Power BATTERY,
    Text Numeric Value Should Be    ${text}    Power BATTERY,    0.59    0.01
    Text Numeric Value Should Be    ${text}    temp    27.8    0.1

    Execute Command           sysbus.adc SetOnboardTemperature 40.2
    Execute Command           sysbus.adc SetDefaultVoltageOnChannel 3 0.5

    ${l}     Wait For Next Line On Uart    timeout=10
    ${text}=    Set Variable    ${l['Line']}
    Should Start With    ${text}    Power BATTERY,
    Text Numeric Value Should Be    ${text}    Power BATTERY,    1.49    0.01
    Text Numeric Value Should Be    ${text}    temp    40.2    0.1


    Execute Command           sysbus.adc SetDefaultVoltageOnChannel 3 1.0
    Execute Command           sysbus.gpio WritePin 24 true
    ${l}     Wait For Next Line On Uart    timeout=10
    ${text}=    Set Variable    ${l['Line']}
    Should Start With    ${text}    Power POWERED,
    Text Numeric Value Should Be    ${text}    Power POWERED,    2.99    0.01
    Text Numeric Value Should Be    ${text}    temp    40.2    0.1


