#!/bin/sh

if [ ! -d pico-examples ]; then
    git clone https://github.com/raspberrypi/pico-examples.git
fi

cd pico-examples
mkdir build
cd build
PICO_SDK_FETCH_FROM_GIT=1 cmake ..
make -j$(nproc)
cd ../..
renode-test -t tests/tests.yaml

