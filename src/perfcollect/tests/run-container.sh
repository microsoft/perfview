#!/bin/bash
# Build and test exactly one disposable Docker container. YAML owns CI scheduling;
# local runs use this same script. Copy diagnostics before removing owned objects,
# including after a failing test. Never mount host homes or prune a shared daemon.
set -euo pipefail

usage()
{
    echo "Usage: $0 BASE_IMAGE@sha256:DIGEST TARGET RESULTS_DIR"
}
if [[ $# == 1 && ( $1 == --help || $1 == -h ) ]]; then usage; exit 0; fi
[[ $# == 3 ]] || { usage >&2; exit 2; }
base_image=$1
target=$2
results_dir=$3
[[ $base_image =~ ^[^[:space:]]+@sha256:[a-f0-9]{64}$ ]] ||
    { echo "BASE_IMAGE must have an immutable sha256 digest." >&2; exit 2; }
[[ $target =~ ^[a-z0-9][a-z0-9.-]*$ ]] ||
    { echo "TARGET must be a lowercase distro target." >&2; exit 2; }

tests_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
source_dir=$(cd -- "$tests_dir/.." && pwd)
umask 077
mkdir -p -- "$(dirname -- "$results_dir")"
mkdir -- "$results_dir"
results_dir=$(cd -- "$results_dir" && pwd)
name="perfcollect-$target-$(date -u +%Y%m%dt%H%M%S)-$BASHPID"
image_built=0
container_attempted=0

cleanup()
{
    local rc=$? failed=0
    trap - EXIT INT TERM
    if [[ $container_attempted == 1 ]]; then
        if ! docker inspect "$name" > "$results_dir/container-inspect.json" 2> "$results_dir/inspect.log"; then
            echo "Could not inspect test container; see inspect.log." >&2
            failed=1
        fi
        if ! docker cp "$name:/results/." "$results_dir" > "$results_dir/copy.log" 2>&1; then
            echo "Could not retrieve test evidence; see copy.log." >&2
            failed=1
        elif [[ ! -s $results_dir/results.tsv ]] ||
            grep -Eiq $'\t(FAIL|BLOCKED|CANCELLED)\t' "$results_dir/results.tsv"; then
            echo "Missing or failing test results; see results.tsv and container.log." >&2
            failed=1
        fi
        if ! docker rm --force "$name" >> "$results_dir/cleanup.log" 2>&1; then
            echo "Could not remove owned container $name; see cleanup.log." >&2
            failed=1
        fi
    fi
    if [[ $image_built == 1 ]] && ! docker rmi "$name" >> "$results_dir/cleanup.log" 2>&1; then
        echo "Could not remove owned test image $name; see cleanup.log." >&2
        failed=1
    fi
    # Preserve the test's exit status, but fail successful tests if cleanup fails.
    if [[ $rc == 0 && $failed == 1 ]]; then rc=1; fi
    exit "$rc"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

printf 'target=%s\nbase_image=%s\ncontainer=%s\ngit_revision=%s\n' \
    "$target" "$base_image" "$name" "${BUILD_SOURCEVERSION:-local-worktree}" > "$results_dir/provenance.txt"
sha256sum "$source_dir/perfcollect" "$tests_dir/"*.sh "$tests_dir/containers/"* \
    "$tests_dir/fixtures/integration/"* > "$results_dir/source-sha256.txt"
docker --version > "$results_dir/runtime.txt"

DOCKER_BUILDKIT=0 docker build --force-rm --file "$tests_dir/containers/matrix.dockerfile" \
    --tag "$name" --build-arg "BASE_IMAGE=$base_image" "$source_dir" \
    2>&1 | tee "$results_dir/build.log"
image_built=1
container_attempted=1
docker run --name "$name" --user 0 --workdir /src \
    --privileged --security-opt seccomp=unconfined --shm-size 1g \
    --env "PERFCOLLECT_TEST_TARGET=$target" --env PERFCOLLECT_RESULTS_DIR=/results \
    --env "PERFCOLLECT_IMAGE=$name" --env "PERFCOLLECT_IMAGE_DIGEST=$base_image" \
    "$name" 2>&1 | tee "$results_dir/container.log"
