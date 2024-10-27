*** Settings ***
Suite Setup         Setup
Suite Teardown      Teardown
Test Teardown       Test Teardown
Test Timeout        15 seconds


*** Test Cases ***
Run successfully 'multicore_runner' example
    Execute Command    include @${CURDIR}/multicore_runner.resc

    Create Terminal Tester    sysbus.uart0

    Wait For Line On Uart    Hello, multicore_runner!    timeout=4
    Wait For Line On Uart    Factorial 10 is 3628800    timeout=1
    Wait For Line On Uart    Fibonacci 10 is 55    timeout=1