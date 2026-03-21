#!/usr/bin/env python3
"""
Helper script to compare two BMP images.
Returns exit code 0 if identical, 1 if different.
"""

import sys
from PIL import Image

def compare_images(ref_path, actual_path):
    """Compare two images pixel by pixel."""
    try:
        ref = Image.open(ref_path)
        actual = Image.open(actual_path)
        
        if ref.size != actual.size:
            print(f"Size mismatch: {ref.size} vs {actual.size}")
            return 1
        
        if list(ref.getdata()) == list(actual.getdata()):
            return 0
        else:
            print(f"Images differ: {ref_path} vs {actual_path}")
            return 1
    except Exception as e:
        print(f"Error comparing images: {e}")
        return 1

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python3 compare_images.py <reference> <actual>")
        sys.exit(1)
    
    sys.exit(compare_images(sys.argv[1], sys.argv[2]))
