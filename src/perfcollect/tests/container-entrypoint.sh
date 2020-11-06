#!/bin/bash

# Set the location of perfcollect.
perfcollect=/src/perfcollect

# Enable debug logging.
set -x

# Test installation.
$perfcollect install

# Capture a short trace.
$perfcollect collect test-trace -collectsec 5

# Open the trace.
unzip test-trace.trace.zip

# Test the size of the files in the trace.
ls test-trace.trace/*
perf_data_size=`wc -c < test-trace.trace/perf.data`
perf_data_txt_size=`wc -c < test-trace.trace/perf.data.txt`
if [ "$perf_data_size" == "0" ]
then
    exit -1
fi
if [ "$perf_data_txt_size" == "0" ]
then
    exit -1
fi
