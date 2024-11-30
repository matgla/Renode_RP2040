#!/bin/sh
START=`pwd`
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
BOARD=rp2040
if [ ! $# -eq 0 ]
  then 
    BOARD=rp2350
fi


cd $SCRIPT_DIR
revision=`cat ./pico_examples_revision`
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
mkdir build_$BOARD
cd build_$BOARD
PICO_SDK_FETCH_FROM_GIT=1 cmake .. -GNinja -DCMAKE_BUILD_TYPE=Release -DPICO_PLATFORM=$BOARD
cmake --build .
cd ../..
cd $START