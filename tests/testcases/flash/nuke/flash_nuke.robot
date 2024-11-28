*** Settings ***
Library             Collections
Library             Telnet

Suite Setup         Setup
Suite Teardown      Teardown
Test Teardown       Test Teardown
Test Timeout        80 seconds


*** Test Cases ***
Run successfully 'flash_nuke' example
    Execute Command    include @${CURDIR}/flash_nuke.resc

    Create Terminal Tester    sysbus.uart0

    Create LED Tester    sysbus.gpio.led

    Assert LED Is Blinking    testDuration=0.4    onDuration=0.1    offDuration=0.1

    # Until USB is not implemented, let's just check if code went back to bootrom
    Wait Until Keyword Succeeds    1 min    1 sec    PC Should Be Less Than    0x00004000


*** Keywords ***
PC Should Be Less Than
    [Arguments]    ${address}
    ${pc}=    Execute Command    sysbus.cpu0 PC
    ${pc}=    Convert To Integer    ${pc}
    ${address}=    Convert To Integer    ${address}
    Should Be True    ${pc}<${address}
