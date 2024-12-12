*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    100 seconds

Resource    ${CURDIR}/../../../common.resource

*** Test Cases ***
Run successfully 'onboard_temperature' example
    Execute Command             include @${CURDIR}/onboard_temperature.resc
    Execute Command             logLevel -1
    
    Create Terminal Tester      sysbus.uart0

    Execute Command           sysbus.adc SetOnboardTemperature 27.8
    ${l}     Wait For Next Line On Uart    timeout=2
    @{elements}     Split String     ${l.line}
    Should Be Equal As Numbers With Tolerance    ${elements}[3]   27.8   0.1 

    Execute Command           sysbus.adc SetOnboardTemperature 40.2
    ${l}     Wait For Next Line On Uart    timeout=2
    @{elements}     Split String     ${l.line}
    Should Be Equal As Numbers With Tolerance    ${elements}[3]   40.2   0.1 



  
