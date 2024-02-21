*** Settings ***
Resource    ../../prepare.robot

Suite Setup     Setup 
Suite Teardown  Teardown
Test Teardown   Test Teardown 
Test Timeout     10 seconds


*** Variables ***
${TEST_FILE}    ${CURDIR}/../../../pico-examples/build/timer/hello_timer/hello_timer.elf

*** Test Cases ***
Run successfully 'hello_timer' example 
    Prepare Machine

    Wait For Line On Uart    Hello Timer!    timeout=5