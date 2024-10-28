*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    40 seconds

Resource    ${CURDIR}/../../common.resource

*** Test Cases ***
Run successfully 'read_vsys' example
    Execute Command             include @${CURDIR}/read_vsys.resc
    Execute Command             logLevel -1
    
    Create Terminal Tester      sysbus.uart0

    Execute Command           sysbus.adc SetOnboardTemperature 27.8
    ${l}     Wait For Next Line On Uart    timeout=1
    @{elements}     Split String     ${l.line}
    Should Be Equal As Numbers With Tolerance    ${elements}[5]   27.8   0.1
    Should Be Equal As Strings    ${elements}[2]   0.59V 
    Should Be Equal As Strings    ${elements}[1]   BATTERY, 
    
    Execute Command           sysbus.adc SetOnboardTemperature 40.2
    Execute Command           sysbus.adc SetDefaultVoltageOnChannel 3 0.5

    ${l}     Wait For Next Line On Uart    timeout=10
    @{elements}     Split String     ${l.line}
    Should Be Equal As Numbers With Tolerance    ${elements}[5]   40.2   0.1
    Should Be Equal As Strings    ${elements}[2]   1.49V 
    Should Be Equal As Strings    ${elements}[1]   BATTERY, 

 
    Execute Command           sysbus.adc SetDefaultVoltageOnChannel 3 1.0
    Execute Command           sysbus.gpio WritePin 24 true
    ${l}     Wait For Next Line On Uart    timeout=10
    @{elements}     Split String     ${l.line}
    Should Be Equal As Numbers With Tolerance    ${elements}[4]   40.2   0.1
    Should Be Equal As Strings    ${elements}[2]   2.99V,
    Should Be Equal As Strings    ${elements}[1]   POWERED, 
  

