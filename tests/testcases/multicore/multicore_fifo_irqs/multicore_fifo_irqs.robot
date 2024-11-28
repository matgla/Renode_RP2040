*** Settings ***
Suite Setup         Setup
Suite Teardown      Teardown
Test Teardown       Test Teardown
Test Timeout        90 seconds


*** Test Cases ***
Run successfully 'multicore_fifo_irqs' example
    Execute Command    include @${CURDIR}/multicore_fifo_irqs.resc

    Create Terminal Tester    sysbus.uart0

    Wait For Line On Uart    Hello, multicore_fifo_irqs!    timeout=4
    Wait For Line On Uart    Irq handlers should have rx'd some stuff - core 0 got 123, core 1 got 321!    timeout=1
