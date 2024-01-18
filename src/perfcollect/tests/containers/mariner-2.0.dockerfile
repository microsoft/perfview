FROM mcr.microsoft.com/cbl-mariner/base/core:2.0@sha256:60323975ec3aabe1840920a65237950a54c5fef6ffc811a5d26bb6bd130f1cc3

# Copy perfcollect sources.
COPY . /src/

# Set working directory.
WORKDIR /src/

# Set tracing environment variables.
ENV DOTNET_PerfMapEnabled 1
ENV DOTNET_EnableEventLog 1
ENV DOTNET_EnableWriteXorExecute 1

# Run the test harness.
CMD tests/container-entrypoint.sh
