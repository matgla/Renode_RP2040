#!/bin/bash

# Quick smoke test script - runs only essential tests for faster feedback
# Usage: ./run_tests_quick.sh [OPTIONS]
#   --skip-build    Skip building pico-examples

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VENV_DIR="$SCRIPT_DIR/venv"
VENV_PYTHON="$VENV_DIR/bin/python3"

SKIP_BUILD=0

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-build)
            SKIP_BUILD=1
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --skip-build    Skip building pico-examples"
            echo "  -h, --help      Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Check if virtual environment exists
if [ -d "$VENV_DIR" ] && [ -f "$VENV_PYTHON" ]; then
    source "$VENV_DIR/bin/activate"
    PYTHON_CMD="$VENV_PYTHON"
else
    PYTHON_CMD="python3"
fi

# Build unless skipped
if [ "$SKIP_BUILD" -eq 0 ]; then
    ./tests/build_pico_examples.sh
else
    echo "Skipping build (--skip-build specified)"
fi

# Create output directory
OUTPUT_DIR="${SCRIPT_DIR}/output"
mkdir -p "$OUTPUT_DIR"

# Run only essential smoke tests (representative subset)
# These tests cover: GPIO, UART, timer, multicore, DMA, PIO
cat > /tmp/smoke_tests.yaml << 'EOF'
- testcases/blink/blink.robot
- testcases/blink_simple/blink_simple.robot
- testcases/hello_world/serial/hello_serial.robot
- testcases/timer/hello_timer/hello_timer.robot
- testcases/hello_divider/hello_divider.robot
- testcases/dma/hello_dma/hello_dma.robot
- testcases/multicore/hello_multicore/hello_multicore.robot
- testcases/pio/hello_pio/hello_pio.robot
EOF

echo "=========================================="
echo "Running SMOKE TESTS (8 essential tests)"
echo "=========================================="

$PYTHON_CMD -u ./tests/run_tests.py -r 3 -f /tmp/smoke_tests.yaml -o "$OUTPUT_DIR" -j auto
