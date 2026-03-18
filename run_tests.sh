#!/bin/bash

# Script to run Renode RP2040 tests
# Uses virtual environment if available, otherwise falls back to system Python

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VENV_DIR="$SCRIPT_DIR/venv"
VENV_PYTHON="$VENV_DIR/bin/python3"

# Default values
SKIP_BUILD=0
JOBS=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-build)
            SKIP_BUILD=1
            shift
            ;;
        -j|--jobs)
            JOBS="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --skip-build    Skip building pico-examples (use cached binaries)"
            echo "  -j, --jobs N    Number of parallel test jobs (default: auto = physical CPU cores)"
            echo "  -h, --help      Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use -h or --help for usage information"
            exit 1
            ;;
    esac
done

# Check if virtual environment exists and activate it
if [ -d "$VENV_DIR" ] && [ -f "$VENV_PYTHON" ]; then
    echo "Using virtual environment at $VENV_DIR"
    source "$VENV_DIR/bin/activate"
    PYTHON_CMD="$VENV_PYTHON"
else
    echo "Warning: Virtual environment not found at $VENV_DIR"
    echo "Using system Python. For better isolation, run ./setup_venv.sh first"
    echo ""
    PYTHON_CMD="python3"
fi

# Build pico-examples unless skipped
if [ "$SKIP_BUILD" -eq 0 ]; then
    ./tests/build_pico_examples.sh
else
    echo "Skipping build (--skip-build specified)"
fi

# Create output directory for test results
OUTPUT_DIR="${SCRIPT_DIR}/output"
mkdir -p "$OUTPUT_DIR"
echo "Test output will be stored in: $OUTPUT_DIR"

# Build Python arguments
PYTHON_ARGS="-u ./tests/run_tests.py -r 3 -f tests/tests.yaml -o \"$OUTPUT_DIR\""

# Add jobs argument if specified
if [ -n "$JOBS" ]; then
    PYTHON_ARGS="$PYTHON_ARGS -j $JOBS"
fi

eval $PYTHON_CMD $PYTHON_ARGS