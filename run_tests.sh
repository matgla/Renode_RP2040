#!/bin/bash

./build_pico_examples.sh
python3 ./tests/run_tests.py -r 3 -f tests/tests.yaml