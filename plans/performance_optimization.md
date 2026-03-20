# Performance Optimization Plan

## Problem

Simple tests take >25s to complete. The simulation has multiple sources of overhead that compound together.

## Root Cause Analysis

| # | Bottleneck | Location | Impact |
|---|-----------|----------|--------|
| 1 | Runtime C# compilation | `cores/initialize_peripherals.resc` loads ~25 `.cs` files compiled on every run | **High** |
| 2 | Visualization always loaded | `boards/initialize_custom_board.resc` unconditionally loads `visualization.py` | **Medium** |
| 3 | No `PerformanceInMips` on main CPUs | `cores/rp2040.repl` — Cortex-M0+ cores use Renode's conservative default | **Medium** |
| 4 | No default quantum for tests | `tests/prepare.resc` has no `SetGlobalQuantum` — each test sets its own or uses Renode default | **Medium** |
| 5 | Global NOISY logging in 2 tests | `hello_serial.resc`, `flash_nuke.resc` use `logLevel -1` globally | **Low-Medium** |

## Action Items

### 1. Pre-compile C# peripherals to DLL

**Files:** `emulation/Peripherals.csproj`, `cores/initialize_peripherals.resc`

- [x] Build peripherals DLL: `dotnet build emulation/Peripherals.csproj -c Release`
- [x] Replace all individual `include .../*.cs` + `EnsureTypeIsLoaded` in `cores/initialize_peripherals.resc` with a single DLL load
- [x] Keep `.cs` source loading available via `cores/initialize_peripherals_source.resc` for development
- [x] Verify tests still pass with DLL-based loading

**Expected saving:** Several seconds per test run (compilation overhead eliminated).

### 2. Skip visualization in test/headless runs

**File:** `boards/initialize_custom_board.resc`

- [x] Remove visualization loading from `initialize_custom_board.resc` (tests/headless runs don't need it)
- [x] Add visualization loading to `run_firmware.resc` (interactive use)
- [x] Verify tests still pass
- [x] Verify visualization still works when explicitly enabled

### 3. Set `PerformanceInMips` on Cortex-M0+ cores

**File:** `cores/rp2040.repl`

- [x] Add `PerformanceInMips: 125` to both `cpu0` and `cpu1` definitions
- [x] Verify tests still pass (timing-sensitive tests may need adjustment)

### 4. Set a relaxed default quantum for tests

**File:** `tests/prepare.resc`

- [x] Add `emulation SetGlobalQuantum "0.0001"` as default
- [x] Individual tests that need tighter sync already override this — no changes needed for them
- [x] Verify tests pass; adjust quantum if needed

### 5. Remove global NOISY logging

**Files:** All test files with `logLevel -1`

- [x] Removed `logLevel -1` from `hello_serial.resc`
- [x] Removed `logLevel -1` from `flash_nuke.resc`
- [x] Removed `logLevel -1` from `button.resc`
- [x] Removed `Execute Command logLevel -1` from 25+ .robot files
- [x] Verified tests still pass

## Implementation Order

1. Items 2, 3, 4, 5 — small, independent changes, easy to validate
2. Item 1 — larger change, requires build pipeline adjustment

## Validation

- Run full test suite after each change
- Compare wall-clock times against profiling baselines in `profiling/`
- Target: simple tests (blink_simple, hello_serial) under 10s
