#!/usr/bin/python3

import os
from pathlib import Path
from subprocess import run
from argparse import ArgumentParser
import psutil
import sys
import shutil
from concurrent.futures import ThreadPoolExecutor, as_completed
import multiprocessing

def run_test(command, test, retries):
    print("Staring test:", test)
    passed = False
    for i in range(0, retries):
        if run([command, test]).returncode == 0:
            passed = True 
            break
    return passed, test

parser = ArgumentParser()
parser.add_argument("-f", "--file", help="Path to yaml file with tests")
parser.add_argument("-r", "--retry", type=int, default=1, help="Number of retries of tests if failed")
parser.add_argument("-e", "--renode_test", default="renode-test", help="renode-test executable path")
parser.add_argument("-j", "--threads", default=multiprocessing.cpu_count(), type=int, help="Number of thread for renode tests")


args, _ = parser.parse_known_args()

script_dir = Path(os.path.dirname(os.path.realpath(__file__)))
test_file = script_dir / "tests.yaml"
print ("Running tests from:", test_file)
runner = shutil.which(args.renode_test)
print ("Using runner:", runner)
failed_tests=0
passed_tests=0
failed_names=[]

print("Using threads:", args.threads)

tests_to_run = []
with open(test_file, "r") as file:
    for line in file.readlines():
        if line.find("#") != -1:
            continue

        test_file = script_dir / line.removeprefix("- tests/").strip()
        tests_to_run.append(str(test_file))



if args.threads != 0:
    with ThreadPoolExecutor(max_workers=multiprocessing.cpu_count()) as executor:
        futures = [executor.submit(run_test, str(runner), test, 3) for test in tests_to_run]

        for future in as_completed(futures):
            passed, test = future.result()
            if passed:
                passed_tests += 1
            else: 
                failed_tests += 1
                failed_names += test
else:
    for test in tests_to_run:
        passed, test = run_test(str(runner), test, 3)
        if passed: 
            passed_tests += 1
        else: 
            failed_tests += 1
            failed_names += test


print("Test passed: " + str(passed_tests) + "/" + str(failed_tests + passed_tests))
print("Failed tests:")
if failed_tests > 0:
    for test in failed_names:
       print("  " + str(test)) 
    sys.exit(-1)