#!/bin/bash

# Run only inside a disposable Linux test container. YAML schedules CI jobs;
# run-container.sh owns each container. Local tracing runs must be serial.
#
# This harness exercises the actual perfcollect CLI with real tools/events, not
# simulated tracing responses. It first proves host capabilities,
# then checks lifecycle/state, actual archive contents, and session-owned cleanup.
# A distinct LTTng-disabled session also proves ordinary perf collect leaves unrelated
# metadata, log bytes, and workspace intact.
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
declare -a state_fields=()
failures=0
blocked=0
CASE_DETAIL=''
scratch=''
root_home=''
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
disabled_metadata_stat=''
disabled_archive=''
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
    shift
    (cd -- "$directory" &&
        timeout -s TERM -k 5s 90s bash "$perfcollect" "$@")
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
    for tool in timeout mktemp stat cmp cp rm mkdir grep sed awk find zip unzip cc perf; do
        command -v "$tool" || { case_block "required test tool missing: $tool"; return 77; }
    done
    bash --version
    stat --version 2>&1 || stat --help 2>&1
    mktemp --version 2>&1 || mktemp --help 2>&1
    readlink --version 2>&1 || readlink --help 2>&1
    timeout --version 2>&1 || timeout --help 2>&1
    cc --version
    # Account-database resolution intentionally does not use caller HOME.
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
            { case_block "container has unsafe pre-existing private state directory"; return 77; }
    fi
    scratch=$(umask 077; mktemp -d /tmp/perfcollect-integration.XXXXXXXX) ||
        { case_block "cannot create private container scratch"; return 77; }
    token=${scratch##*/}
    mkdir -- "$scratch/start" "$scratch/stop" "$scratch/collect" ||
        { case_block "cannot create invocation directories"; return 77; }
    for source_file in "$perfcollect" "$0"; do
        if grep -q $'\r$' "$source_file"; then
            case_fail "CRLF shell snapshot: $source_file must be copied with LF line endings"
            return 1
        fi
        bash -n "$source_file" ||
            { case_fail "snapshot is not valid Linux Bash: $source_file"; return 1; }
    done
    CASE_DETAIL="actual $distro_id $distro_version; root-home=$root_home; private container scratch"
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

# Inspect actual six-field metadata and filesystem modes without executing any
# stored content. Later cases retain these bytes/paths as independent evidence.
read_state()
{
    local index extra=''
    [[ -d "$state_dir" && ! -L "$state_dir" && "$(stat -c '%u %a' -- "$state_dir")" == '0 700' ]] ||
        { case_fail "private root-home directory must be root-owned mode 0700"; return 1; }
    [[ -f "$state_file" && ! -L "$state_file" && "$(stat -c '%u %a' -- "$state_file")" == '0 600' ]] ||
        { case_fail "new-location state must be a root-owned mode-0600 regular file"; return 1; }
    state_fields=()
    {
        for (( index=0; index<6; index++ )); do
            IFS= read -r -d '' "state_fields[$index]" ||
                { case_fail "state is not six terminated NUL records"; return 1; }
        done
        if IFS= read -r -d '' extra || [[ -n "$extra" ]]; then
            case_fail "state has extra/trailing data"
            return 1
        fi
    } < "$state_file"
    [[ "${state_fields[0]}" == 1 ]] ||
        { case_fail "unsupported state version"; return 1; }
    [[ "${state_fields[1]}" == /* && -s "${state_fields[1]}" && ! -L "${state_fields[1]}" ]] ||
        { case_fail "saved log is missing, empty, or not an absolute regular-file path"; return 1; }
    [[ -f "${state_fields[1]}" && "${state_fields[2]}" == /* && -d "${state_fields[2]}" &&
        ! -L "${state_fields[2]}" && "$(stat -c '%u %a' -- "${state_fields[2]}")" == '0 700' ]] ||
        { case_fail "saved workspace is not a private root-owned absolute directory"; return 1; }
    stat -c '%n uid=%u gid=%g mode=%a type=%F' -- "$state_dir" "$state_file" "${state_fields[1]}" "${state_fields[2]}"
    printf 'state: version=%q log=%q workspace=%q session=%q trace=%q enabled=%q\n' "${state_fields[@]}"
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
        lttng-sessiond --no-kernel > "$results_dir/IT-UST-CAP.sessiond.log" 2>&1 &
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
            cat "$results_dir/IT-UST-CAP.sessiond.log"
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

# Start via production, read the new data contract, and query the real daemon to
# prove saved metadata describes a session that actually exists.
ust_start_case()
{
    invoke_perfcollect "$scratch/start" start ust-integration -noperf -rawevents perfcollect_test:tick ||
        { case_fail "real perfcollect start failed after successful UST capability probe"; return 1; }
    read_state || return 1
    [[ "${state_fields[5]}" == 1 && -n "${state_fields[3]}" && "${state_fields[3]}" != -* &&
        "${state_fields[4]}" == /* ]] ||
        { case_fail "start did not persist an enabled real LTTng session"; return 1; }
    active_session=${state_fields[3]}
    ust_workspace=${state_fields[2]}
    ust_trace_dir=${state_fields[4]}
    cp -- "$state_file" "$scratch/ust-state.before" || return 1
    timeout 15s lttng list "$active_session" ||
        { case_fail "the persisted actual LTTng session does not exist after start"; return 1; }
    CASE_DETAIL="real session exists; six literal NUL records in $state_file"
}

# The producer waits for enablement and emits a bounded batch with a unique
# archive marker; a successful process without real events is caught at decoding.
ust_emit_case()
{
    timeout 25s "$scratch/ust-producer" "archive-$token" ||
        { case_fail "fixture tracepoint was not enabled/emitted by perfcollect"; return 1; }
    CASE_DETAIL="actual UST fixture emitted 128 uniquely marked events"
}

# Prove a real non-root UID reaches the explicit privilege guard promptly and
# does not change saved metadata, the live session, or operation-guard state.
root_stop_case()
{
    local rc uid_output
    command -v setpriv ||
        { case_block "setpriv (util-linux) required for actual non-root stop"; return 77; }
    uid_output=$(timeout 10s setpriv --reuid=65534 --regid=65534 --clear-groups id -u) ||
        { case_block "container cannot switch to an unprivileged UID"; return 77; }
    [[ "$uid_output" == 65534 ]] ||
        { case_block "UID-switch prerequisite did not produce a non-root process"; return 77; }
    timeout 15s setpriv --reuid=65534 --regid=65534 --clear-groups \
        bash "$perfcollect" stop nonroot-stop > "$scratch/nonroot-stop.output" 2>&1
    rc=$?
    cat "$scratch/nonroot-stop.output"
    [[ "$rc" != 0 && "$rc" != 124 && "$rc" != 137 ]] ||
        { case_fail "non-root stop succeeded or timed out instead of rejecting"; return 1; }
    grep -qi 'must be run as root' "$scratch/nonroot-stop.output" ||
        { case_fail "non-root stop did not reach the explicit root guard"; return 1; }
    if [[ -n "$active_session" ]]; then
        cmp -s -- "$scratch/ust-state.before" "$state_file" &&
            timeout 15s lttng list "$active_session" ||
            { case_fail "non-root stop changed metadata or the actual live session"; return 1; }
    else
        cmp -s -- "$scratch/disabled-state.before" "$state_file" ||
            { case_fail "non-root stop changed disabled-session metadata"; return 1; }
    fi
    [[ ! -e "$state_dir/.operation" && ! -L "$state_dir/.operation" ]] ||
        { case_fail "non-root stop created an operation guard"; return 1; }
    CASE_DETAIL="actual UID 65534 stop rejected before state access; session unchanged"
}

# Stop from another working directory and verify actual daemon-side removal,
# not merely a failed list caused by a missing session daemon.
ust_stop_case()
{
    invoke_perfcollect "$scratch/stop" stop ust-integration ||
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
    CASE_DETAIL="stop/destroy removed actual session; archive uses stop process's working directory"
}

# Finalization removes only session metadata/workspace, retaining the private
# directory for reuse and releasing the operation guard.
verify_finalized_state()
{
    local workspace=$1
    [[ ! -e "$workspace" && ! -L "$workspace" ]] ||
        { case_fail "completed session workspace was not removed"; return 1; }
    [[ ! -e "$state_file" && ! -L "$state_file" ]] ||
        { case_fail "completed session metadata was not removed"; return 1; }
    [[ -d "$state_dir" && ! -L "$state_dir" && "$(stat -c '%u %a' -- "$state_dir")" == '0 700' ]] ||
        { case_fail "private root-home state directory was removed or changed"; return 1; }
    [[ ! -e "$state_dir/.operation" && ! -L "$state_dir/.operation" ]] ||
        { case_fail "successful operation leaked its guard"; return 1; }
    stat -c '%n uid=%u mode=%a type=%F' -- "$state_dir"
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
    CASE_DETAIL="archive contains decoded fixture event and log; workspace/state removed; .perfcollect retained"
}

# Establish real disabled-session state, then snapshot its bytes, metadata, log,
# and workspace sentinel for the independent ordinary-collect preservation gate.
disabled_start_case()
{
    # A plain stop, without -nolttng, is intentional: prove the saved flag is
    # restored instead of masking a serializer/loader regression with CLI flags.
    [[ ! -e "$state_file" && ! -L "$state_file" ]] ||
        { case_block "previous case retained state; refusing to overwrite it"; return 77; }
    resolve_real_perf || return 77
    # On disabled distros, omit -nolttng to prove automatic distro detection.
    invoke_perfcollect "$scratch/start" start disabled-integration "${disabled_lttng_options[@]}" -noperf || return 1
    read_state || return 1
    [[ "${state_fields[5]}" == 0 && -z "${state_fields[3]}" && -z "${state_fields[4]}" ]] ||
        { case_fail "LTTng-disabled start did not save disabled flag and empty LTTng fields"; return 1; }
    disabled_workspace=${state_fields[2]}
    disabled_log=${state_fields[1]}
    cp -- "$state_file" "$scratch/disabled-state.before" &&
        cp -- "$disabled_log" "$scratch/disabled-log.before" || return 1
    disabled_metadata_stat=$(stat -c '%u:%g:%a:%i:%s:%Y:%Z' -- "$state_file") || return 1
    printf '%s\n' "$token" > "$disabled_workspace/unrelated-session-sentinel" || return 1
    CASE_DETAIL="actual LTTng-disabled start persisted a valid separate session for plain stop/collect isolation"
}

# A plain stop must restore the persisted disabled flag without repeating CLI
# options. Its log/sentinel archive also proves cleanup was session-owned.
disabled_stop_case()
{
    cmp -s -- "$scratch/disabled-state.before" "$state_file" ||
        { case_fail "unrelated session metadata changed before disabled stop"; return 1; }
    invoke_perfcollect "$scratch/stop" stop disabled-integration ||
        { case_fail "plain stop did not restore/preserve saved useLTTng=0"; return 1; }
    disabled_archive="$scratch/stop/disabled-integration.trace.zip"
    timeout 30s unzip -t "$disabled_archive" &&
        timeout 30s unzip -q "$disabled_archive" -d "$scratch/disabled-extracted" || return 1
    local trace="$scratch/disabled-extracted/disabled-integration.trace"
    [[ -s "$trace/perfcollect.log" && ! -e "$trace/lttngTrace" &&
        ! -e "$trace/perf.data" && ! -e "$trace/perf.data.txt" ]] ||
        { case_fail "disabled start-only archive has unexpected LTTng/perf data or lacks log"; return 1; }
    grep -Fx "$token" "$trace/unrelated-session-sentinel" || return 1
    cat "$trace/perfcollect.log"
    verify_finalized_state "$disabled_workspace" || return 1
    CASE_DETAIL="plain stop finalized saved LTTng-disabled session; private directory retained"
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
    start_cpu IT-PERF-CAP || { case_block "CPU fixture unavailable"; return 77; }
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

# Collect real samples from the fixture while the unrelated disabled session is
# active. Record that collect was attempted even when it subsequently fails.
perf_collect_case()
{
    local rc
    start_cpu IT-PERF-COLLECT || return 1
    perf_collect_attempted=1
    invoke_perfcollect "$scratch/collect" collect cpu-integration "${disabled_lttng_options[@]}" -pid "$cpu_pid" -collectsec 3
    rc=$?
    stop_cpu
    (( rc == 0 )) ||
        { case_fail "real perfcollect collect failed after successful host capability probe"; return 1; }
    [[ -s "$scratch/collect/cpu-integration.trace.zip" ]] ||
        { case_fail "real perf collect produced no nonempty archive"; return 1; }
    CASE_DETAIL="real perf collect ran while a separate valid LTTng-disabled start/stop session existed"
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

# Check preservation even after a failed actual collect. Without an attempted
# collect, unchanged state alone cannot prove isolation and is reported BLOCKED.
perf_state_case()
{
    (( perf_collect_attempted == 1 )) ||
        { case_block "actual collect did not execute; cannot claim preservation across collection"; return 77; }
    cmp -s -- "$scratch/disabled-state.before" "$state_file" &&
        [[ "$(stat -c '%u:%g:%a:%i:%s:%Y:%Z' -- "$state_file")" == "$disabled_metadata_stat" ]] &&
        cmp -s -- "$scratch/disabled-log.before" "$disabled_log" &&
        [[ -d "$disabled_workspace" ]] &&
        grep -Fx "$token" "$disabled_workspace/unrelated-session-sentinel" ||
        { case_fail "ordinary real collect erased/modified separate valid session state/log/workspace"; return 1; }
    [[ ! -e "$state_dir/.operation" && ! -L "$state_dir/.operation" ]] ||
        { case_fail "ordinary collect touched the session operation guard"; return 1; }
    read_state || return 1
    CASE_DETAIL="separate session metadata bytes/inode/ownership/mode/timestamps, log, and workspace sentinel unchanged"
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
    run_case IT-CLEANUP cleanup_case
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
run_case IT-ENV environment_case
if [[ $target == mariner-2.0 ]]; then
    run_case IT-INSTALL install_case IT-ENV
fi
case "$target" in
    ubuntu-20.04|debian-bookworm|ci-debian12)
        run_case IT-UST-CAP ust_capability_case IT-ENV
        run_case IT-UST-START ust_start_case IT-UST-CAP
        run_case IT-UST-EMIT ust_emit_case IT-UST-START
        run_case IT-STOP-ROOT root_stop_case IT-UST-START
        # Still finalize a started session if emission or root rejection failed.
        run_case IT-UST-STOP ust_stop_case IT-UST-START
        run_case IT-UST-ARCHIVE ust_archive_case IT-UST-STOP
        # The disabled lifecycle remains independently runnable even when the
        # host's real UST capability is blocked.
        run_case IT-NOLTTNG-START disabled_start_case IT-ENV
        run_case IT-NOLTTNG-STOP disabled_stop_case IT-NOLTTNG-START
        ;;
    ubuntu-24.04|mariner-2.0|alpine|azurelinux-3.0)
        run_case IT-PERF-CAP perf_capability_case IT-ENV
        # This disabled session is real production state, not fabricated data.
        run_case IT-NOLTTNG-START disabled_start_case IT-ENV
        run_case IT-STOP-ROOT root_stop_case IT-NOLTTNG-START
        run_case IT-PERF-COLLECT perf_collect_case IT-PERF-CAP IT-NOLTTNG-START
        run_case IT-PERF-ARCHIVE perf_archive_case IT-PERF-COLLECT
        # Check preservation even if collection/archiving failed.
        run_case IT-PERF-STATE perf_state_case IT-NOLTTNG-START
        run_case IT-NOLTTNG-STOP disabled_stop_case IT-NOLTTNG-START
        ;;
esac
