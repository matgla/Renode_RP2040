#!/bin/bash
START=`pwd`
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
echo "Using script directory: $SCRIPT_DIR"

cd $SCRIPT_DIR
revision=`cat $SCRIPT_DIR/pico_examples_revision`
echo "Using Pico Examples revision: $revision"

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
cd $START
