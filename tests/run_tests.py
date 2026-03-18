#!/usr/bin/python3

import os
from pathlib import Path
from subprocess import run
from argparse import ArgumentParser
import sys
import shutil
import concurrent.futures
from concurrent.futures import ProcessPoolExecutor, wait
from collections import deque
import multiprocessing
import time
import subprocess


def get_physical_cores():
    """Get number of physical CPU cores (excluding hyperthreading)."""
    try:
        # Try using lscpu first (most reliable)
        result = subprocess.run(['lscpu', '-p=Core'], capture_output=True, text=True)
        if result.returncode == 0:
            # Count unique core IDs (excluding header lines starting with #)
            cores = set()
            for line in result.stdout.strip().split('\n'):
                if line and not line.startswith('#'):
                    cores.add(line.strip())
            return len(cores)
    except FileNotFoundError:
        pass

    try:
        # Fallback: parse /proc/cpuinfo
        with open('/proc/cpuinfo', 'r') as f:
            cores = set()
            current_physical_id = None
            current_core_id = None
            for line in f:
                if line.startswith('physical id'):
                    current_physical_id = line.split(':')[1].strip()
                elif line.startswith('core id'):
                    current_core_id = line.split(':')[1].strip()
                elif line.strip() == '' and current_physical_id is not None and current_core_id is not None:
                    cores.add(f"{current_physical_id}:{current_core_id}")
                    current_physical_id = None
                    current_core_id = None
            if cores:
                return len(cores)
    except Exception:
        pass

    # Final fallback: use half of logical cores (typical HT ratio)
    logical = multiprocessing.cpu_count()
    return max(1, logical // 2)


def resolve_test_file(script_dir, file_argument):
    if file_argument is None:
        return script_dir / "tests.yaml"

    candidate = Path(file_argument)
    if not candidate.is_absolute():
        candidate = Path.cwd() / candidate

    return candidate.resolve()


def load_tests(script_dir, test_file):
    tests_to_run = []

    with open(test_file, "r") as file:
        for raw_line in file.readlines():
            line = raw_line.strip()
            if not line or line.startswith("#"):
                continue

            if not line.startswith("-"):
                continue

            relative_path = line.removeprefix("-").strip()
            tests_to_run.append(str((script_dir / relative_path).resolve()))

    return tests_to_run


def run_test(command, test, retries, output_dir=None):
    """Run a single test with retries."""
    # Pass current environment to subprocess
    env = os.environ.copy()

    passed = False
    output_buffer = []
    attempts = 0
    test_start = time.time()

    for attempt in range(1, retries + 1):
        attempts = attempt
        cmd = [command, test]
        if output_dir:
            cmd.extend(['-r', output_dir])

        # Capture output to avoid interleaving
        result = run(cmd, env=env, capture_output=True, text=True)

        if result.stdout:
            output_buffer.append(result.stdout)
        if result.stderr:
            output_buffer.append(result.stderr)

        if result.returncode == 0:
            passed = True
            break

    elapsed = time.time() - test_start
    return passed, test, output_buffer, elapsed, attempts


def main():
    parser = ArgumentParser()
    parser.add_argument("-f", "--file", help="Path to yaml file with tests")
    parser.add_argument("-r", "--retry", type=int, default=1, help="Number of retries of tests if failed")
    parser.add_argument("-e", "--renode_test", default="renode-test", help="renode-test executable path")
    parser.add_argument("-j", "--threads", type=int, default=None, help="Number of parallel jobs for tests (0 for sequential, default is physical CPU cores)")
    parser.add_argument("-o", "--output", default=None, help="Output directory for test results")
    parser.add_argument("-v", "--verbose", action="store_true", help="Verbose output")

    args, _ = parser.parse_known_args()

    script_dir = Path(os.path.dirname(os.path.realpath(__file__)))
    test_file = resolve_test_file(script_dir, args.file)
    print("Running tests from:", test_file, flush=True)
    runner = shutil.which(args.renode_test)
    print("Using runner:", runner, flush=True)
    failed_tests=0
    passed_tests=0
    failed_names=[]
    physical_cores = get_physical_cores()

    # Determine thread count
    thread_count = args.threads
    if thread_count is None:
        thread_count = physical_cores
        print(f"Using parallel jobs: {thread_count} (physical cores)", flush=True)
    else:
        print("Using parallel jobs:", thread_count, flush=True)

    # Track timing
    start_time = time.time()

    # Create output directory if specified
    output_dir = args.output
    if output_dir:
        output_path = Path(output_dir)
        output_path.mkdir(parents=True, exist_ok=True)
        # Convert to absolute path
        output_dir = str(output_path.resolve())
        print("Output directory:", output_dir, flush=True)

    tests_to_run = load_tests(script_dir, test_file)

    if thread_count < 0:
        print("Thread count cannot be negative", flush=True)
        sys.exit(2)

    if thread_count > 0:
        thread_count = min(thread_count, len(tests_to_run))

    print(f"Found {len(tests_to_run)} tests to run", flush=True)

    if thread_count > 0:
        print(f"Scheduling at most {thread_count} tests concurrently (physical cores detected: {physical_cores})", flush=True)

    if thread_count != 0:
        # Use ProcessPoolExecutor with controlled submission to limit memory usage
        with ProcessPoolExecutor(max_workers=thread_count) as executor:
            # Use a queue to control submission rate - only submit when slots are available
            pending_tests = deque(tests_to_run)
            futures = {}
            started_tests = 0

            def submit_test(test_path):
                nonlocal started_tests

                started_tests += 1
                active_jobs = len(futures) + 1
                print(f">>> [{started_tests}/{len(tests_to_run)}] Starting test (active {active_jobs}/{thread_count}): {test_path}", flush=True)
                future = executor.submit(run_test, str(runner), test_path, args.retry, output_dir)
                futures[future] = test_path

            # Submit initial batch up to max_workers
            for _ in range(min(thread_count, len(pending_tests))):
                submit_test(pending_tests.popleft())

            # Process completed tests and submit new ones as slots free up
            while futures:
                # Wait for at least one test to complete
                done, _ = wait(futures, return_when=concurrent.futures.FIRST_COMPLETED)

                for future in done:
                    test = futures.pop(future)
                    passed, _, output_buffer, elapsed, attempts = future.result()
                    status = "PASS" if passed else "FAIL"
                    print(f"<<< [{status}] {test} ({elapsed:.2f}s, attempts={attempts})", flush=True)

                    # Print output now that test is complete
                    if args.verbose or not passed:
                        if output_buffer:
                            print("\n".join(output_buffer), end='', flush=True)

                    if passed:
                        passed_tests += 1
                    else:
                        failed_tests += 1
                        failed_names.append(test)

                    # Submit next test if available
                    if pending_tests:
                        submit_test(pending_tests.popleft())
    else:
        # Sequential execution
        for index, test in enumerate(tests_to_run, start=1):
            print(f">>> [{index}/{len(tests_to_run)}] Starting test: {test}", flush=True)
            passed, test_name, output_buffer, elapsed, attempts = run_test(str(runner), test, args.retry, output_dir)
            status = "PASS" if passed else "FAIL"
            print(f"<<< [{status}] {test_name} ({elapsed:.2f}s, attempts={attempts})", flush=True)

            if args.verbose or not passed:
                if output_buffer:
                    print("\n".join(output_buffer), end='', flush=True)

            if passed:
                passed_tests += 1
            else:
                failed_tests += 1
                failed_names.append(test)

    # Calculate elapsed time
    elapsed = time.time() - start_time
    minutes = int(elapsed // 60)
    seconds = int(elapsed % 60)

    print("\n" + "="*60, flush=True)
    print(f"Test Results: {passed_tests}/{failed_tests + passed_tests} passed in {minutes}m {seconds}s", flush=True)
    if failed_tests > 0:
        print(f"Failed tests ({failed_tests}):", flush=True)
        for test in failed_names:
           print("  - " + str(test), flush=True)
        sys.exit(-1)
    else:
        print("All tests passed!", flush=True)


if __name__ == '__main__':
    main()
