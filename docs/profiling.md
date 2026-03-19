# Profiling Renode test runs

Use the helper below to profile a single Robot Framework test and collect:

- wall-clock duration,
- Renode execution metrics dump,
- collapsed stack guest CPU profiles for `sysbus.cpu0` and `sysbus.cpu1`,
- raw `renode-test` stdout/stderr,
- the generated `robot_output.xml`.

## Usage

From the repository root:

```bash
python3 tests/profile_test.py blink_simple
```

You can also pass an explicit Robot test path:

```bash
python3 tests/profile_test.py tests/testcases/pio/pio_blink/pio_blink.robot
```

## Output

Artifacts are stored under:

```text
profiling/<test-name>-<timestamp>/
```

Important files:

- `metrics.dump-*` (Renode may append the suite name, for example `metrics.dump-pico_tests`)
- `cpu0_profile.collapsed`
- `cpu1_profile.collapsed`
- `robot_output.xml`
- `profile_manifest.json`

## Viewing results

### Guest CPU profile

Open `cpu0_profile.collapsed` or `cpu1_profile.collapsed` in [speedscope](https://www.speedscope.app/).

### Renode metrics

Use Renode's metrics tools to inspect the generated `metrics.dump-*` file.

## Notes

- The helper works by creating a temporary patched copy of the selected Robot test.
- It injects profiling commands immediately after the test's `.resc` include.
- It flushes CPU profiler output during test teardown.
- It is intended for this RP2040 test suite shape, where tests load a `.resc` file using `Execute Command    include @...`.
