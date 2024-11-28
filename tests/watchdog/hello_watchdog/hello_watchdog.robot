*** Settings ***
Suite Setup         Setup
Suite Teardown      Teardown
Test Teardown       Test Teardown
Test Timeout        80 seconds


*** Test Cases ***
Run successfully 'hello_watchdog' example
    Execute Command    include @${CURDIR}/hello_watchdog.resc

    Create Terminal Tester    sysbus.uart0

    Wait For Line On Uart    Clean boot    timeout=1
    Wait For Line On Uart    Updating watchdog 0    timeout=1
    Wait For Line On Uart    Updating watchdog 1    timeout=1
    Wait For Line On Uart    Updating watchdog 2    timeout=1
    Wait For Line On Uart    Updating watchdog 3    timeout=1
    Wait For Line On Uart    Updating watchdog 4    timeout=1
    Wait For Line On Uart    Waiting to be rebooted by watchdog    timeout=1
    Wait For Line On Uart    Rebooted by Watchdog!    timeout=1
