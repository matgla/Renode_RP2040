#!/usr/bin/env python3
"""
I2C Peripheral Unit Test Runner

Cross-platform test runner for I2C peripheral unit tests.
Supports Windows, Linux, and macOS.

Usage:
    python run_unit_tests.py [options]

Options:
    -c, --cs-only       Run only C# unit tests
    -r, --renode-only   Run only Renode Python tests
    -v, --verbose       Enable verbose output
    -o, --output DIR    Output directory for test results
    -e, --renode-test   Path to renode-test executable
    -h, --help          Show this help message

Examples:
    python run_unit_tests.py                    # Run all tests
    python run_unit_tests.py -c                 # Run only C# tests
    python run_unit_tests.py -r -v              # Run only Renode tests with verbose output
    python run_unit_tests.py -e /path/to/renode-test
"""

import os
import sys
import subprocess
import argparse
import platform
import shutil
from pathlib import Path
from datetime import datetime
from typing import List, Tuple, Optional


class Colors:
    """ANSI color codes for terminal output."""
    RED = '\033[0;31m'
    GREEN = '\033[0;32m'
    YELLOW = '\033[1;33m'
    BLUE = '\033[0;34m'
    CYAN = '\033[0;36m'
    NC = '\033[0m'  # No Color
    BOLD = '\033[1m'

    @classmethod
    def disable(cls):
        """Disable colors (for Windows or non-terminal output)."""
        cls.RED = ''
        cls.GREEN = ''
        cls.YELLOW = ''
        cls.BLUE = ''
        cls.CYAN = ''
        cls.NC = ''
        cls.BOLD = ''


class TestRunner:
    """Main test runner class."""
    
    def __init__(self, args: argparse.Namespace):
        self.args = args
        self.script_dir = Path(__file__).parent.resolve()
        self.output_dir = Path(args.output) if args.output else self.script_dir / "output_unit_tests"
        self.passed = 0
        self.failed = 0
        self.skipped = 0
        
        # Ensure output directory exists
        self.output_dir.mkdir(parents=True, exist_ok=True)
        
        # Detect platform
        self.is_windows = platform.system() == "Windows"
        self.is_linux = platform.system() == "Linux"
        self.is_macos = platform.system() == "Darwin"
        
        # Disable colors on Windows if not supported
        if self.is_windows and not os.environ.get('ANSICON'):
            Colors.disable()
    
    def log(self, message: str, color: str = Colors.NC, bold: bool = False):
        """Print a colored message."""
        prefix = Colors.BOLD if bold else ""
        print(f"{prefix}{color}{message}{Colors.NC}")
    
    def log_section(self, title: str):
        """Print a section header."""
        print()
        self.log("=" * 60, Colors.CYAN)
        self.log(title, Colors.CYAN, bold=True)
        self.log("=" * 60, Colors.CYAN)
        print()
    
    def run_command(
        self, 
        cmd: List[str], 
        description: str, 
        timeout: int = 300,
        cwd: Optional[Path] = None
    ) -> Tuple[bool, str]:
        """Run a command and return success status and output."""
        if self.args.verbose:
            self.log(f"Running: {' '.join(cmd)}", Colors.BLUE)
        
        try:
            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=timeout,
                cwd=cwd or self.script_dir
            )
            
            output = result.stdout + result.stderr
            
            if result.returncode == 0:
                return True, output
            else:
                return False, output
                
        except subprocess.TimeoutExpired:
            return False, f"Command timed out after {timeout} seconds"
        except Exception as e:
            return False, f"Error running command: {e}"
    
    def find_renode_test(self) -> Optional[str]:
        """Find the renode-test executable."""
        if self.args.renode_test:
            return shutil.which(self.args.renode_test)
        
        # Try common names/paths
        names = ["renode-test", "renode-test.exe"]
        for name in names:
            path = shutil.which(name)
            if path:
                return path
        
        # Try common installation paths on Windows
        if self.is_windows:
            common_paths = [
                Path("C:/Program Files/Renode/bin/renode-test.exe"),
                Path("C:/Renode/bin/renode-test.exe"),
                Path(os.environ.get("LOCALAPPDATA", "")) / "Renode" / "bin" / "renode-test.exe",
            ]
            for path in common_paths:
                if path.exists():
                    return str(path)
        
        return None
    
    def find_dotnet(self) -> Optional[str]:
        """Find the dotnet executable."""
        return shutil.which("dotnet")
    
    def run_cs_tests(self) -> bool:
        """Run C# unit tests using dotnet run."""
        self.log_section("C# Unit Tests")
        
        dotnet = self.find_dotnet()
        if not dotnet:
            self.log("ERROR: dotnet not found in PATH", Colors.RED)
            self.log("Please install .NET SDK from https://dotnet.microsoft.com/download", Colors.YELLOW)
            return False
        
        self.log(f"Using dotnet: {dotnet}")
        
        # Build test library first
        self.log("Building test library...", Colors.BLUE)
        build_cmd = [
            dotnet,
            "build",
            "Peripherals.Tests.csproj",
            "-c", "Release"
        ]
        
        build_log = self.output_dir / "cs_build.log"
        success, output = self.run_command(
            build_cmd,
            "Building C# test library",
            cwd=self.script_dir / "emulation"
        )
        
        build_log.write_text(output, encoding="utf-8")
        
        if not success:
            self.log("BUILD FAILED", Colors.RED, bold=True)
            if self.args.verbose:
                print(output)
            return False
        
        self.log("Build successful", Colors.GREEN)
        
        # Build and run test runner
        self.log("Building test runner...", Colors.BLUE)
        runner_build_cmd = [
            dotnet,
            "build",
            "Tests.Runner/Tests.Runner.csproj",
            "-c", "Release"
        ]
        
        success, output = self.run_command(
            runner_build_cmd,
            "Building test runner",
            cwd=self.script_dir / "emulation"
        )
        
        if not success:
            self.log("TEST RUNNER BUILD FAILED", Colors.RED, bold=True)
            if self.args.verbose:
                print(output)
            return False
        
        self.log("Running C# unit tests...", Colors.BLUE)
        
        # Run the test runner
        runner_exe = self.script_dir / "emulation" / "Tests.Runner" / "bin" / "Release" / "net10.0" / "Tests.Runner"
        if self.is_windows:
            runner_exe = runner_exe.with_suffix(".exe")
        
        run_cmd = [str(runner_exe)]
        
        test_log = self.output_dir / "cs_test.log"
        success, output = self.run_command(
            run_cmd,
            "Running C# unit tests",
            cwd=self.script_dir / "emulation" / "Tests.Runner" / "bin" / "Release" / "net10.0"
        )
        
        test_log.write_text(output, encoding="utf-8")
        
        # Parse results from output
        if "Results:" in output:
            # Extract pass/fail counts from output
            for line in output.split('\n'):
                if "Results:" in line:
                    try:
                        parts = line.split(':')[1].strip().split(',')
                        for part in parts:
                            if "passed" in part.lower():
                                self.passed += int(part.strip().split()[0])
                            if "failed" in part.lower():
                                self.failed += int(part.strip().split()[0])
                    except:
                        pass
                    break
        elif success:
            self.passed += 1
        else:
            self.failed += 1
        
        if success:
            self.log("C# tests completed", Colors.GREEN)
        else:
            self.log("C# tests failed", Colors.RED)
        
        if self.args.verbose or not success:
            print(output)
        
        return success
    
    def run_renode_tests(self) -> bool:
        """Run Renode Python tests."""
        self.log_section("Renode Python Tests")
        
        renode_test = self.find_renode_test()
        if not renode_test:
            self.log("WARNING: renode-test not found, skipping Renode tests", Colors.YELLOW)
            self.log("Install Renode from https://renode.io/ or use -e to specify path", Colors.YELLOW)
            self.skipped += 1
            return True  # Don't fail if renode is not installed
        
        self.log(f"Using renode-test: {renode_test}")
        
        # Check if test files exist
        test_dir = self.script_dir / "tests" / "unit" / "i2c"
        robot_file = test_dir / "i2c_unit_tests.robot"
        
        if not robot_file.exists():
            self.log(f"ERROR: Test file not found: {robot_file}", Colors.RED)
            return False
        
        # Run tests
        self.log("Running Renode Python tests...", Colors.BLUE)
        test_cmd = [
            renode_test,
            str(robot_file)
        ]
        
        if self.args.verbose:
            test_cmd.append("--verbose")
        
        test_log = self.output_dir / "renode_test.log"
        success, output = self.run_command(
            test_cmd,
            "Running Renode Python tests",
            timeout=120
        )
        
        test_log.write_text(output, encoding="utf-8")
        
        # Parse results
        if success:
            self.passed += 1
            self.log("Renode tests passed", Colors.GREEN)
        else:
            self.failed += 1
            self.log("Renode tests failed", Colors.RED)
        
        if self.args.verbose or not success:
            print(output)
        
        return success
    
    def print_summary(self, start_time: datetime):
        """Print test summary."""
        elapsed = (datetime.now() - start_time).total_seconds()
        
        self.log_section("Test Summary")
        
        self.log(f"Total time: {elapsed:.2f} seconds")
        self.log(f"Tests passed: {self.passed}", Colors.GREEN if self.passed > 0 else Colors.NC)
        self.log(f"Tests failed: {self.failed}", Colors.RED if self.failed > 0 else Colors.NC)
        if self.skipped > 0:
            self.log(f"Tests skipped: {self.skipped}", Colors.YELLOW)
        
        print()
        self.log(f"Output directory: {self.output_dir}", Colors.CYAN)
        
        # List log files
        log_files = list(self.output_dir.glob("*.log"))
        if log_files:
            self.log("Log files:", Colors.CYAN)
            for log_file in log_files:
                print(f"  - {log_file.name}")
        
        print()
        
        if self.failed > 0:
            self.log("SOME TESTS FAILED", Colors.RED, bold=True)
            return 1
        elif self.passed > 0:
            self.log("ALL TESTS PASSED", Colors.GREEN, bold=True)
            return 0
        else:
            self.log("NO TESTS WERE RUN", Colors.YELLOW, bold=True)
            return 1
    
    def run(self) -> int:
        """Run all requested tests."""
        start_time = datetime.now()
        
        self.log("=" * 60, Colors.CYAN)
        self.log("I2C Peripheral Unit Test Runner", Colors.CYAN, bold=True)
        self.log(f"Platform: {platform.system()} ({platform.machine()})", Colors.CYAN)
        self.log(f"Output: {self.output_dir}", Colors.CYAN)
        self.log("=" * 60, Colors.CYAN)
        
        success = True
        
        # Run C# tests
        if not self.args.renode_only:
            if not self.run_cs_tests():
                success = False
        
        # Run Renode tests
        if not self.args.cs_only:
            if not self.run_renode_tests():
                success = False
        
        # Print summary
        return self.print_summary(start_time)


def create_argument_parser() -> argparse.ArgumentParser:
    """Create the argument parser."""
    parser = argparse.ArgumentParser(
        description="I2C Peripheral Unit Test Runner",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python run_unit_tests.py                    # Run all tests
  python run_unit_tests.py -c                 # Run only C# tests
  python run_unit_tests.py -r -v              # Run only Renode tests, verbose
  python run_unit_tests.py -e /path/to/renode-test  # Specify renode-test path
        """
    )
    
    parser.add_argument(
        "-c", "--cs-only",
        action="store_true",
        help="Run only C# unit tests"
    )
    
    parser.add_argument(
        "-r", "--renode-only",
        action="store_true",
        help="Run only Renode Python tests"
    )
    
    parser.add_argument(
        "-v", "--verbose",
        action="store_true",
        help="Enable verbose output"
    )
    
    parser.add_argument(
        "-o", "--output",
        metavar="DIR",
        help="Output directory for test results (default: output_unit_tests)"
    )
    
    parser.add_argument(
        "-e", "--renode-test",
        metavar="PATH",
        help="Path to renode-test executable"
    )
    
    return parser


def main():
    """Main entry point."""
    parser = create_argument_parser()
    args = parser.parse_args()
    
    runner = TestRunner(args)
    exit_code = runner.run()
    sys.exit(exit_code)


if __name__ == "__main__":
    main()
