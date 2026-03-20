#
# load_peripherals.py
#
# Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
#
# Distributed under the terms of the MIT License.
#

# This script loads RP2040 peripherals from a pre-compiled DLL and registers
# their types with Renode before the REPL is parsed.

import clr
import os
import System

clr.AddReference("Infrastructure")

from Antmicro.Renode.Core import EmulationManager
from Antmicro.Renode.Utilities import TypeManager


script_dir = os.path.dirname(os.path.abspath(__file__))
peripherals_dll = os.path.normpath(
    os.path.join(
        script_dir,
        "..",
        "emulation",
        "bin",
        "Release",
        "netstandard2.1",
        "Peripherals.dll",
    )
)

if not os.path.exists(peripherals_dll):
    raise Exception(
        "Peripherals.dll not found at: {}. Build it first with: "
        "dotnet build emulation/Peripherals.csproj -c Release, or include "
        "cores/initialize_peripherals_source.resc for source mode.".format(peripherals_dll)
    )

type_manager = TypeManager.Instance
if not type_manager.ScanFile(peripherals_dll, False):
    raise Exception("Failed to scan peripheral assembly: {}".format(peripherals_dll))

assembly = System.Reflection.Assembly.LoadFrom(peripherals_dll)
pio_sim_path_type = assembly.GetType("Antmicro.Renode.Peripherals.CPU.PioSimPath")
if pio_sim_path_type is None:
    raise Exception("PioSimPath type not found in {}".format(peripherals_dll))

pio_sim_path = System.Activator.CreateInstance(pio_sim_path_type)
pio_sim_path.path = os.path.normpath(os.path.join(script_dir, "..", "piosim"))
EmulationManager.Instance.CurrentEmulation.ExternalsManager.AddExternal(pio_sim_path, "piosim_path")
