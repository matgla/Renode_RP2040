#!/bin/bash

# Run a single test or tests matching a pattern
# Usage: ./run_single_test.sh <test_name_or_pattern> [--skip-build]
# Examples:
#   ./run_single_test.sh blink
#   ./run_single_test.sh dma
#   ./run_single_test.sh blink --skip-build

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VENV_DIR="$SCRIPT_DIR/venv"
VENV_PYTHON="$VENV_DIR/bin/python3"

if [ $# -lt 1 ]; then
    echo "Usage: $0 <test_name_or_pattern> [--skip-build]"
    echo ""
    echo "Examples:"
    echo "  $0 blink              # Run tests matching 'blink'"
    echo "  $0 hello_dma          # Run specific test"
    echo "  $0 dma --skip-build   # Run DMA tests, skip build"
    exit 1
fi

PATTERN="$1"
SKIP_BUILD=0

# Check for --skip-build in remaining args
for arg in "${@:2}"; do
    if [ "$arg" = "--skip-build" ]; then
        SKIP_BUILD=1
    fi
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
    echo "Building RP2040 peripherals DLL"
    dotnet build ./emulation/Peripherals.csproj -c Release
    ./tests/build_pico_examples.sh
else
    echo "Skipping DLL and pico-examples build (--skip-build specified)"
fi

# Find matching tests
OUTPUT_DIR="${SCRIPT_DIR}/output"
mkdir -p "$OUTPUT_DIR"

# Search for matching tests
echo "Searching for tests matching: $PATTERN"
MATCHING_TESTS=$(grep -i "^- .*${PATTERN}.*\.robot$" tests/tests.yaml || true)

if [ -z "$MATCHING_TESTS" ]; then
    # Try searching in test file paths
    MATCHING_TESTS=$(find tests/testcases -name "*.robot" | grep -i "$PATTERN" | sed 's|^tests/||' | sed 's|^|- |' || true)
fi

if [ -z "$MATCHING_TESTS" ]; then
    echo "No tests found matching: $PATTERN"
    exit 1
fi

# Create temporary test list
TEMP_TEST_LIST=$(mktemp)
trap 'rm -f "$TEMP_TEST_LIST"' EXIT

printf '%s\n' "$MATCHING_TESTS" > "$TEMP_TEST_LIST"
NUM_TESTS=$(wc -l < "$TEMP_TEST_LIST")
echo "Found $NUM_TESTS matching test(s):"
echo "$MATCHING_TESTS"
echo ""

$PYTHON_CMD -u ./tests/run_tests.py -r 3 -f "$TEMP_TEST_LIST" -o "$OUTPUT_DIR" -j 1
