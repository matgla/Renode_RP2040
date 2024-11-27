#!/bin/sh

if [ ! -d pico-examples ]; then
    git clone https://github.com/raspberrypi/pico-examples.git
    git checkout 7e77a0c381863be0c49086567e7f1934d78ac591
    for i in pico_examples_patches/*.patch; do
        cd pico-examples 
        git am < ../${i}
        cd ..
    done
fi

cd pico-examples
mkdir build
cd build
PICO_SDK_FETCH_FROM_GIT=1 cmake .. -GNinja -DCMAKE_BUILD_TYPE=Release -DPICO_BOARD=pico
cmake --build .
cd ../..

renode-test -t tests/tests.yaml

