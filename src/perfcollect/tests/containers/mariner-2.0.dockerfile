FROM mcr.microsoft.com/cbl-mariner/base/core:2.0

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
