#!/bin/bash

# Enable debug output to enable diagnostics in the CI.
set -x

# Run containers.
docker run --privileged --security-opt seccomp=unconfined perfcollect-mariner-2.0
