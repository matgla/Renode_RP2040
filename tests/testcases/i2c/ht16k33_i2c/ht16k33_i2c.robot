*** Settings ***

Resource        ../../../common.resource
Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    60 seconds

*** Test Cases ***
Run successfully 'ht16k33_i2c' example
    Execute Command             include @${CURDIR}/ht16k33_i2c.resc
    Register HT16K33 Sniffer

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Welcome to HT33k16!               timeout=5
    HT16K33 Should Receive Write    [["0x21"]]    HT16K33 should receive the system run command
    HT16K33 Should Receive Write    [["0xA0"]]    HT16K33 should receive the row/int configuration command
    HT16K33 Should Receive Write    [["0x81"]]    HT16K33 should receive the display enable command
    HT16K33 Should Receive Write    [["0x00", "0x36", "0x28"]]    HT16K33 should receive a display payload for the first character of the welcome scroll    5
    HT16K33 Should Capture Display Payload

    Log                         HT16K33 I2C test completed successfully with verified I2C traffic

*** Keywords ***
Register HT16K33 Sniffer
    [Timeout]    10 seconds
    Log    Registering and clearing HT16K33 sniffer
    Execute Command             python "mc_RegisterI2CCaptureTester('ht16k33_sniffer', 'sysbus.i2c0.ht16k33')"
    Execute Command             python "mc_ClearI2CCapturedData('ht16k33_sniffer')"

HT16K33 Should Receive Write
    [Arguments]    ${expected_data}    ${message}    ${timeout}=2
    [Timeout]    10 seconds
    Log    Waiting for HT16K33 write: ${expected_data}
    ${python_expected_data}=    Replace String    ${expected_data}    "    '
    Execute Command             python "mc_AssertI2CData('ht16k33_sniffer', ${python_expected_data}, ${timeout})"

HT16K33 Should Capture Display Payload
    [Timeout]    10 seconds
    Log    Reading captured HT16K33 writes
    Execute Command             python "mc_AssertAnyCapturedWriteLength('ht16k33_sniffer', 3)"
