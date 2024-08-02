*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    270 seconds

*** Test Cases ***
Run successfully 'detached_clk_peri' example
    Execute Command             include @${CURDIR}/detached_clk_peri.resc
    Execute Command             logLevel -1

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart       Peripheral clock is attached directly to system PLL                 timeout=1
    Wait For Line On Uart       We can vary the system clock divisor while printing from the UART:  timeout=1
    Wait For Line On Uart       Setting system clock divisor to 1   timeout=1
    Wait For Line On Uart       Measuring system clock with frequency counter:133000 kHz   timeout=1
    Wait For Line On Uart       Setting system clock divisor to 2   timeout=1
    Wait For Line On Uart       Measuring system clock with frequency counter:66500 kHz   timeout=1
    Wait For Line On Uart       Setting system clock divisor to 3   timeout=1
    Wait For Line On Uart       Measuring system clock with frequency counter:44333 kHz   timeout=1
    Wait For Line On Uart       Setting system clock divisor to 4   timeout=1
    Wait For Line On Uart       Measuring system clock with frequency counter:33250 kHz   timeout=1
    Wait For Line On Uart       Setting system clock divisor to 5   timeout=1
    Wait For Line On Uart       Measuring system clock with frequency counter:26600 kHz   timeout=1
    Wait For Line On Uart       Setting system clock divisor to 6   timeout=1
    Wait For Line On Uart       Measuring system clock with frequency counter:22166 kHz   timeout=1
    Wait For Line On Uart       Setting system clock divisor to 7   timeout=1
    Wait For Line On Uart       Measuring system clock with frequency counter:19000 kHz   timeout=1
    Wait For Line On Uart       Setting system clock divisor to 8   timeout=1
    Wait For Line On Uart       Measuring system clock with frequency counter:16625 kHz   timeout=1
    Wait For Line On Uart       Setting system clock divisor to 9   timeout=1
    Wait For Line On Uart       Measuring system clock with frequency counter:14777 kHz   timeout=1
    Wait For Line On Uart       Setting system clock divisor to 10   timeout=1
    Wait For Line On Uart       Measuring system clock with frequency counter:13300 kHz   timeout=1
    
