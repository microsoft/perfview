#!/bin/bash

# Enable debug output to enable diagnostics in the CI.
set -x

# Build containers.
docker build ../ -f containers/mariner-2.0.dockerfile -t perfcollect-mariner-2.0
