#!/bin/bash

# Run only inside a disposable Linux test container. YAML schedules CI jobs;
# run-container.sh owns each container. Local tracing runs must be serial.
#
# This harness exercises the actual perfcollect CLI with real tools/events, not
# simulated tracing responses. It first proves host capabilities,
# then checks lifecycle/state, actual archive contents, and session-owned cleanup.
# Ordinary perf collect must preserve both a completed lifecycle's archive and
# a separate active session's state, log, and workspace.
#
# Contract: bash /src/tests/session-integration-tests.sh (no arguments).
# PERFCOLLECT_TEST_TARGET selects ubuntu-20.04, debian-bookworm, ci-debian12
# (real userspace UST), or ubuntu-24.04, mariner-2.0, alpine, azurelinux-3.0
# (real perf). Only the Mariner install case invokes the actual package installer.
# PERFCOLLECT_RESULTS_DIR defaults to /results.
#
# Every mandatory case appends case-id<TAB>PASS|FAIL|BLOCKED<TAB>detail to
# results.tsv and writes its command/diagnostic log. BLOCKED is not a skip:
# either FAIL or BLOCKED makes this harness return nonzero.
#
# Deliberately do not use errexit: continue independent assertions and record
# blocked dependents, then reclaim only test-owned processes and scratch.
set -u
set -o pipefail
export LC_ALL=C
export TMPDIR=/tmp
# Legacy stop returns the final tput status; xterm supplies the reset capability.
export TERM=xterm

# Refuse before creating results, installing cleanup traps, or touching any
# session/home/scratch path. A target name alone is not a container fence.
if [[ "$(uname -s)" != Linux || "$EUID" != 0 ]]; then
    printf 'BLOCKED: mandatory integration suite requires Linux root inside a disposable container.\n' >&2
    exit 1
fi
if [[ ! -f /.dockerenv && ! -f /run/.containerenv ]]; then
    printf 'BLOCKED: refusing session/home mutation outside a marked disposable container.\n' >&2
    exit 1
fi

# Initialize every tracked resource before installing cleanup. A blocked early
# case must not cause guessed session names, broad process kills, or state deletion.
perfcollect=/src/perfcollect
fixtures=/src/tests/fixtures/integration
results_dir=${PERFCOLLECT_RESULTS_DIR:-/results}
target=${PERFCOLLECT_TEST_TARGET:-}
argument_count=$#
declare -A outcomes=()
declare -A state_fields=()
failures=0
blocked=0
CASE_DETAIL=''
scratch=''
state_dir=''
state_file=''
perf_binary=''
trace_reader=''
sessiond_pid=''
cpu_pid=''
probe_session=''
active_session=''
ust_workspace=''
ust_trace_dir=''
ust_archive=''
disabled_workspace=''
disabled_log=''
disabled_archive=''
disabled_name=''
disabled_metadata_stat=''
token=''
perf_collect_attempted=0
disabled_lttng_options=(-nolttng)
case "$target" in
    mariner-2.0|alpine|azurelinux-3.0) disabled_lttng_options=() ;;
esac

# Persistent evidence is outside the private scratch removed at suite exit.
if [[ "$results_dir" != /* ]] || ! mkdir -p -- "$results_dir"; then
    printf 'BLOCKED: cannot create absolute results directory %s\n' "$results_dir" >&2
    exit 1
fi

# Case functions return 1 for a behavior failure or 77 for an unavailable
# prerequisite; neither outcome is considered passing coverage.
case_fail()
{
    CASE_DETAIL=$*
    printf 'FAIL: %s\n' "$CASE_DETAIL"
    return 1
}

case_block()
{
    CASE_DETAIL=$*
    printf 'BLOCKED: %s\n' "$CASE_DETAIL"
    return 77
}

# Dependencies refer to prior case IDs. Skip only a dependent operation, retain
# its explicit BLOCKED row, and allow other independent assertions to continue.
run_case()
{
    local id=$1 function_name=$2 dependency rc=0 detail
    shift 2
    CASE_DETAIL=''
    {
        printf 'case=%s target=%s bash=%s\n' "$id" "$target" "$BASH_VERSION"
        for dependency in "$@"; do
            if [[ "${outcomes[$dependency]:-NOT_RUN}" != PASS ]]; then
                case_block "requires $dependency (${outcomes[$dependency]:-NOT_RUN})"
                rc=77
                break
            fi
        done
        if (( rc == 0 )); then
            set -x
            "$function_name"
            rc=$?
            set +x
        fi
    } > "$results_dir/$id.log" 2>&1
    case "$rc" in
        0) outcomes[$id]=PASS ;;
        77) outcomes[$id]=BLOCKED; (( blocked += 1 )) ;;
        *) outcomes[$id]=FAIL; (( failures += 1 )) ;;
    esac
    detail=${CASE_DETAIL:-"exit=$rc"}
    detail="target=$target; $detail; log=$id.log"
    detail=${detail//$'\t'/ }
    detail=${detail//$'\r'/ }
    detail=${detail//$'\n'/ }
    if ! printf '%s\t%s\t%s\n' "$id" "${outcomes[$id]}" "$detail" >> "$results_dir/results.tsv"; then
        printf 'FAIL: cannot append results.tsv\n' >&2
        (( failures += 1 ))
    fi
    printf '[%s] %s %s: %s\n' "$target" "$id" "${outcomes[$id]}" "$detail"
}

# Invoke separate bounded processes from deliberately different directories,
# exercising saved state rather than globals retained in this harness.
invoke_perfcollect()
{
    local directory=$1
    local -a launcher=()
    shift
    [[ "${PERFCOLLECT_TEST_SETSID:-0}" != 1 ]] || launcher=(setsid --wait)
    (cd -- "$directory" &&
        timeout -s TERM -k 5s 90s "${launcher[@]}" bash "$perfcollect" "$@")
}

# Confirm target/distro/tool identity and an empty, safe state starting point.
# Never overwrite another session or recover a guard just to make a test run.
environment_case()
{
    local tool distro_id distro_version source_file
    [[ "$argument_count" == 0 ]] || { case_block "no positional arguments are supported"; return 77; }
    [[ "$(uname -s)" == Linux && "$EUID" == 0 ]] ||
        { case_block "requires root inside a disposable Linux container"; return 77; }
    [[ -r /etc/os-release && -f "$perfcollect" && -d "$fixtures" ]] ||
        { case_block "missing real distro identification or /src test snapshot"; return 77; }
    # os-release is distro-owned configuration, not perfcollect session data.
    distro_id=$(. /etc/os-release; printf '%s' "$ID")
    distro_version=$(. /etc/os-release; printf '%s' "$VERSION_ID")
    cat /etc/os-release
    uname -a
    printf 'architecture=%s\n' "$(uname -m)"
    case "$target:$distro_id:$distro_version" in
        ubuntu-20.04:ubuntu:20.04|debian-bookworm:debian:12|ci-debian12:debian:12|ubuntu-24.04:ubuntu:24.04|\
        mariner-2.0:mariner:2.0|alpine:alpine:3.*|azurelinux-3.0:azurelinux:3.0) ;;
        *) case_block "target $target does not match actual distro $distro_id $distro_version"; return 77 ;;
    esac
    for tool in timeout mktemp stat cmp cp rm mkdir grep sed awk find zip unzip cc perf setsid; do
        command -v "$tool" || { case_block "required test tool missing: $tool"; return 77; }
    done
    bash --version
    stat --version 2>&1 || stat --help 2>&1
    mktemp --version 2>&1 || mktemp --help 2>&1
    readlink --version 2>&1 || readlink --help 2>&1
    timeout --version 2>&1 || timeout --help 2>&1
    cc --version
    if command -v tput >/dev/null; then
        tput sgr0 ||
            { case_block "terminal reset capability required by the current CLI is unavailable"; return 77; }
    fi
    local root_home
    root_home=$(cd -P -- ~root && pwd -P) ||
        { case_block "cannot resolve root's account home"; return 77; }
    [[ "$root_home" == /* && "$root_home" != / ]] ||
        { case_block "invalid root account home"; return 77; }
    state_dir="$root_home/.perfcollect"
    state_file="$state_dir/sessioninfo"
    if [[ -e "$state_file" || -L "$state_file" || -e "$state_dir/.operation" || -L "$state_dir/.operation" ]]; then
        case_block "container already has session state/guard; refusing destructive recovery"
        return 77
    fi
    if [[ -e "$state_dir" || -L "$state_dir" ]]; then
        [[ -d "$state_dir" && ! -L "$state_dir" && "$(stat -c '%u %a' -- "$state_dir")" == '0 700' ]] ||
            { case_block "container has an unsafe pre-existing state directory"; return 77; }
    fi
    scratch=$(umask 077; mktemp -d /tmp/perfcollect-integration.XXXXXXXX) ||
        { case_block "cannot create private container scratch"; return 77; }
    token=${scratch##*/}
    mkdir -- "$scratch/start" "$scratch/stop" "$scratch/collect" "$scratch/caller-home" ||
        { case_block "cannot create invocation directories"; return 77; }
    for source_file in "$perfcollect" "$0"; do
        if grep -q $'\r$' "$source_file"; then
            case_fail "CRLF shell snapshot: $source_file must be copied with LF line endings"
            return 1
        fi
        bash -n "$source_file" ||
            { case_fail "snapshot is not valid Linux Bash: $source_file"; return 1; }
    done
    CASE_DETAIL="actual $distro_id $distro_version; state=$state_file; private container scratch"
}

install_case()
{
    invoke_perfcollect "$scratch/start" install ||
        { case_fail "actual perfcollect install failed"; return 1; }
    CASE_DETAIL="actual Mariner perfcollect install succeeded"
}

resolve_real_perf()
{
    local candidate
    # Ubuntu's /usr/bin/perf can be a kernel-version wrapper. Select a real
    # installed binary, not a stub, and keep it at the command-discovery boundary.
    # A container cannot install the host's kernel; version mismatch alone is
    # not a capability failure. The actual record/export probe decides that.
    for candidate in "$(command -v perf)" /usr/lib/linux-tools/*/perf /usr/bin/perf_*; do
        [[ -f "$candidate" && -x "$candidate" ]] || continue
        if timeout 10s "$candidate" --version; then
            perf_binary=$candidate
            export PATH="${candidate%/*}:$PATH"
            printf 'selected actual perf executable: %s\n' "$perf_binary"
            return 0
        fi
    done
    case_block "no installed perf executable works with this host kernel"
}

# Read the exact data-only format and verify its filesystem protection without
# sourcing metadata. Named fields keep the lifecycle assertions readable.
read_state()
{
    local name extra=''
    [[ -d "$state_dir" && ! -L "$state_dir" && "$(stat -c '%u %a' -- "$state_dir")" == '0 700' ]] ||
        { case_fail "state directory must be root-owned mode 0700"; return 1; }
    [[ -f "$state_file" && ! -L "$state_file" && "$(stat -c '%u %a' -- "$state_file")" == '0 600' ]] ||
        { case_fail "state must be a root-owned mode-0600 regular file"; return 1; }
    state_fields=()
    {
        for name in version logFile tempDir lttngSessionName lttngTraceDir useLTTng; do
            IFS= read -r -d '' "state_fields[$name]" ||
                { case_fail "state is not six terminated NUL records"; return 1; }
        done
        if IFS= read -r -d '' extra || [[ -n "$extra" ]]; then
            case_fail "state has extra/trailing data"
            return 1
        fi
    } < "$state_file"
    [[ "${state_fields[version]}" == 1 && "${state_fields[useLTTng]}" =~ ^[01]$ ]] ||
        { case_fail "unsupported state version or LTTng flag"; return 1; }
    [[ "${state_fields[logFile]}" == /* && -s "${state_fields[logFile]}" && ! -L "${state_fields[logFile]}" ]] ||
        { case_fail "saved log is missing, empty, or not an absolute regular-file path"; return 1; }
    [[ -f "${state_fields[logFile]}" && "${state_fields[tempDir]}" == /tmp/* && -d "${state_fields[tempDir]}" &&
        ! -L "${state_fields[tempDir]}" && "$(stat -c '%u %a' -- "${state_fields[tempDir]}")" == '0 700' ]] ||
        { case_fail "saved workspace is not a private root-owned absolute directory"; return 1; }
    if [[ "${state_fields[useLTTng]}" == 1 ]]; then
        [[ "${state_fields[lttngTraceDir]}" == "${state_fields[tempDir]}/lttngTrace" &&
            -d "${state_fields[lttngTraceDir]}" && ! -L "${state_fields[lttngTraceDir]}" &&
            "$(stat -c '%u' -- "${state_fields[lttngTraceDir]}")" == 0 ]] ||
            { case_fail "enabled trace output is not rooted in its private collection workspace"; return 1; }
    fi
    [[ ! -e "$state_dir/.operation" && ! -L "$state_dir/.operation" ]] ||
        { case_fail "completed operation leaked its guard"; return 1; }
    stat -c '%n uid=%u gid=%g mode=%a type=%F' -- "$state_dir" "$state_file" "${state_fields[logFile]}" "${state_fields[tempDir]}"
    printf 'state: version=%q log=%q workspace=%q session=%q trace=%q enabled=%q\n' \
        "${state_fields[version]}" "${state_fields[logFile]}" "${state_fields[tempDir]}" \
        "${state_fields[lttngSessionName]}" "${state_fields[lttngTraceDir]}" "${state_fields[useLTTng]}"
}

# A nonempty archive/metadata file is insufficient: decode CTF using the real
# reader and require this run's named fixture event and unique marker together.
verify_fixture_events()
{
    local trace_directory=$1 marker=$2 output_file=$3
    local metadata
    metadata=$(find "$trace_directory" -type f -name metadata -size +0c -print -quit) || return 1
    [[ -n "$metadata" ]] || { case_fail "no nonempty CTF metadata; a log-only archive is not a trace"; return 1; }
    timeout -s TERM -k 5s 30s "$trace_reader" "$trace_directory" > "$output_file" 2>&1 ||
        { cat "$output_file"; case_fail "babeltrace could not decode the actual CTF trace"; return 1; }
    cat "$output_file"
    awk -v marker="$marker" '
        index($0, "perfcollect_test:tick") && index($0, marker) { found = 1 }
        END { exit !found }
    ' "$output_file" ||
        { case_fail "archived trace has no fixture event with this invocation's marker"; return 1; }
}

# Compile/link against this image's UST library, then prove a full userspace
# create/enable/emit/stop/decode cycle before blaming production for trace failure.
ust_capability_case()
{
    local tool ready=0 attempt
    local -a compile_flags link_flags
    for tool in lttng lttng-sessiond pkg-config; do
        command -v "$tool" || { case_block "required UST test tool missing: $tool"; return 77; }
    done
    trace_reader=$(command -v babeltrace2 || command -v babeltrace) ||
        { case_block "babeltrace2/babeltrace is required to verify real events"; return 77; }
    lttng --version
    "$trace_reader" --version
    pkg-config --exists lttng-ust ||
        { case_block "liblttng-ust development package is unavailable"; return 77; }
    pkg-config --modversion lttng-ust
    resolve_real_perf || return 77
    read -r -a compile_flags <<< "$(pkg-config --cflags lttng-ust)"
    read -r -a link_flags <<< "$(pkg-config --libs lttng-ust)"
    timeout 60s cc -std=c99 -O2 -g -Wall -Wextra -Werror -I "$fixtures" "${compile_flags[@]}" \
        "$fixtures/ust-producer.c" -o "$scratch/ust-producer" "${link_flags[@]}" ||
        { case_fail "could not compile/link real UST fixture against installed libraries"; return 1; }

    if ! timeout 10s lttng list; then
        # A userspace-only daemon needs no host LTTng kernel modules. In
        # particular, production's optional kernel add-context errors are not
        # evidence that userspace tracing is unavailable.
        lttng-sessiond --no-kernel > "$results_dir/lttng-tools-record-and-decode-events.sessiond.log" 2>&1 &
        sessiond_pid=$!
        for (( attempt=0; attempt<100; attempt++ )); do
            if timeout 2s lttng list && kill -0 "$sessiond_pid" 2>/dev/null; then
                ready=1
                break
            fi
            kill -0 "$sessiond_pid" 2>/dev/null || break
            sleep 0.1
        done
        if (( ready == 0 )); then
            cat "$results_dir/lttng-tools-record-and-decode-events.sessiond.log"
            case_block "userspace session daemon did not become ready"
            return 77
        fi
    fi
    local proposed_session="perfcollect-probe-$token"
    timeout 15s lttng create "$proposed_session" --output="$scratch/ust-probe" ||
        { case_block "real userspace UST session creation failed"; return 77; }
    probe_session=$proposed_session

    # Track the probe only after successful creation. Subsequent failures leave
    # cleanup an exact owned session name instead of a destroy-all fallback.
    timeout 15s lttng enable-event --session="$probe_session" --userspace --tracepoint perfcollect_test:tick &&
        timeout 15s lttng start "$probe_session" &&
        timeout 25s env LTTNG_UST_DEBUG=1 "$scratch/ust-producer" "probe-$token" &&
        timeout 15s lttng stop "$probe_session" &&
        timeout 15s lttng destroy "$probe_session" ||
        { case_block "real userspace UST create/enable/start/emit/stop/destroy probe failed"; return 77; }
    probe_session=''
    if ! verify_fixture_events "$scratch/ust-probe" "probe-$token" "$scratch/ust-probe.events"; then
        case_block "host/userspace UST probe did not produce a decodable fixture event"
        return 77
    fi
    CASE_DETAIL="real UST fixture recorded and decoded; no kernel module or .NET dependency"
}

# Start via production, read its saved state, and query the real daemon to
# prove saved metadata describes a session that actually exists.
ust_start_case()
{
    chown 65534:65534 "$scratch/caller-home" || return 1
    HOME="$scratch/caller-home" LTTNG_HOME="$scratch/caller-home" SUDO_USER=nobody \
        invoke_perfcollect "$scratch/start" start ust-integration -noperf -rawevents perfcollect_test:tick ||
        { case_fail "real perfcollect start failed after successful UST capability probe"; return 1; }
    read_state || return 1
    [[ "${state_fields[useLTTng]}" == 1 && -n "${state_fields[lttngSessionName]}" && "${state_fields[lttngSessionName]}" != -* &&
        "${state_fields[lttngTraceDir]}" == /* ]] ||
        { case_fail "start did not persist a real LTTng session"; return 1; }
    active_session=${state_fields[lttngSessionName]}
    ust_workspace=${state_fields[tempDir]}
    ust_trace_dir=${state_fields[lttngTraceDir]}
    [[ "$ust_trace_dir" == "$ust_workspace/lttngTrace" &&
        ! -e "$scratch/caller-home/lttng-traces" ]] ||
        { case_fail "enabled start placed trace output outside its private workspace"; return 1; }
    cp -- "$state_file" "$scratch/ust-state.before" || return 1
    timeout 15s lttng list "$active_session" ||
        { case_fail "the persisted actual LTTng session does not exist after start"; return 1; }
    CASE_DETAIL="real session exists; protected NUL-delimited metadata in $state_file"
}

# The producer waits for enablement and emits a bounded batch with a unique
# archive marker; a successful process without real events is caught at decoding.
ust_emit_case()
{
    timeout 25s "$scratch/ust-producer" "archive-$token" ||
        { case_fail "fixture tracepoint was not enabled/emitted by perfcollect"; return 1; }
    CASE_DETAIL="actual UST fixture emitted 128 uniquely marked events"
}

unsafe_trace_case()
{
    local variant path rc unchanged
    for variant in user-owned symlink missing; do
        case "$variant" in
            user-owned) path="$scratch/caller-home" ;;
            symlink)
                path="$scratch/unsafe-trace"
                ln -s -- "$ust_trace_dir" "$path" || return 1
                ;;
            missing) path="$scratch/missing-trace" ;;
        esac
        printf '%s\0' 1 "${state_fields[logFile]}" "$ust_workspace" "$active_session" "$path" 1 > "$state_file"
        cp -- "$state_file" "$scratch/unsafe-trace-state.before" || return 1
        invoke_perfcollect "$scratch/stop" stop unsafe-trace > "$scratch/unsafe-trace.output" 2>&1
        rc=$?
        cat "$scratch/unsafe-trace.output"
        cmp -s -- "$scratch/unsafe-trace-state.before" "$state_file"
        unchanged=$?
        cp -- "$scratch/ust-state.before" "$state_file" || return 1
        [[ "$rc" == 1 && "$unchanged" == 0 ]] &&
            grep -F 'LTTng trace directory is missing or unsafe' "$scratch/unsafe-trace.output" &&
            timeout 15s lttng list "$active_session" &&
            [[ -d "$ust_workspace" && -d "$ust_trace_dir" && ! -e "$state_dir/.operation" ]] ||
            { case_fail "unsafe $variant output changed the live session or leaked its guard"; return 1; }
    done
    read_state || return 1
    CASE_DETAIL="user-owned, symlink, and missing trace paths rejected before stopping/destroying the actual live session"
}

# Exercise both privilege guards with a real UID switch while a session exists.
nonroot_operation_case()
{
    local operation=$1 rc uid_output
    command -v setpriv ||
        { case_block "setpriv (util-linux) required for actual non-root $operation"; return 77; }
    uid_output=$(timeout 10s setpriv --reuid=65534 --regid=65534 --clear-groups id -u) ||
        { case_block "container cannot switch to an unprivileged UID"; return 77; }
    [[ "$uid_output" == 65534 ]] ||
        { case_block "UID-switch prerequisite did not produce a non-root process"; return 77; }
    timeout 15s setpriv --reuid=65534 --regid=65534 --clear-groups \
        bash "$perfcollect" "$operation" "nonroot-$operation" "${disabled_lttng_options[@]}" -noperf \
        > "$scratch/nonroot-$operation.output" 2>&1
    rc=$?
    cat "$scratch/nonroot-$operation.output"
    [[ "$rc" != 0 && "$rc" != 124 && "$rc" != 137 ]] ||
        { case_fail "non-root $operation succeeded or timed out instead of rejecting"; return 1; }
    grep -qi 'must be run as root' "$scratch/nonroot-$operation.output" ||
        { case_fail "non-root $operation did not reach the explicit root guard"; return 1; }
    if [[ -n "$active_session" ]]; then
        cmp -s -- "$scratch/ust-state.before" "$state_file" &&
            timeout 15s lttng list "$active_session" ||
            { case_fail "non-root $operation changed metadata or the actual live session"; return 1; }
    else
        cmp -s -- "$scratch/disabled-state.before" "$state_file" ||
            { case_fail "non-root $operation changed disabled-session metadata"; return 1; }
    fi
    read_state || return 1
    CASE_DETAIL="actual UID 65534 $operation rejected before state access; active session and guard unchanged"
}

root_start_case()
{
    nonroot_operation_case start
}

root_stop_case()
{
    nonroot_operation_case stop
}

# Stop from another working directory and verify actual daemon-side removal,
# not merely a failed list caused by a missing session daemon.
ust_stop_case()
{
    invoke_perfcollect "$scratch/stop" stop ./ust-integration ||
        { case_fail "real perfcollect stop failed"; return 1; }
    # A missing daemon must not masquerade as successful session removal.
    timeout 15s lttng list ||
        { case_fail "cannot query daemon to establish actual session removal"; return 1; }
    if timeout 15s lttng list "$active_session"; then
        case_fail "actual LTTng session still exists after stop/destroy"
        return 1
    fi
    active_session=''
    ust_archive="$scratch/stop/ust-integration.trace.zip"
    [[ -s "$ust_archive" ]] ||
        { case_fail "stop did not place a nonempty archive in its different working directory"; return 1; }
    CASE_DETAIL="./-prefixed stop removed actual session; archive uses stop process's working directory"
}

# Finalization retains the protected state directory and releases its guard.
verify_finalized_state()
{
    local workspace=$1
    [[ ! -e "$workspace" && ! -L "$workspace" ]] ||
        { case_fail "completed session workspace was not removed"; return 1; }
    [[ ! -e "$state_file" && ! -L "$state_file" ]] ||
        { case_fail "completed session metadata was not removed"; return 1; }
    [[ -d "$state_dir" && ! -L "$state_dir" && "$(stat -c '%u %a' -- "$state_dir")" == '0 700' ]] ||
        { case_fail "private state directory was removed or changed"; return 1; }
    [[ ! -e "$state_dir/.operation" && ! -L "$state_dir/.operation" ]] ||
        { case_fail "successful stop leaked its operation guard"; return 1; }
}

# Extract/test the production ZIP, decode actual archived fixture events, and
# prove a start-only session did not introduce perf capture artifacts.
ust_archive_case()
{
    timeout 30s unzip -t "$ust_archive" &&
        timeout 30s unzip -q "$ust_archive" -d "$scratch/ust-extracted" ||
        { case_fail "UST archive is corrupt or cannot be extracted"; return 1; }
    local trace="$scratch/ust-extracted/ust-integration.trace"
    [[ -s "$trace/perfcollect.log" && -d "$trace/lttngTrace" ]] ||
        { case_fail "archive lacks collection log or actual LTTng traces"; return 1; }
    cat "$trace/perfcollect.log"
    verify_fixture_events "$trace/lttngTrace" "archive-$token" "$scratch/ust-archive.events" || return 1
    [[ ! -e "$trace/perf.data" && ! -e "$trace/perf.data.txt" ]] ||
        { case_fail "start-only stop unexpectedly used perf"; return 1; }
    verify_finalized_state "$ust_workspace" || return 1
    CASE_DETAIL="archive contains decoded fixture event and log; workspace/state removed"
}

# Establish real disabled-session state and snapshot it for isolation checks.
disabled_start_case()
{
    disabled_name=${1:-disabled-integration}
    [[ ! -e "$state_file" && ! -L "$state_file" ]] ||
        { case_block "previous case retained state; refusing to overwrite it"; return 77; }
    resolve_real_perf || return 77
    # On disabled distros, omit -nolttng to prove automatic distro detection.
    HOME="$scratch/caller-home" SUDO_USER=nobody \
        invoke_perfcollect "$scratch/start" start "$disabled_name" "${disabled_lttng_options[@]}" -noperf || return 1
    read_state || return 1
    [[ "${state_fields[useLTTng]}" == 0 && -z "${state_fields[lttngSessionName]}" && -z "${state_fields[lttngTraceDir]}" ]] ||
        { case_fail "LTTng-disabled start did not save disabled flag and empty LTTng fields"; return 1; }
    [[ ! -e "$scratch/caller-home/.perfcollect" && ! -L "$scratch/caller-home/.perfcollect" ]] ||
        { case_fail "start used caller HOME instead of root's account home"; return 1; }
    disabled_workspace=${state_fields[tempDir]}
    disabled_log=${state_fields[logFile]}
    cp -- "$state_file" "$scratch/disabled-state.before" &&
        cp -- "$disabled_log" "$scratch/disabled-log.before" || return 1
    disabled_metadata_stat=$(stat -c '%u:%g:%a:%i:%s:%Y:%Z' -- "$state_file") || return 1
    printf '%s\n' "$token" > "$disabled_workspace/unrelated-session-sentinel" || return 1
    CASE_DETAIL="actual disabled start ignored caller HOME/SUDO_USER; saved private state, log, and workspace sentinel"
}

# Plain stop must restore the saved disabled flag, not mask it with CLI options.
disabled_stop_case()
{
    cmp -s -- "$scratch/disabled-state.before" "$state_file" ||
        { case_fail "unrelated session metadata changed before disabled stop"; return 1; }
    invoke_perfcollect "$scratch/stop" stop "$disabled_name" ||
        { case_fail "plain stop did not restore saved useLTTng=0"; return 1; }
    disabled_archive="$scratch/stop/$disabled_name.trace.zip"
    timeout 30s unzip -t "$disabled_archive" &&
        timeout 30s unzip -q "$disabled_archive" -d "$scratch/$disabled_name-extracted" || return 1
    local trace="$scratch/$disabled_name-extracted/$disabled_name.trace"
    [[ -s "$trace/perfcollect.log" && ! -e "$trace/lttngTrace" &&
        ! -e "$trace/perf.data" && ! -e "$trace/perf.data.txt" ]] ||
        { case_fail "disabled start-only archive has unexpected LTTng/perf data or lacks log"; return 1; }
    grep -Fx "$token" "$trace/unrelated-session-sentinel" || return 1
    cat "$trace/perfcollect.log"
    verify_finalized_state "$disabled_workspace" || return 1
    cp -- "$disabled_archive" "$scratch/disabled-archive.before" || return 1
    CASE_DETAIL="plain disabled stop archived actual log/sentinel; workspace/state removed; private directory retained"
}

preservation_start_case()
{
    disabled_start_case preservation-integration
}

# Corrupt only this container's real session record, invoke the real stop, then
# restore valid bytes even after a failed assertion so later finalization can run.
invalid_state_case()
{
    local variant expected rc failed=0
    for variant in permissions version truncated trailing-shell; do
        cp -- "$scratch/disabled-state.before" "$state_file" || return 1
        case "$variant" in
            permissions)
                chmod 644 "$state_file" || return 1
                expected='root-owned regular file with mode 0600'
                ;;
            version)
                printf '%s\0' 2 "${state_fields[logFile]}" "$disabled_workspace" '' '' 0 > "$state_file"
                expected='Unsupported session state version'
                ;;
            truncated)
                printf '%s\0' 1 "${state_fields[logFile]}" "$disabled_workspace" '' '' > "$state_file"
                printf 0 >> "$state_file"
                expected='Incomplete session state'
                ;;
            trailing-shell)
                printf '$(touch "%s")\n' "$scratch/state-was-executed" >> "$state_file"
                expected='Unexpected trailing session state'
                ;;
        esac
        cp -- "$state_file" "$scratch/rejected-state.before" || return 1
        invoke_perfcollect "$scratch/stop" stop "$disabled_name" > "$scratch/rejected-$variant.output" 2>&1
        rc=$?
        cat "$scratch/rejected-$variant.output"
        if [[ "$rc" != 1 ]] || ! grep -F "$expected" "$scratch/rejected-$variant.output" ||
            ! cmp -s -- "$scratch/rejected-state.before" "$state_file" ||
            ! cmp -s -- "$scratch/disabled-log.before" "$disabled_log" ||
            [[ ! -d "$disabled_workspace" || -e "$scratch/state-was-executed" ||
                -e "$state_dir/.operation" || -L "$state_dir/.operation" ]]; then
            case_fail "stop did not safely reject $variant state and retain recovery artifacts"
            failed=1
        fi
        cp -- "$scratch/disabled-state.before" "$state_file" && chmod 600 "$state_file" || return 1
        (( failed == 0 )) || return 1
    done
    read_state || return 1
    CASE_DETAIL="real stop rejected unsafe mode, version, truncation, and shell payload without execution; state restored"
}

# Interrupt the unmodified CLI at exact ownership/publication boundaries, not a timing
# approximation. The hook never substitutes filesystem or tracing commands.
interrupted_start_case()
{
    local helper="$scratch/interrupt-start.bash" evidence="$scratch/interrupt-state"
    local name="interrupt-${interrupt_signal,,}-$interrupt_boundary"
    local rc expected=143 field saved_log workspace session trace enabled temporary extra=''
    local -a options=("${disabled_lttng_options[@]}")
    [[ ! -e "$state_file" && ! -L "$state_file" &&
        ! -e "$state_dir/.operation" && ! -L "$state_dir/.operation" ]] ||
        { case_block "previous operation retained state/guard; refusing interruption test"; return 77; }
    if [[ "$target" == ubuntu-20.04 || "$target" == debian-bookworm || "$target" == ci-debian12 ]]; then
        options=(-rawevents perfcollect_test:tick)
    fi
    mkdir -p -- "$scratch/interrupt-allocations" || return 1
    cat > "$helper" <<'INTERRUPT_START'
unset BASH_ENV
perfcollect_interrupt_pid=$BASHPID
# Bash keeps BASH_COMMAND at the exit instruction inside EXIT handlers; source
# line metadata still identifies the real cleanup command about to run.
perfcollect_cleanup_lines=$(awk '/RunSilentArguments.*destroy.*lttngSessionName|rm -rf -- "\$sessionWorkspace"/ {print NR}' \
    "$PERFCOLLECT_INTERRUPT_SCRIPT") || exit 1
[[ -n "$perfcollect_cleanup_lines" ]] || exit 1
perfcollect_cleanup_lines=" ${perfcollect_cleanup_lines//$'\n'/ } "
perfcollect_inject()
{
    local workspace=${tempDir:-} temporary=$sessionStateTemporary
    if [[ -z "$workspace" ]]; then
        workspace=$(find "$PERFCOLLECT_INTERRUPT_TMPDIR" -mindepth 1 -maxdepth 1 -type d -name 'tmp.*' -print)
    fi
    if [[ -z "$temporary" ]]; then
        temporary=$(find "$sessionDirectory" -maxdepth 1 -name '.state.*' -print)
    fi
    if [[ "$PERFCOLLECT_INTERRUPT_BOUNDARY" != cleanup-repeat || "$1" == cleanup ]]; then
        trap - DEBUG
        set +T
    fi
    if [[ "$1" != cleanup ]]; then
        printf '%s\0' "$logFile" "$workspace" "$lttngSessionName" "$lttngTraceDir" \
            "$useLTTng" "$temporary" > "$PERFCOLLECT_INTERRUPT_EVIDENCE" || exit 1
    else
        printf '%s\n' "$PERFCOLLECT_INTERRUPT_SIGNAL" > "$PERFCOLLECT_INTERRUPT_EVIDENCE.repeated" || exit 1
        trap -p INT TERM >&2
    fi
    printf 'Injecting %s at %s (%s) in CLI PID %s from %s\n' "$PERFCOLLECT_INTERRUPT_SIGNAL" \
        "$PERFCOLLECT_INTERRUPT_BOUNDARY" "$1" "$perfcollect_interrupt_pid" "$BASHPID" >&2
    # setsid makes this the exact CLI group, including any allocating subprocess.
    kill -s "$PERFCOLLECT_INTERRUPT_SIGNAL" -- "-$perfcollect_interrupt_pid"
}
perfcollect_interrupt()
{
    local command=$1
    shift
    if [[ "$BASHPID" != "$perfcollect_interrupt_pid" &&
        "${FUNCNAME[1]:-}" == SessionAllocate && "$command" == 'return "$status"' ]]; then
        perfcollect_interrupt_allocator "$@"
        return 0
    fi
    [[ "$BASHPID" == "$perfcollect_interrupt_pid" ]] || return 0
    if [[ "$PERFCOLLECT_INTERRUPT_BOUNDARY" == cleanup-repeat && "${FUNCNAME[1]:-}" == SessionExit &&
        "$perfcollect_cleanup_lines" == *" ${BASH_LINENO[0]} "* ]]; then
        perfcollect_inject cleanup
        return
    fi
    case "$PERFCOLLECT_INTERRUPT_BOUNDARY:${FUNCNAME[1]:-}:$command" in
        'guard-created:AcquireSessionGuard:sessionGuardHeld=1'|\
        'workspace-created:DoCollect:sessionWorkspace=$tempDir'|\
        'lttng-created:CreateLTTngSession:sessionLTTngCreated=1'|\
        'before-publish:SaveSessionState:mv -T -- "$sessionStateTemporary" "$sessionStateFile"'|\
        "after-publish:SaveSessionState:sessionStateTemporary=''"|\
        'after-temporary-clear:SaveSessionState:sessionStartPending=0') ;;
        *) return 0 ;;
    esac
    perfcollect_inject boundary
}
perfcollect_interrupt_allocator()
{
    [[ "$BASHPID" != "$perfcollect_interrupt_pid" ]] || return 0
    printf 'Allocator completed in PID %s: %s\n' "$BASHPID" "$*" >&2
    case "$PERFCOLLECT_INTERRUPT_BOUNDARY:$*" in
        'guard-allocator-exit:mkdir -- '*'/'.operation) ;;
        'workspace-allocator-exit:mktemp -d') ;;
        'state-allocator-exit:mktemp '*'/'.state.*|'cleanup-repeat:mktemp '*'/'.state.*) ;;
        'lttng-allocator-exit:RunSilentArguments '*)
            [[ "${3:-}" == create ]] || return 0 ;;
        *) return 0 ;;
    esac
    perfcollect_inject allocator-exit
}
set -T
trap 'perfcollect_interrupt "$BASH_COMMAND" "$@"' DEBUG
INTERRUPT_START
    [[ "$interrupt_signal" != INT ]] || expected=130
    TMPDIR="$scratch/interrupt-allocations" BASH_ENV="$helper" PERFCOLLECT_INTERRUPT_BOUNDARY="$interrupt_boundary" \
        PERFCOLLECT_INTERRUPT_SIGNAL="$interrupt_signal" PERFCOLLECT_INTERRUPT_EVIDENCE="$evidence" \
        PERFCOLLECT_INTERRUPT_TMPDIR="$scratch/interrupt-allocations" PERFCOLLECT_INTERRUPT_SCRIPT="$perfcollect" \
        PERFCOLLECT_TEST_SETSID=1 \
        invoke_perfcollect "$scratch/start" start "$name" -noperf "${options[@]}"
    rc=$?
    [[ "$rc" == "$expected" && -s "$evidence" ]] ||
        { case_fail "interrupted start returned $rc, expected $expected with boundary evidence"; return 1; }
    {
        for field in saved_log workspace session trace enabled temporary; do
            IFS= read -r -d '' "$field" ||
                { case_fail "interruption hook did not capture exact owned resources"; return 1; }
        done
        if IFS= read -r -d '' extra || [[ -n "$extra" ]]; then
            case_fail "unexpected interruption evidence"
            return 1
        fi
    } < "$evidence"
    rm -- "$evidence" || return 1
    active_session=$session
    [[ ! -e "$state_dir/.operation" && ! -L "$state_dir/.operation" &&
        -z "$(find "$state_dir" -maxdepth 1 -name '.state.*' -print -quit)" ]] ||
        { case_fail "interruption leaked the guard or unpublished state"; return 1; }
    if [[ "$interrupt_boundary" != after-publish && "$interrupt_boundary" != after-temporary-clear ]]; then
        [[ ! -e "$state_file" && ! -L "$state_file" && ! -e "$workspace" && ! -L "$workspace" ]] ||
            { case_fail "interruption before publication did not roll back state/workspace"; return 1; }
        if [[ -n "$session" ]]; then
            timeout 15s lttng list ||
                { case_fail "cannot query daemon to verify rollback"; return 1; }
            if timeout 15s lttng list "$session"; then
                case_fail "interrupted unpublished start retained its actual LTTng session"
                return 1
            fi
        fi
        active_session=''
        [[ -z "$(find "$scratch/interrupt-allocations" -mindepth 1 -print -quit)" ]] ||
            { case_fail "interrupted allocation leaked a workspace"; return 1; }
        if [[ "$interrupt_boundary" == cleanup-repeat ]]; then
            [[ -n "$temporary" ]] && grep -Fx "$interrupt_signal" "$evidence.repeated" &&
                rm -- "$evidence.repeated" ||
                { case_fail "repeated signal did not enter rollback with allocated state"; return 1; }
        fi
        CASE_DETAIL="$interrupt_signal exit=$rc before publication; owned session/workspace/temp rolled back; guard released"
        return 0
    fi
    read_state || return 1
    [[ "${state_fields[logFile]}" == "$saved_log" && "${state_fields[tempDir]}" == "$workspace" &&
        "${state_fields[lttngSessionName]}" == "$session" && "${state_fields[lttngTraceDir]}" == "$trace" &&
        "${state_fields[useLTTng]}" == "$enabled" ]] ||
        { case_fail "interrupted published state does not describe the retained resources"; return 1; }
    if [[ "$enabled" == 1 ]]; then
        timeout 15s lttng list "$session" &&
            timeout 25s "$scratch/ust-producer" "$name-$token" ||
            { case_fail "published session is not live after interruption"; return 1; }
    fi
    invoke_perfcollect "$scratch/stop" stop "./$name" ||
        { case_fail "published interrupted start cannot be finalized by normal stop"; return 1; }
    local archive="$scratch/stop/$name.trace.zip"
    local extracted="$scratch/$name-extracted"
    timeout 30s unzip -t "$archive" && timeout 30s unzip -q "$archive" -d "$extracted" ||
        { case_fail "interrupted session archive is corrupt or missing"; return 1; }
    [[ -s "$extracted/$name.trace/perfcollect.log" &&
        ! -e "$extracted/$name.trace/perf.data" && ! -e "$extracted/$name.trace/perf.data.txt" ]] ||
        { case_fail "interrupted start archive lacks its log or unexpectedly captured perf"; return 1; }
    if [[ "$enabled" == 1 ]]; then
        verify_fixture_events "$extracted/$name.trace/lttngTrace" "$name-$token" "$scratch/$name.events" || return 1
        timeout 15s lttng list ||
            { case_fail "cannot query daemon after interrupted session stop"; return 1; }
        if timeout 15s lttng list "$session"; then
            case_fail "stop retained the interrupted start's actual LTTng session"
            return 1
        fi
    else
        [[ ! -e "$extracted/$name.trace/lttngTrace" ]] ||
            { case_fail "disabled interrupted start archived unexpected LTTng data"; return 1; }
    fi
    active_session=''
    verify_finalized_state "$workspace" || return 1
    CASE_DETAIL="$interrupt_signal exit=$rc at $interrupt_boundary; published resources retained, archived, and finalized by plain stop"
}

absolute_stop_case()
{
    local destination="$scratch/absolute output/stopped" workspace session enabled trace
    local -a options=("${disabled_lttng_options[@]}")
    case "$target" in
        ubuntu-20.04|debian-bookworm|ci-debian12) options=(-rawevents perfcollect_test:tick) ;;
    esac
    mkdir -p -- "${destination%/*}" || return 1
    HOME="$scratch/caller-home" LTTNG_HOME="$scratch/caller-home" \
        invoke_perfcollect "$scratch/start" start absolute-integration -noperf "${options[@]}" || return 1
    read_state || return 1
    workspace=${state_fields[tempDir]}
    session=${state_fields[lttngSessionName]}
    enabled=${state_fields[useLTTng]}
    active_session=$session
    printf '%s\n' "$token" > "$workspace/absolute-sentinel" || return 1
    if [[ "$enabled" == 1 ]]; then
        timeout 25s "$scratch/ust-producer" "absolute-stop-$token" || return 1
    fi
    invoke_perfcollect "$scratch/stop" stop "$destination" || return 1
    timeout 30s unzip -t "$destination.trace.zip" &&
        timeout 30s unzip -q "$destination.trace.zip" -d "$scratch/absolute-stop-extracted" || return 1
    trace=$(find "$scratch/absolute-stop-extracted" -type f -name perfcollect.log -print) || return 1
    [[ -n "$trace" && "$trace" != *$'\n'* && -s "$trace" && ! -e "$destination.trace" ]] ||
        { case_fail "absolute stop lost its log or retained external staging"; return 1; }
    trace=${trace%/*}
    grep -Fx "$token" "$trace/absolute-sentinel" &&
        [[ ! -e "$trace/perf.data" ]] || return 1
    if [[ "$enabled" == 1 ]]; then
        verify_fixture_events "$trace/lttngTrace" "absolute-stop-$token" "$scratch/absolute-stop.events" || return 1
        timeout 15s lttng list || return 1
        if timeout 15s lttng list "$session"; then
            case_fail "absolute stop retained its LTTng session"
            return 1
        fi
    fi
    active_session=''
    verify_finalized_state "$workspace" || return 1
    CASE_DETAIL="absolute stop destination with spaces contains actual session data; staging/workspace/state/guard finalized"
}

absolute_collect_case()
{
    local destination="$scratch/absolute output/collected" rc emitter_rc=0 collect_pid trace workspace
    mkdir -p -- "${destination%/*}" || return 1
    case "$target" in
        ubuntu-20.04|debian-bookworm|ci-debian12)
            HOME="$scratch/caller-home" LTTNG_HOME="$scratch/caller-home" \
                invoke_perfcollect "$scratch/collect" collect "$destination" -noperf \
                    -rawevents perfcollect_test:tick -collectsec 5 > "$scratch/absolute-collect.output" 2>&1 &
            collect_pid=$!
            timeout 25s "$scratch/ust-producer" "absolute-collect-$token" || emitter_rc=$?
            wait "$collect_pid"
            rc=$?
            cat "$scratch/absolute-collect.output"
            ;;
        *)
            start_cpu absolute-collect || return 1
            invoke_perfcollect "$scratch/collect" collect "$destination" "${disabled_lttng_options[@]}" \
                -pid "$cpu_pid" -collectsec 3
            rc=$?
            stop_cpu
            ;;
    esac
    [[ "$rc" == 0 && "$emitter_rc" == 0 ]] ||
        { case_fail "real absolute collect or UST emission failed"; return 1; }
    timeout 30s unzip -t "$destination.trace.zip" &&
        timeout 30s unzip -q "$destination.trace.zip" -d "$scratch/absolute-collect-extracted" || return 1
    trace=$(find "$scratch/absolute-collect-extracted" -type f -name perfcollect.log -print) || return 1
    [[ -n "$trace" && "$trace" != *$'\n'* && -s "$trace" && ! -e "$destination.trace" ]] || return 1
    workspace=$(sed -n 's/^Created temp directory //p' "$trace")
    [[ "$workspace" == /tmp/tmp.* && ! -e "$workspace" && ! -e "$state_file" && ! -e "$state_dir/.operation" ]] ||
        { case_fail "absolute collect retained workspace/state/guard"; return 1; }
    trace=${trace%/*}
    case "$target" in
        ubuntu-20.04|debian-bookworm|ci-debian12)
            verify_fixture_events "$trace/lttngTrace" "absolute-collect-$token" "$scratch/absolute-collect.events" || return 1
            ;;
        *)
            [[ -s "$trace/perf.data" && -s "$trace/perf.data.txt" ]] &&
                grep -F perfcollect_cpu "$trace/perf.data.txt" &&
                timeout 20s "$perf_binary" script -i "$trace/perf.data" > "$scratch/absolute-perf.events" &&
                grep -F perfcollect_cpu "$scratch/absolute-perf.events" || return 1
            ;;
    esac
    CASE_DETAIL="absolute collect destination with spaces contains independently decoded real traces; staging/workspace finalized"
}

# Launch only the compiled test workload and wait for its explicit readiness
# record; track its PID for bounded cleanup rather than killing by executable name.
start_cpu()
{
    local attempt
    "$scratch/perfcollect_cpu" 60 > "$results_dir/$1.cpu.log" 2>&1 &
    cpu_pid=$!
    for (( attempt=0; attempt<100; attempt++ )); do
        if grep -q '^ready pid=' "$results_dir/$1.cpu.log"; then
            return 0
        fi
        kill -0 "$cpu_pid" 2>/dev/null || break
        sleep 0.05
    done
    case_fail "CPU fixture did not become ready"
}

# Terminate/reap only that workload, escalating from TERM if it does not exit.
stop_cpu()
{
    local attempt
    [[ -n "$cpu_pid" ]] || return 0
    if kill -0 "$cpu_pid" 2>/dev/null; then
        kill -TERM "$cpu_pid" || return 1
        for (( attempt=0; attempt<50; attempt++ )); do
            kill -0 "$cpu_pid" 2>/dev/null || break
            sleep 0.1
        done
        if kill -0 "$cpu_pid" 2>/dev/null; then
            kill -KILL "$cpu_pid" || return 1
        fi
    fi
    wait "$cpu_pid" 2>/dev/null
    cpu_pid=''
}

# Probe actual software sampling, JIT injection, and export without modifying
# host sysctls. Missing permissions/kernel support are mandatory blocked coverage.
perf_capability_case()
{
    local rc setting
    resolve_real_perf || return 77
    grep -E '^(Cap(Inh|Prm|Eff|Bnd|Amb)|Seccomp)' /proc/self/status
    for setting in perf_event_paranoid kptr_restrict; do
        if [[ -r "/proc/sys/kernel/$setting" ]]; then
            printf '%s=' "$setting"
            cat "/proc/sys/kernel/$setting"
        fi
    done
    # Do not change host-wide permissions/sysctls or assume container privilege
    # implies working perf. Probe the actual software sampling/clock/stack flags.
    timeout 60s cc -std=c99 -O2 -g -fno-omit-frame-pointer -Wall -Wextra -Werror \
        "$fixtures/cpu-workload.c" -o "$scratch/perfcollect_cpu" ||
        { case_fail "could not compile CPU fixture"; return 1; }
    start_cpu perf-tools-record-and-export-samples || { case_block "CPU fixture unavailable"; return 77; }
    timeout -s TERM -k 5s 20s "$perf_binary" record \
        -o "$scratch/probe-perf.data" -k 1 -g --pid="$cpu_pid" -F 1000 -e cpu-clock -- sleep 1
    rc=$?
    stop_cpu
    (( rc == 0 )) && [[ -s "$scratch/probe-perf.data" ]] ||
        { case_block "host perf sampling unavailable (permissions/seccomp/kernel-tool mismatch); record exit=$rc"; return 77; }
    # Default fields establish that the host can export real samples. The
    # subsequent production archive assertions independently test its field
    # selection and fallbacks (including hosts without the sampled CPU field).
    timeout 20s "$perf_binary" inject --input "$scratch/probe-perf.data" --jit --output "$scratch/probe-perf-jit.data" &&
        timeout 20s "$perf_binary" script -i "$scratch/probe-perf-jit.data" > "$scratch/probe-perf.txt" ||
        { case_block "host perf inject/export unavailable"; return 77; }
    [[ -s "$scratch/probe-perf.txt" ]] && grep -F perfcollect_cpu "$scratch/probe-perf.txt" ||
        { case_block "host perf probe contains no exported CPU fixture samples"; return 77; }
    CASE_DETAIL="real CPU samples recorded, injected, and exported; host settings not altered"
}

# Collect real samples while an independent start/stop session remains active.
perf_collect_case()
{
    local rc
    start_cpu perf-collect-creates-trace-archive || return 1
    perf_collect_attempted=1
    invoke_perfcollect "$scratch/collect" collect ./cpu-integration "${disabled_lttng_options[@]}" -pid "$cpu_pid" -collectsec 3
    rc=$?
    stop_cpu
    (( rc == 0 )) ||
        { case_fail "real perfcollect collect failed after successful host capability probe"; return 1; }
    [[ -s "$scratch/collect/cpu-integration.trace.zip" ]] ||
        { case_fail "real perf collect produced no nonempty archive"; return 1; }
    CASE_DETAIL="./-prefixed real perf collect produced an archive while a separate disabled session remained active"
}

# Require raw data, exported data, and log bytes, then decode raw data again to
# reproduce the fixture samples independently of production's exported text.
perf_archive_case()
{
    local archive="$scratch/collect/cpu-integration.trace.zip"
    local trace="$scratch/perf-extracted/cpu-integration.trace"
    timeout 30s unzip -t "$archive" &&
        timeout 30s unzip -q "$archive" -d "$scratch/perf-extracted" || return 1
    [[ -s "$trace/perf.data" && -s "$trace/perf.data.txt" && -s "$trace/perfcollect.log" ]] ||
        { case_fail "perf archive lacks nonempty raw data, exported data, or collection log"; return 1; }
    cat "$trace/perfcollect.log"
    grep -F perfcollect_cpu "$trace/perf.data.txt" ||
        { case_fail "archived export contains no real CPU fixture sample"; return 1; }
    timeout 20s "$perf_binary" script -i "$trace/perf.data" > "$scratch/archived-raw-perf.txt" &&
        grep -F perfcollect_cpu "$scratch/archived-raw-perf.txt" ||
        { case_fail "archived raw perf.data cannot reproduce actual CPU fixture samples"; return 1; }
    CASE_DETAIL="nonempty perf.data/perf.data.txt and log; actual fixture samples decoded from archived raw data"
}

# Verify collection removes its own workspace and preserves an earlier archive.
perf_state_case()
{
    (( perf_collect_attempted == 1 )) ||
        { case_block "actual collect did not execute; cannot check its finalization"; return 77; }
    cmp -s -- "$scratch/disabled-archive.before" "$disabled_archive" ||
        { case_fail "ordinary collect changed the completed disabled-session archive"; return 1; }
    local workspace
    workspace=$(sed -n 's/^Created temp directory //p' "$scratch/perf-extracted/cpu-integration.trace/perfcollect.log")
    [[ "$workspace" == /tmp/tmp.* && "$workspace" != *$'\n'* ]] ||
        { case_fail "collection log did not identify exactly one actual temporary workspace"; return 1; }
    [[ ! -e "$workspace" && ! -L "$workspace" ]] ||
        { case_fail "completed collect workspace was not removed"; return 1; }
    CASE_DETAIL="collect workspace removed; completed disabled-session archive unchanged"
}

# A failed collect must also preserve the active session; require an actual
# attempt, not a successful archive, before checking the retained artifacts.
active_state_case()
{
    (( perf_collect_attempted == 1 )) ||
        { case_block "actual collect did not execute; cannot check session isolation"; return 77; }
    cmp -s -- "$scratch/disabled-state.before" "$state_file" &&
        [[ "$(stat -c '%u:%g:%a:%i:%s:%Y:%Z' -- "$state_file")" == "$disabled_metadata_stat" ]] &&
        cmp -s -- "$scratch/disabled-log.before" "$disabled_log" &&
        [[ -d "$disabled_workspace" ]] &&
        grep -Fx "$token" "$disabled_workspace/unrelated-session-sentinel" ||
        { case_fail "ordinary collect changed separate session state, log, or workspace"; return 1; }
    read_state || return 1
    CASE_DETAIL="active session bytes/inode/ownership/mode/timestamps, log, sentinel, and released guard unchanged"
}

# Reclaim only individually tracked test resources. Recoverable production
# state/artifacts are not automatically guessed or deleted after a failed case.
cleanup_case()
{
    local rc=0 attempt
    stop_cpu || rc=1
    # Destroy only individually tracked sessions created by this test. Never
    # destroy all sessions, guess stale state, or remove referenced workspaces.
    if [[ -n "$probe_session" ]]; then
        timeout 15s lttng destroy "$probe_session" || rc=1
    fi
    if [[ -n "$active_session" ]]; then
        if timeout 10s lttng list "$active_session" >/dev/null 2>&1; then
            timeout 15s lttng destroy "$active_session" || rc=1
        fi
    fi
    if [[ -n "$sessiond_pid" ]]; then
        if kill -0 "$sessiond_pid" 2>/dev/null; then
            kill -TERM "$sessiond_pid" || rc=1
            for (( attempt=0; attempt<50; attempt++ )); do
                kill -0 "$sessiond_pid" 2>/dev/null || break
                sleep 0.1
            done
            if kill -0 "$sessiond_pid" 2>/dev/null; then
                kill -KILL "$sessiond_pid" || rc=1
            fi
        fi
        wait "$sessiond_pid" 2>/dev/null || :
    fi
    if [[ -n "$scratch" ]]; then
        [[ "$scratch" == /tmp/perfcollect-integration.* && -d "$scratch" && ! -L "$scratch" ]] &&
            rm -rf -- "$scratch" || rc=1
    fi
    (( rc == 0 )) || { case_fail "could not reclaim tracked test-owned process/session/scratch"; return 1; }
    CASE_DETAIL="tracked processes/scratch reclaimed; results and private .perfcollect retained"
}

# Always retain a cleanup case; combine its outcome, prior FAIL/BLOCKED rows,
# and the incoming exit status so missing capability never yields suite success.
finish()
{
    local rc=$?
    trap - EXIT INT TERM
    run_case test-owned-resources-are-cleaned-up cleanup_case
    printf '[%s] integration failures=%s blocked=%s\n' "$target" "$failures" "$blocked"
    if (( failures != 0 || blocked != 0 || rc != 0 )); then
        exit 1
    fi
    exit 0
}

trap finish EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

# Target selection defines two real-tool paths. Stop/cleanup stages depend on a
# successful start, not successful emission, so failed assertions still finalize
# a known live session whenever possible.
run_case environment-meets-prerequisites environment_case
if [[ $target == mariner-2.0 ]]; then
    run_case install-completes-successfully install_case environment-meets-prerequisites
fi
case "$target" in
    ubuntu-20.04|debian-bookworm|ci-debian12)
        run_case lttng-tools-record-and-decode-events ust_capability_case environment-meets-prerequisites
        run_case lttng-start-creates-live-session ust_start_case lttng-tools-record-and-decode-events
        run_case lttng-fixture-emits-marked-events ust_emit_case lttng-start-creates-live-session
        run_case lttng-stop-rejects-unsafe-trace-output unsafe_trace_case lttng-start-creates-live-session
        run_case session-start-rejects-nonroot-user root_start_case lttng-start-creates-live-session
        run_case session-stop-rejects-nonroot-user root_stop_case lttng-start-creates-live-session
        # Still finalize a started session if emission or root rejection failed.
        run_case lttng-stop-destroys-session-and-creates-archive ust_stop_case lttng-start-creates-live-session
        run_case lttng-archive-contains-decodable-events ust_archive_case lttng-stop-destroys-session-and-creates-archive
        # The disabled lifecycle remains independently runnable even when the
        # host's real UST capability is blocked.
        run_case lttng-disabled-start-saves-session-state disabled_start_case environment-meets-prerequisites
        run_case session-stop-rejects-invalid-state invalid_state_case lttng-disabled-start-saves-session-state
        run_case lttng-disabled-stop-archives-and-cleans-up disabled_stop_case lttng-disabled-start-saves-session-state
        ;;
    ubuntu-24.04|mariner-2.0|alpine|azurelinux-3.0)
        run_case perf-tools-record-and-export-samples perf_capability_case environment-meets-prerequisites
        # This disabled session is real production state, not fabricated data.
        run_case lttng-disabled-start-saves-session-state disabled_start_case environment-meets-prerequisites
        run_case session-start-rejects-nonroot-user root_start_case lttng-disabled-start-saves-session-state
        run_case session-stop-rejects-nonroot-user root_stop_case lttng-disabled-start-saves-session-state
        run_case session-stop-rejects-invalid-state invalid_state_case lttng-disabled-start-saves-session-state
        run_case lttng-disabled-stop-archives-and-cleans-up disabled_stop_case lttng-disabled-start-saves-session-state
        run_case preservation-session-starts-before-collect preservation_start_case lttng-disabled-stop-archives-and-cleans-up
        run_case perf-collect-creates-trace-archive perf_collect_case perf-tools-record-and-export-samples preservation-session-starts-before-collect
        run_case perf-archive-contains-decodable-samples perf_archive_case perf-collect-creates-trace-archive
        run_case perf-collect-cleans-up-and-preserves-prior-archive perf_state_case perf-archive-contains-decodable-samples lttng-disabled-stop-archives-and-cleans-up
        run_case perf-collect-preserves-active-session active_state_case preservation-session-starts-before-collect
        run_case preservation-session-stops-after-collect disabled_stop_case preservation-session-starts-before-collect
        ;;
esac

run_case absolute-stop-archives-and-cleans-up absolute_stop_case lttng-disabled-stop-archives-and-cleans-up
case "$target" in
    ubuntu-20.04|debian-bookworm|ci-debian12)
        run_case absolute-collect-archives-decodable-traces absolute_collect_case lttng-tools-record-and-decode-events
        ;;
    *)
        run_case absolute-collect-archives-decodable-traces absolute_collect_case perf-tools-record-and-export-samples
        ;;
esac

for interrupt_signal in TERM INT; do
    interrupt_boundaries=(guard-created guard-allocator-exit workspace-created workspace-allocator-exit
        state-allocator-exit cleanup-repeat before-publish after-publish after-temporary-clear)
    case "$target" in
        ubuntu-20.04|debian-bookworm|ci-debian12)
            interrupt_boundaries+=(lttng-created lttng-allocator-exit)
            ;;
    esac
    for interrupt_boundary in "${interrupt_boundaries[@]}"; do
        case "$target" in
            ubuntu-20.04|debian-bookworm|ci-debian12)
                run_case "session-start-${interrupt_signal,,}-$interrupt_boundary" interrupted_start_case lttng-tools-record-and-decode-events
                ;;
            ubuntu-24.04|mariner-2.0|alpine|azurelinux-3.0)
                run_case "session-start-${interrupt_signal,,}-$interrupt_boundary" interrupted_start_case environment-meets-prerequisites
                ;;
        esac
    done
done
