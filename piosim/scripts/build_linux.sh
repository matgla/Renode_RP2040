#!/bin/bash
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )/..
docker run --rm -v $SCRIPT_DIR:/workdir multiarch/crossbuild sh -c "ls -la && mkdir -p build_linux && cd build_linux && cmake .. -DCMAKE_BUILD_TYPE=Release && cmake --build ." 
