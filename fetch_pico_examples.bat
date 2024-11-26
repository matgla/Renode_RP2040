@echo off

set directory=pico-examples
if not exist pico-examples (
    git clone https://github.com/raspberrypi/pico-examples.git
    git checkout 7e77a0c381863be0c49086567e7f1934d78ac591
    for %%f in (pico_examples_patches/*.patch) do (
        cd pico-examples 
        git am < ../pico_examples_patches/%%f
        cd ..
    )
)

cd pico-examples 
