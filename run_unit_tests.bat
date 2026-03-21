@echo off
REM I2C Peripheral Unit Test Runner (Windows Batch Wrapper)
REM 
REM This script is a convenience wrapper around run_unit_tests.py
REM 
REM Usage: run_unit_tests.bat [options]
REM Options are passed directly to run_unit_tests.py
REM
REM Examples:
REM   run_unit_tests.bat                    REM Run all tests
REM   run_unit_tests.bat -c                 REM Run only C# tests
REM   run_unit_tests.bat -r -v              REM Run only Renode tests, verbose

cd /d "%~dp0"

REM Check if Python is available
python --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python not found. Please install Python 3.
    exit /b 1
)

REM Run the Python test runner
python run_unit_tests.py %*
