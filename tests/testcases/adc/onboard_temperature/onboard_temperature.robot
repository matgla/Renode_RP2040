*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    100 seconds

Resource    ${CURDIR}/../../../common.resource

*** Test Cases ***
Run successfully 'onboard_temperature' example
    Execute Command             include @${CURDIR}/onboard_temperature.resc


    Create Terminal Tester      sysbus.uart0

    Execute Command           sysbus.adc SetOnboardTemperature 27.8
    ${l}     Wait For Next Line On Uart    timeout=2
    Text Numeric Value Should Be    ${l['Line']}    Onboard temperature =    27.8    0.1

    Execute Command           sysbus.adc SetOnboardTemperature 40.2
    ${l}     Wait For Next Line On Uart    timeout=2
    Text Numeric Value Should Be    ${l['Line']}    Onboard temperature =    40.2    0.1




