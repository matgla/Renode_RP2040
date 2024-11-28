#!/bin/sh

revision=`cat ./tests/pico_examples_revision`
echo "$revision"

if [ ! -d pico-examples ]; then
    git clone https://github.com/raspberrypi/pico-examples.git
    git checkout $value
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
