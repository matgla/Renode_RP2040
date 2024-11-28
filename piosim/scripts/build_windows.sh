#!/bin/bash
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )/..
docker run --rm -v $SCRIPT_DIR:/workdir dockcross/windows-shared-x64 sh -c "cd /workdir && ls -la && mkdir -p build_windows && cd build_windows && cmake .. -DCMAKE_BUILD_TYPE=Release && cmake --build ." 
	