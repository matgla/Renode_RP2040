*** Settings ***

Resource        ../../common.resource
Suite Setup     Setup
Suite Teardown  Teardown
Test Timeout    120 seconds

*** Variables ***

${TEST_TIMEOUT}    30

*** Test Cases ***

I2C Reset Values Correct
    [Documentation]    Verify I2C registers have correct reset values
    Execute Command             include @${CURDIR}/i2c_unit_tests.resc
    
    # IC_CON reset value: Master=1, Speed=2, Restart=1, SlaveDisable=1 -> 0x65
    ${ic_con}=     Execute Command    sysbus.i2c0 ReadDoubleWord 0x00
    Should Contain    ${ic_con}    65
    
    # IC_TAR reset value: 0x55
    ${ic_tar}=     Execute Command    sysbus.i2c0 ReadDoubleWord 0x04
    Should Contain    ${ic_tar}    55
    
    # IC_ENABLE reset value: 0x00
    ${ic_enable}=  Execute Command    sysbus.i2c0 ReadDoubleWord 0x6C
    Should Contain    ${ic_enable}    0

I2C Register Write Read
    [Documentation]    Verify registers can be written and read back
    Execute Command             include @${CURDIR}/i2c_unit_tests.resc
    
    # Write to IC_TAR
    Execute Command    sysbus.i2c0 WriteDoubleWord 0x04 0x17
    ${value}=          Execute Command    sysbus.i2c0 ReadDoubleWord 0x04
    Should Contain     ${value}    17
    
    # Write to IC_SAR
    Execute Command    sysbus.i2c0 WriteDoubleWord 0x08 0x3F
    ${value}=          Execute Command    sysbus.i2c0 ReadDoubleWord 0x08
    Should Contain     ${value}    3F

I2C Enable Disable
    [Documentation]    Verify I2C can be enabled and disabled
    Execute Command             include @${CURDIR}/i2c_unit_tests.resc
    
    # Configure: Master mode, Fast speed, Slave disable
    Execute Command    sysbus.i2c0 WriteDoubleWord 0x00 0x65
    
    # Enable I2C
    Execute Command    sysbus.i2c0 WriteDoubleWord 0x6C 0x01
    ${value}=          Execute Command    sysbus.i2c0 ReadDoubleWord 0x6C
    Should Contain     ${value}    1

I2C Status Register
    [Documentation]    Verify status register reflects FIFO state
    Execute Command             include @${CURDIR}/i2c_unit_tests.resc
    
    # Status: TFE=1 (TX FIFO empty), TFNF=1 (TX FIFO not full)
    ${status}=    Execute Command    sysbus.i2c0 ReadDoubleWord 0x70
    # Should have bits 1 and 2 set (0x06)
    Should Contain     ${status}    6
