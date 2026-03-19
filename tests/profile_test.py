#!/usr/bin/python3

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path


def resolve_test_path(repo_root: Path, test_argument: str) -> Path:
    candidate = Path(test_argument)
    if candidate.exists():
        return candidate.resolve()

    tests_root = repo_root / "tests" / "testcases"
    matches = sorted(
        path.resolve()
        for path in tests_root.rglob("*.robot")
        if test_argument.lower() in str(path.relative_to(tests_root)).lower()
        or test_argument.lower() in path.stem.lower()
    )

    if not matches:
        raise FileNotFoundError(f"No robot test matches: {test_argument}")
    if len(matches) > 1:
        match_list = "\n".join(f"  - {path.relative_to(repo_root)}" for path in matches)
        raise RuntimeError(f"Ambiguous test pattern: {test_argument}\nMatches:\n{match_list}")
    return matches[0]


def patch_robot_test(original_test: Path, output_dir: Path, metrics_command: str) -> Path:
    original_lines = original_test.read_text().splitlines()
    original_dir = original_test.parent.as_posix()

    patched_lines = []
    injected = False
    settings_start = None
    settings_end = None
    existing_test_teardown = None

    for idx, line in enumerate(original_lines):
        replaced_line = line.replace("${CURDIR}", original_dir)
        stripped = replaced_line.strip()

        if stripped == "*** Settings ***":
            settings_start = len(patched_lines)
        elif settings_start is not None and settings_end is None and stripped.startswith("*** ") and stripped != "*** Settings ***":
            settings_end = len(patched_lines)

        if stripped.startswith("Test Teardown"):
            parts = re.split(r"\s{2,}|\t+", stripped, maxsplit=1)
            if len(parts) == 2 and parts[0] == "Test Teardown":
                existing_test_teardown = parts[1].strip()
                replaced_line = "Test Teardown   Profiling Test Teardown"

        patched_lines.append(replaced_line)

        if stripped.startswith("Execute Command") and "include @" in stripped:
            patched_lines.append(f"    Execute Command             {metrics_command}")
            patched_lines.append(f"    Execute Command             sysbus.cpu0 EnableProfilerCollapsedStack @{(output_dir / 'cpu0_profile.collapsed').as_posix()}")
            patched_lines.append(f"    Execute Command             sysbus.cpu1 EnableProfilerCollapsedStack @{(output_dir / 'cpu1_profile.collapsed').as_posix()}")
            injected = True

    if settings_start is None:
        raise RuntimeError(f"Could not find settings section in {original_test}")

    if settings_end is None:
        settings_end = len(patched_lines)

    if existing_test_teardown is None:
        patched_lines.insert(settings_end, "Test Teardown   Profiling Test Teardown")
        settings_end += 1

    if not injected:
        raise RuntimeError(f"Could not find 'Execute Command include @...' in {original_test}")

    if not any(line.strip() == "*** Keywords ***" for line in patched_lines):
        patched_lines.extend(["", "*** Keywords ***"])

    patched_lines.extend(
        [
            "",
            "Profiling Test Teardown",
            "    Run Keyword And Ignore Error    Execute Command    sysbus.cpu0 FlushProfiler",
            "    Run Keyword And Ignore Error    Execute Command    sysbus.cpu1 FlushProfiler",
        ]
    )

    if existing_test_teardown:
        patched_lines.append(f"    {existing_test_teardown}")

    patched_path = output_dir / f"{original_test.stem}.profiled.robot"
    patched_path.write_text("\n".join(patched_lines) + "\n")
    return patched_path


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Profile a single Renode robot test.")
    parser.add_argument("test", help="Robot test path or substring to match under tests/testcases")
    parser.add_argument("-e", "--renode-test", default="renode-test", help="Path to renode-test executable")
    parser.add_argument("-o", "--output-dir", help="Directory for profiling artifacts")
    parser.add_argument("--metrics-command", default=None,
                        help="Monitor command used to enable execution metrics. Defaults to EnableProfilerGlobally.")
    parser.add_argument("--retry", type=int, default=1, help="Number of retries")
    return parser


def find_metrics_artifacts(output_dir: Path) -> list[Path]:
    return sorted(path for path in output_dir.glob("metrics.dump*") if path.is_file())


def main() -> int:
    parser = build_argument_parser()
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parent.parent
    env = os.environ.copy()

    venv_bin = repo_root / "venv" / "bin"
    if venv_bin.is_dir():
        env["PATH"] = f"{venv_bin}:{env['PATH']}"

    try:
        test_path = resolve_test_path(repo_root, args.test)
    except (FileNotFoundError, RuntimeError) as exc:
        print(str(exc), file=sys.stderr)
        return 2

    runner = shutil.which(args.renode_test, path=env["PATH"])
    if runner is None:
        print(f"Could not find renode-test executable: {args.renode_test}", file=sys.stderr)
        return 2

    timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    default_output_dir = repo_root / "profiling" / f"{test_path.stem}-{timestamp}"
    output_dir = Path(args.output_dir).resolve() if args.output_dir else default_output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    metrics_command = args.metrics_command or f'EnableProfilerGlobally "{(output_dir / "metrics.dump").as_posix()}"'

    try:
        patched_test = patch_robot_test(test_path, output_dir, metrics_command)
    except RuntimeError as exc:
        print(str(exc), file=sys.stderr)
        return 2

    command = [runner, str(patched_test), "-r", str(output_dir)]

    start = time.perf_counter()
    result = subprocess.run(command, capture_output=True, text=True, env=env)
    wall_time = time.perf_counter() - start

    (output_dir / "renode-test.stdout.txt").write_text(result.stdout)
    (output_dir / "renode-test.stderr.txt").write_text(result.stderr)

    metrics_artifacts = find_metrics_artifacts(output_dir)

    manifest = {
        "original_test": str(test_path),
        "patched_test": str(patched_test),
        "runner": runner,
        "command": command,
        "wall_time_seconds": wall_time,
        "return_code": result.returncode,
        "artifacts": {
            "metrics_command_target": str(output_dir / "metrics.dump"),
            "metrics": [str(path) for path in metrics_artifacts],
            "cpu0_profile": str(output_dir / "cpu0_profile.collapsed"),
            "cpu1_profile": str(output_dir / "cpu1_profile.collapsed"),
            "robot_output": str(output_dir / "robot_output.xml"),
            "stdout": str(output_dir / "renode-test.stdout.txt"),
            "stderr": str(output_dir / "renode-test.stderr.txt"),
        },
    }
    (output_dir / "profile_manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")

    print(f"Test: {test_path.relative_to(repo_root)}")
    print(f"Output: {output_dir}")
    print(f"Wall time: {wall_time:.2f}s")
    print(f"Return code: {result.returncode}")
    print("")
    print("Artifacts:")
    if metrics_artifacts:
        for metrics_artifact in metrics_artifacts:
            print(f"- metrics: {metrics_artifact.relative_to(repo_root)}")
    else:
        print(f"- metrics target: {(output_dir / 'metrics.dump').relative_to(repo_root)} (no file found)")
    print(f"- cpu0 profile: {(output_dir / 'cpu0_profile.collapsed').relative_to(repo_root)}")
    print(f"- cpu1 profile: {(output_dir / 'cpu1_profile.collapsed').relative_to(repo_root)}")
    print(f"- robot output: {(output_dir / 'robot_output.xml').relative_to(repo_root)}")

    if result.stdout:
        print("\nrenode-test stdout:\n")
        print(result.stdout)
    if result.stderr:
        print("\nrenode-test stderr:\n", file=sys.stderr)
        print(result.stderr, file=sys.stderr)

    if result.returncode == 0:
        print("Next steps:")
        print(f"- Open cpu profile in speedscope: {(output_dir / 'cpu0_profile.collapsed').as_posix()}")
        if metrics_artifacts:
            for metrics_artifact in metrics_artifacts:
                print(f"- Inspect metrics dump: {metrics_artifact.as_posix()}")
        else:
            print(f"- Metrics target was: {(output_dir / 'metrics.dump').as_posix()}")
        return 0
    return result.returncode


if __name__ == "__main__":
    raise SystemExit(main())
