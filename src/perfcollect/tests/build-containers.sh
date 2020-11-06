#!/bin/bash

# Enable debug output to enable diagnostics in the CI.
set -x

# Build containers.
#docker build ../ -f containers/ubuntu-16.04.dockerfile -t perfcollect-ubuntu-16.04
docker build ../ -f containers/ubuntu-18.04.dockerfile -t perfcollect-ubuntu-18.04
