*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    60 seconds

*** Test Cases ***
Run successfully 'ht16k33_i2c' example
    # NOTE: This test is a placeholder. The HT16K33 emulation is implemented
    # but the I2C capture tester needs additional work to verify I2C commands.
    # The HT16K33 peripheral is registered at sysbus.i2c0.ht16k33 (address 0x70).
    Execute Command             include @${CURDIR}/ht16k33_i2c.resc

    Create Terminal Tester      sysbus.uart0

    # Wait for the welcome message
    # The firmware prints "Welcome to HT33k16!" on startup
    Wait For Line On Uart       Welcome to HT33k16!               timeout=5
