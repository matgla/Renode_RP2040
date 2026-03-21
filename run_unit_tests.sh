#!/bin/bash
# I2C Peripheral Unit Test Runner (Shell Wrapper)
# 
# This script is a convenience wrapper around run_unit_tests.py
# For Windows, use: python run_unit_tests.py
#
# Usage: ./run_unit_tests.sh [options]
# Options are passed directly to run_unit_tests.py
#
# Examples:
#   ./run_unit_tests.sh                    # Run all tests
#   ./run_unit_tests.sh -c                 # Run only C# tests
#   ./run_unit_tests.sh -r -v              # Run only Renode tests, verbose

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Check if Python is available
if command -v python3 &> /dev/null; then
    PYTHON=python3
elif command -v python &> /dev/null; then
    PYTHON=python
else
    echo "ERROR: Python not found. Please install Python 3."
    exit 1
fi

# Run the Python test runner
exec "$PYTHON" "$SCRIPT_DIR/run_unit_tests.py" "$@"
