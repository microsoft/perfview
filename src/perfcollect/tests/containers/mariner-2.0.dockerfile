FROM mcr.microsoft.com/cbl-mariner/base/core:2.0@sha256:82314abb594a695fd8817774e8b7f101934902cc1d99b3075e80acbc8b9b23ee

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
