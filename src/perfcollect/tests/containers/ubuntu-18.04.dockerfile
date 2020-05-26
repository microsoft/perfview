FROM ubuntu:18.04

# Copy perfcollect sources.
COPY . /src/

# Set working directory.
WORKDIR /src/

# Refresh package cache.
RUN apt-get -y update

# Set tracing environment variables.
ENV COMPlus_PerfMapEnabled 1
ENV COMPlus_EnableEventLog 1

# Run the test harness.
CMD tests/container-entrypoint.sh
