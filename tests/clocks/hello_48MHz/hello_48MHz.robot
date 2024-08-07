*** Settings ***

Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    270 seconds

*** Test Cases ***
Run successfully 'hello_48MHz' example
    Execute Command             include @${CURDIR}/hello_48MHz.resc
    Execute Command             logLevel -1

    Create Terminal Tester      sysbus.uart0

    Wait For Line On Uart           Hello, world!           timeout=1
    Wait For Line On Uart           pll_sys${SPACE} = 125000kHz    timeout=1 
    Wait For Line On Uart           pll_usb${SPACE} = 48000kHz     timeout=1 
    ${l}    Wait For Next Line On Uart                      timeout=1 
    ${rosc_freq}=    Evaluate    int(re.search("\\d+","${l.line}")[0])     modules=re
    Should Be True      ${rosc_freq} <= 13000 and ${rosc_freq} >= 1000
    Wait For Line On Uart           clk_sys${SPACE} = 125000kHz     timeout=1 
    Wait For Line On Uart           clk_peri = 125000kHz     timeout=1 
    Wait For Line On Uart           clk_usb${SPACE} = 48000kHz     timeout=1 
    Wait For Line On Uart           clk_adc${SPACE} = 48000kHz     timeout=1 
    Wait For Line On Uart           clk_rtc${SPACE} = 46kHz     timeout=1 

    Wait For Line On Uart           pll_sys${SPACE} = 125000kHz    timeout=1 
    Wait For Line On Uart           pll_usb${SPACE} = 48000kHz     timeout=1 
    ${l}    Wait For Next Line On Uart                      timeout=1 
    ${rosc_freq}=    Evaluate    int(re.search("\\d+","${l.line}")[0])     modules=re
    Should Be True      ${rosc_freq} <= 13000 and ${rosc_freq} >= 1000
    Wait For Line On Uart           clk_sys${SPACE} = 48000kHz     timeout=1 
    Wait For Line On Uart           clk_peri = 48000kHz     timeout=1 
    Wait For Line On Uart           clk_usb${SPACE} = 48000kHz     timeout=1 
    Wait For Line On Uart           clk_adc${SPACE} = 48000kHz     timeout=1 
    Wait For Line On Uart           clk_rtc${SPACE} = 46kHz     timeout=1 
    
    Wait For Line On Uart           Hello, 48MHz           timeout=1
 
