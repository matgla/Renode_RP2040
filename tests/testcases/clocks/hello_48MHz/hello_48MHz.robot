*** Settings ***

Resource        ../../../common.resource

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    100 seconds

*** Test Cases ***
Run successfully 'hello_48MHz' example
    Execute Command             include @${CURDIR}/hello_48MHz.resc


    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart           Hello, world!           timeout=1
    Wait For Line On Uart           pll_sys${SPACE} = 125000kHz    timeout=1
    Wait For Line On Uart           pll_usb${SPACE} = 48000kHz     timeout=1
    Uart Next Numeric Value Should Be In Range    1000    13000    1
    Wait For Line On Uart           clk_sys${SPACE} = 125000kHz     timeout=1
    Wait For Line On Uart           clk_peri = 125000kHz     timeout=1
    Wait For Line On Uart           clk_usb${SPACE} = 48000kHz     timeout=1
    Wait For Line On Uart           clk_adc${SPACE} = 48000kHz     timeout=1
    Wait For Line On Uart           clk_rtc${SPACE} = 46kHz     timeout=1

    Wait For Line On Uart           pll_sys${SPACE} = 125000kHz    timeout=1
    Wait For Line On Uart           pll_usb${SPACE} = 48000kHz     timeout=1
    Uart Next Numeric Value Should Be In Range    1000    13000    1
    Wait For Line On Uart           clk_sys${SPACE} = 48000kHz     timeout=1
    Wait For Line On Uart           clk_peri = 48000kHz     timeout=1
    Wait For Line On Uart           clk_usb${SPACE} = 48000kHz     timeout=1
    Wait For Line On Uart           clk_adc${SPACE} = 48000kHz     timeout=1
    Wait For Line On Uart           clk_rtc${SPACE} = 46kHz     timeout=1

    Wait For Line On Uart           Hello, 48MHz           timeout=1

