#!/usr/bin/env python3
"""
Helper script to view SSD1306 display output.
Converts BMP files to ASCII art for terminal viewing.
"""

import sys
from PIL import Image

def bmp_to_ascii(filename, width=128, height=32):
    """Convert a 1-bit BMP to ASCII art."""
    try:
        img = Image.open(filename)
        if img.size != (width, height):
            print(f"Warning: Expected {width}x{height}, got {img.size}")
        
        # Convert to 1-bit if not already
        img = img.convert('1')
        
        pixels = img.load()
        
        # Print header
        print(f"Display: {filename}")
        print("-" * (width // 2))
        
        # Print ASCII representation (using half width since characters are tall)
        for y in range(0, height, 2):  # Skip every other row for aspect ratio
            line = ""
            for x in range(width):
                # Get pixel (inverted because BMP uses 0=white, 1=black in palette)
                p1 = pixels[x, y] if y < height else 1
                p2 = pixels[x, y+1] if y+1 < height else 1
                
                # Use different characters for different pixel patterns
                if p1 == 0 and p2 == 0:
                    line += "█"  # Both pixels on
                elif p1 == 0 and p2 == 1:
                    line += "▀"  # Top pixel on
                elif p1 == 1 and p2 == 0:
                    line += "▄"  # Bottom pixel on
                else:
                    line += " "  # Both pixels off
            print(line)
        
        print("-" * (width // 2))
        
    except Exception as e:
        print(f"Error: {e}")
        sys.exit(1)

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python3 view_display.py <bmp_file>")
        print("Example: python3 view_display.py output_images/raspberry_render.bmp")
        sys.exit(1)
    
    bmp_to_ascii(sys.argv[1])
