#!/bin/bash
set -e

START=`pwd`
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
echo "Using script directory: $SCRIPT_DIR"

cd $SCRIPT_DIR
revision=`cat $SCRIPT_DIR/pico_examples_revision`
echo "Using Pico Examples revision: $revision"

# Clone pico-examples if not present
if [ ! -d pico-examples ]; then
    echo "Cloning pico-examples repository..."
    git clone https://github.com/raspberrypi/pico-examples.git
    cd pico-examples
    git checkout $revision
    cd ..
    for i in pico_examples_patches/*.patch; do
        if [ -f "$i" ]; then
            cd pico-examples
            git am < ../${i}
            cd ..
        fi
    done
fi

cd pico-examples

# Check if already at correct revision (only if git repo is clean)
if [ -d .git ]; then
    current_rev=$(git rev-parse --short HEAD 2>/dev/null || echo "unknown")
    target_rev=$(git rev-parse --short $revision 2>/dev/null || echo "target")
    if [ "$current_rev" != "$target_rev" ]; then
        # Only try to update if working directory is clean
        if git diff --quiet 2>/dev/null; then
            echo "Updating pico-examples to revision $revision..."
            git fetch origin 2>/dev/null || true
            git checkout $revision 2>/dev/null || echo "Warning: Could not checkout $revision, using current"
        else
            echo "Warning: pico-examples has local changes, not updating"
        fi
    fi
fi

# Create build directory if not exists
mkdir -p build
cd build

# Configure only if not already configured
if [ ! -f build.ninja ]; then
    echo "Configuring pico-examples build..."
    PICO_SDK_FETCH_FROM_GIT=1 cmake .. -GNinja -DCMAKE_BUILD_TYPE=Release -DPICO_BOARD=pico
fi

# Build only modified targets (incremental build)
echo "Building pico-examples (incremental)..."
cmake --build . --parallel

cd ../..
cd $START
echo "Build completed successfully"
