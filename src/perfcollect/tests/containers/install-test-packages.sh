#!/bin/sh
# Build-time dependency installer for the disposable matrix test images.
# Read the base image's real os-release and use its native package manager.
# Shared utilities exercise actual session metadata/archives. Compilers build
# real CPU/event producers; Debian/Ubuntu also receive UST libraries and a reader.
#
# This runs inside the image build, never on the execution host. Packages come
# from distro feeds. POSIX sh keeps the initial bootstrap usable before Bash is
# installed.
set -eu

# This is trusted distro configuration, not perfcollect's session data.
. /etc/os-release

case "$ID" in
    ubuntu|debian)
        # Avoid interactive package prompts and select the reader that each
        # supported release actually packages.
        export DEBIAN_FRONTEND=noninteractive
        trace_reader_package=babeltrace2
        if [ "$ID:$VERSION_ID" = ubuntu:20.04 ]; then
            trace_reader_package=babeltrace
        elif [ "$ID:$VERSION_ID" = debian:11 ]; then
            # Bullseye's retired feeds are available from Debian's signed archive.
            # Disable archive expiry checks only; package signatures stay required.
            printf '%s\n' \
                'deb [check-valid-until=no] http://archive.debian.org/debian bullseye main' \
                'deb [check-valid-until=no] http://archive.debian.org/debian-security bullseye-security main' \
                > /etc/apt/sources.list
        fi

        # Real GNU filesystem/account tools support session/UID checks. UST
        # development packages compile the fixture against this image's library.
        apt-get -o Acquire::http::No-Cache=true -o Acquire::Retries=3 update
        apt-get install -y --no-install-recommends bash coreutils diffutils findutils grep sed gawk \
            procps util-linux passwd sudo zip unzip gcc libc6-dev pkg-config \
            liblttng-ust-dev lttng-tools "$trace_reader_package"

        # Container kernels belong to the host, not the distro. Install distro
        # perf packages and let actual record/export probes decide compatibility.
        if [ "$ID" = ubuntu ]; then
            apt-get install -y --no-install-recommends linux-tools-generic linux-tools-common
        else
            apt-get install -y --no-install-recommends linux-perf
        fi
        # Discard only image-local package indexes, leaving apt's directory usable.
        rm -rf -- /var/lib/apt/lists
        mkdir -p /var/lib/apt/lists
        ;;
    alpine)
        # Keep BusyBox stat/mktemp/readlink: installing coreutils hides portability bugs.
        # Production disables LTTng on Alpine, so no UST integration packages are needed.
        apk add --no-cache bash shadow sudo zip unzip diffutils findutils \
            grep sed gawk procps util-linux perf gcc musl-dev
        ;;
    mariner|azurelinux)
        # kernel-tools supplies perf; gcc builds the actual CPU fixture.
        # LTTng is disabled by production on both Microsoft Linux families.
        tdnf install -y bash coreutils diffutils findutils grep sed gawk procps-ng util-linux \
            shadow-utils sudo zip unzip kernel-tools kernel-headers gcc binutils glibc-devel
        tdnf clean all
        ;;
    rocky|fedora)
        # Replace minimal coreutils-single where necessary instead of retaining
        # conflicting utility packages. UST packages are not installed here.
        dnf install -y --allowerasing bash coreutils diffutils findutils grep sed gawk procps-ng util-linux \
            shadow-utils sudo zip unzip perf
        dnf clean all
        ;;
    opensuse-leap|opensuse-tumbleweed)
        # Keep the same real utility contract using openSUSE's native repositories.
        zypper --non-interactive refresh
        zypper --non-interactive install bash coreutils diffutils findutils grep sed gawk \
            procps util-linux shadow sudo zip unzip perf
        zypper clean --all
        ;;
    # An unsupported base must fail its build, not produce an incomplete test image.
    *) printf 'Unsupported test distribution: %s\n' "$ID" >&2; exit 1 ;;
esac
