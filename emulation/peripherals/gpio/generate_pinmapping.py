# -*- coding: utf-8 -*-

#
# generate_pinmapping.py
#
# Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
#
# Distributed under the terms of the MIT License.
#

import argparse
import pandas as pd

parser = argparse.ArgumentParser()
parser.add_argument("-f", "--file", help="CSV file with pinmapping", required=True)
parser.add_argument(
    "-o", "--output", help="Output file with generated code", required=True
)

args, _ = parser.parse_known_args()

print("Parsing file: " + args.file)

with open(args.output, "w") as out:
    out.write("public RPGpioMapping[,] Generate()\n")
    out.write("{\n")
    df = pd.read_csv(args.file)
    rows, columns = df.shape
    out.write(
        "  var pinMapping = new RpGpioFunction["
        + str(rows - 1)
        + ","
        + str(columns - 1)
        + "];"
    )

    rowId = 0
    for index, row in df.iterrows():
        columnId = 0
        for index, e in row.to_dict().items():
            if index == "GPIO":
                continue
            out.write(
                "  pinMapping["
                + str(rowId)
                + ","
                + str(columnId)
                + "] = RpGpioFunction."
            )
            out.write(e + ";\n")
            columnId += 1
        rowId += 1

    out.write("  return pinMapping;")
    out.write("}\n")
