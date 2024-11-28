#!/usr/bin/python3

import os
from pathlib import Path
from subprocess import run
from argparse import ArgumentParser
import psutil
import sys

parser = ArgumentParser()
parser.add_argument("-f", "--file", help="Path to yaml file with tests")
parser.add_argument("-r", "--retry", type=int, default=1, help="Number of retries of tests if failed")
parser.add_argument("-e", "--renode_test", default="renode-test", help="renode-test executable path")
args, _ = parser.parse_known_args()

script_dir = Path(os.path.dirname(os.path.realpath(__file__)));
test_file = script_dir / "tests.yaml"
print ("Running tests from:", test_file)
print ("Using runner:", args.renode_test);
failed_tests=0
passed_tests=0
failed_names=[]

with open(test_file, "r") as file:
    for line in file.readlines():
        if line.find("#") != -1:
            continue

        test_file = script_dir / line.removeprefix("- tests/").strip()

        passed = False
        for i in range(0, args.retry):
            if run([str(Path(args.renode_test).absolute()) + " " + str(test_file)], shell=True).returncode == 0:
                passed = True 
                break
            else:
                for proc in psutil.process_iter():
                    filtered_name = proc.name().lower()
                    if filtered_name.find("renode") != -1:
                        print("Killing potentialy orphaned renode process: " + str(proc.pid) + ", name: " + proc.name())
                        proc.kill()
        if passed:
            passed_tests += 1
        else: 
            failed_tests += 1
            failed_names.append(test_file)

print("Test passed: " + str(passed_tests) + "/" + str(failed_tests + passed_tests))
print("Failed tests:")
if failed_tests > 0:
    for test in failed_names:
       print("  " + str(test)) 
    sys.exit(-1)