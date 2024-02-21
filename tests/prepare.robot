*** Settings ***
Resource    ${RENODEKEYWORDS}


*** Keywords ***
Prepare Machine 
    Execute Command     include @${CURDIR}/../initialize_rp2040.repl
    Execute Command     sysbus LoadELF @${TEST_FILE}
    Execute Command     sysbus.cpu0 VectorTableOffset `sysbus GetSymbolAddress "__VECTOR_TABLE"`
    #Execute Command     sysbus.cpu1 VectorTableOffset `sysbus GetSymbolAddress "__VECTOR_TABLE"`

    Create Terminal Tester    sysbus.uart0

    Start Emulation