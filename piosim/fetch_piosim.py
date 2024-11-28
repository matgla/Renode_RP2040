import argparse
import requests 
from pathlib import Path
import os
import zipfile
import shutil
import hashlib
import sys 

def calculate_hash(filepath):
    hash_function = hashlib.sha256()
    with open(filepath, "rb") as file:
        while chunk := file.read(8192):
            hash_function.update(chunk)
    return hash_function.hexdigest()

parser = argparse.ArgumentParser()
parser.add_argument("-v", "--version", default=None, help="PioSim version to fetch")
parser.add_argument("--verify", action="store_true", default=False, help="Verify if libraries are at version to fetch")

args, _ = parser.parse_known_args()

script_dir = Path(os.path.dirname(os.path.realpath(__file__)))
if args.version is None:
    with open(script_dir / "version", "r") as file: 
        args.version = file.read().strip()

print ("Fetching version:", args.version)

package_name = "libpiosim.zip"
url = "https://github.com/matgla/Renode_RP2040_PioSim/releases/download/" + args.version + "/" + package_name

r = requests.get(url)

if r.status_code != 200:
    print("Can't download piosim release version:", args.version)
    sys.exit(-1)

if not args.verify:
    try:
        os.remove(script_dir / "libpiosim.zip")
    except OSError:
        pass
    try:
        os.remove(script_dir / "libpiosim.dll")
    except OSError:
        pass
    try:
        os.remove(script_dir / "libpiosim.so")
    except OSError:
        pass
    try:
        os.remove(script_dir / "libpiosim.dylib")
    except OSError:
        pass

target_file = script_dir / package_name
open(target_file, 'wb').write(r.content)

with zipfile.ZipFile(target_file, "r") as zip_ref:
    zip_ref.extractall(script_dir / "piosim")

match = True
if not args.verify:
    for filename in os.listdir(script_dir / "piosim"): 
        source_file = script_dir / "piosim" / filename 
        shutil.copy(source_file, ".")
else: 
    for filename in os.listdir(script_dir / "piosim"): 
        source_file = script_dir / "piosim" / filename 
        actual_file = script_dir / filename

        if calculate_hash(source_file) != calculate_hash(actual_file):
            match = False 
            print("Verification of", str(filename), "failed")
            print("Make sure that fetch_piosim.py with specified version was executed!")

try:
    shutil.rmtree(script_dir / "piosim")
except OSError: 
    pass

try:
    os.remove(script_dir / "piosim")
except OSError: 
    pass

try:
    os.remove(target_file)
except OSError: 
    pass


if not match:
    sys.exit(-1)
