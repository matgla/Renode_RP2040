#!/bin/bash
set -euo pipefail

START="$(pwd)"
SCRIPT_DIR="$(cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd)"
REPO_DIR="$(cd -- "$SCRIPT_DIR/.." && pwd)"
PICO_EXAMPLES_DIR="$SCRIPT_DIR/pico-examples"
BUILD_DIR="$PICO_EXAMPLES_DIR/build"
PATCHES_DIR="$SCRIPT_DIR/pico_examples_patches"
REVISION_FILE="$SCRIPT_DIR/pico_examples_revision"
CACHE_ROOT="${PICO_EXAMPLES_CACHE_DIR:-$REPO_DIR/tmp/pico-examples-cache}"
FORCE_REBUILD="${PICO_EXAMPLES_FORCE_REBUILD:-0}"

cleanup() {
    cd "$START"
}

recover_interrupted_git_am() {
    if [ ! -d .git/rebase-apply ] && [ ! -d .git/rebase-merge ]; then
        return 0
    fi

    echo "Found interrupted git am state; recovering before applying patches..."
    git am --abort 2>/dev/null || true

    if [ -d .git/rebase-apply ] || [ -d .git/rebase-merge ]; then
        echo "Removing stale git am metadata from pico-examples checkout"
        rm -rf .git/rebase-apply .git/rebase-merge
    fi
}

compute_cache_key() {
    {
        printf 'revision=%s\n' "$revision"
        if compgen -G "$PATCHES_DIR/*.patch" > /dev/null; then
            for patch in "$PATCHES_DIR"/*.patch; do
                printf 'patch=%s %s\n' "$(basename "$patch")" "$(sha256sum "$patch" | awk '{print $1}')"
            done
        fi
    } | sha256sum | awk '{print $1}'
}

build_outputs_present() {
    find "$BUILD_DIR" -type f \( -name '*.elf' -o -name '*.uf2' -o -name '*.bin' \) -print -quit | grep -q .
}

build_marker_matches() {
    [ -f "$BUILD_DIR/.renode-cache-key" ] && [ "$(cat "$BUILD_DIR/.renode-cache-key")" = "$CACHE_KEY" ]
}

write_build_marker() {
    printf '%s\n' "$CACHE_KEY" > "$BUILD_DIR/.renode-cache-key"
}

restore_cached_build() {
    if [ ! -d "$CACHE_DIR" ]; then
        return 1
    fi

    echo "Restoring pico-examples build from cache: $CACHE_DIR"
    rm -rf "$BUILD_DIR"
    mkdir -p "$BUILD_DIR"
    cp -a "$CACHE_DIR/." "$BUILD_DIR/"
}

save_cached_build() {
    local temp_dir

    mkdir -p "$CACHE_ROOT"
    temp_dir="$(mktemp -d "$CACHE_ROOT/.tmp.XXXXXX")"
    cp -a "$BUILD_DIR/." "$temp_dir/"
    rm -rf "$CACHE_DIR"
    mv "$temp_dir" "$CACHE_DIR"
}

trap cleanup EXIT

echo "Using script directory: $SCRIPT_DIR"

revision="$(cat "$REVISION_FILE")"
CACHE_KEY="$(compute_cache_key)"
CACHE_DIR="$CACHE_ROOT/$CACHE_KEY"

echo "Using Pico Examples revision: $revision"
echo "Using pico-examples cache key: $CACHE_KEY"

cd "$SCRIPT_DIR"

# Clone pico-examples if not present
if [ ! -d "$PICO_EXAMPLES_DIR" ]; then
    echo "Cloning pico-examples repository..."
    git clone https://github.com/raspberrypi/pico-examples.git "$PICO_EXAMPLES_DIR"
fi

cd "$PICO_EXAMPLES_DIR"

recover_interrupted_git_am

# Ensure we're at the correct revision with patches applied.
PATCHES_APPLIED=false
if [ -d .git ]; then
    last_patch=""
    for patch in "$PATCHES_DIR"/*.patch; do
        if [ -f "$patch" ]; then
            last_patch="$patch"
        fi
    done
    if [ -n "$last_patch" ]; then
        patch_subject="$(head -20 "$last_patch" | grep "^Subject:" | sed 's/^Subject: \[PATCH\] //')"
        if [ -n "$patch_subject" ] && git log --oneline -20 | grep -qF "$patch_subject"; then
            PATCHES_APPLIED=true
            echo "Patches already applied"
        fi
    else
        PATCHES_APPLIED=true
    fi
fi

if [ "$PATCHES_APPLIED" = false ] && [ -d .git ]; then
    echo "Resetting pico-examples to revision $revision and applying patches..."
    git fetch origin 2>/dev/null || true
    git reset --hard "$revision"

    for patch in "$PATCHES_DIR"/*.patch; do
        if [ -f "$patch" ]; then
            echo "Applying patch: $patch"
            git am --ignore-space-change --ignore-whitespace < "$patch"
        fi
    done

    # Force reconfigure after patches change source.
    rm -f "$BUILD_DIR/build.ninja"
fi

if [ "$FORCE_REBUILD" != "1" ]; then
    if build_marker_matches && build_outputs_present; then
        echo "Existing pico-examples build matches cache key; skipping rebuild"
        echo "Build completed successfully"
        exit 0
    fi

    if restore_cached_build && build_marker_matches && build_outputs_present; then
        echo "Restored pico-examples build from cache"
        echo "Build completed successfully"
        exit 0
    fi

    echo "No reusable pico-examples build found for cache key $CACHE_KEY"
else
    echo "Forcing pico-examples rebuild (PICO_EXAMPLES_FORCE_REBUILD=1)"
fi

mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

if [ ! -f build.ninja ]; then
    echo "Configuring pico-examples build..."
    PICO_SDK_FETCH_FROM_GIT=1 cmake .. -GNinja -DCMAKE_BUILD_TYPE=Release -DPICO_BOARD=pico
fi

echo "Building pico-examples (incremental)..."
cmake --build . --parallel

write_build_marker
save_cached_build

echo "Saved pico-examples build to cache: $CACHE_DIR"
echo "Build completed successfully"
