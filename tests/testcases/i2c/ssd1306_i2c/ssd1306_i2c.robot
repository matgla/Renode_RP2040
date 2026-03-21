*** Settings ***

Resource        ../../../common.resource
Suite Setup     Setup
Suite Teardown  Teardown
Test Teardown   Test Teardown
Test Timeout    60 seconds

*** Variables ***
${UART_TIMEOUT}              5
${REF_IMAGE_DIR}             ${CURDIR}/reference_images
${OUTPUT_DIR}                ${CURDIR}/output_images
${SCRIPT_DIR}                ${CURDIR}

*** Test Cases ***
Run successfully 'ssd1306_i2c' example with OLED display rendering
    Start SSD1306 Example
    
    # Wait for intro flash sequence to complete (3 flashes = ~3 seconds)
    # The display shows raspberries after the flash sequence
    Sleep                       5s
    
    # Capture and verify raspberry display
    SSD1306 Display Should Match Reference    raspberry_render
    
    # Continue to text rendering phase (5 second scroll + text render)
    Sleep                       8s
    
    # Capture and verify text display  
    SSD1306 Display Should Match Reference    text_render
    
    # Verify display is on
    SSD1306 Display Should Be On

*** Keywords ***
Start SSD1306 Example
    Create Directory            ${OUTPUT_DIR}
    Execute Command             include @${CURDIR}/ssd1306_i2c.resc
    Create Terminal Tester      sysbus.uart0
    Wait For Line On Uart       Hello, SSD1306 OLED display!    timeout=${UART_TIMEOUT}

SSD1306 Display Should Match Reference
    [Arguments]    ${image_name}
    [Documentation]    Captures display and compares with reference image.
    ...    If reference doesn't exist, creates it as a seed.
    
    ${output_path}=             Set Variable    ${OUTPUT_DIR}/${image_name}.bmp
    ${ref_path}=                Set Variable    ${REF_IMAGE_DIR}/${image_name}.bmp
    
    # Capture display
    Execute Command             sysbus.i2c0.ssd1306 SaveAsBmp @${output_path}
    File Should Exist           ${output_path}
    
    # Check if reference exists - if not, copy output as reference (seed mode)
    ${ref_exists}=              Run Keyword And Return Status    File Should Exist    ${ref_path}
    IF    not ${ref_exists}
        Log                     Reference image not found, creating from output (SEED MODE)    level=WARN
        Copy File               ${output_path}    ${ref_path}
        File Should Exist       ${ref_path}
        RETURN
    END
    
    # Compare images using helper script
    ${result}=                  Run Process    python3    ${SCRIPT_DIR}/compare_images.py    ${ref_path}    ${output_path}
    Should Be Equal As Integers    ${result.rc}    0    msg=Display does not match reference: ${image_name}

SSD1306 Display Should Be On
    ${display_on}=              Execute Command    sysbus.i2c0.ssd1306 IsDisplayOn
    Should Contain              ${display_on}    True

SSD1306 Save Display
    [Arguments]    ${filename}
    [Documentation]    Saves current display to output directory with given name.
    Execute Command             sysbus.i2c0.ssd1306 SaveAsBmp @${OUTPUT_DIR}/${filename}.bmp
