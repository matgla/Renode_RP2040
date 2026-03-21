*** Settings ***

Resource        ../../../common.resource
Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    60 seconds

*** Variables ***
${UART_TIMEOUT}              10

*** Test Cases ***
Run successfully 'slave_mem_i2c_burst' example with burst I2C master/slave communication
    Start SlaveMemI2C Burst Example
    
    # Verify first write at address 0x00
    ${line}=    Wait For Line On Uart    Write at 0x00:    timeout=${UART_TIMEOUT}
    Should Contain    ${line['Line']}    Hello, I2C slave! - 0x00
    
    # Burst mode reads the entire message at once (different from regular mode)
    ${line}=    Wait For Line On Uart    0x00:    timeout=${UART_TIMEOUT}
    Should Contain    ${line['Line']}    Hello, I2C slave! - 0x00
    
    # Wait for and verify second cycle at address 0x20
    ${line}=    Wait For Line On Uart    Write at 0x20:    timeout=${UART_TIMEOUT}
    Should Contain    ${line['Line']}    Hello, I2C slave! - 0x20
    
    # Verify burst read at 0x20
    ${line}=    Wait For Line On Uart    0x20:    timeout=${UART_TIMEOUT}
    Should Contain    ${line['Line']}    Hello, I2C slave! - 0x20

*** Keywords ***
Start SlaveMemI2C Burst Example
    Execute Command             include @${CURDIR}/slave_mem_i2c_burst.resc
    Create Terminal Tester      sysbus.uart0
    Wait For Line On Uart       I2C slave example    timeout=${UART_TIMEOUT}
