ARG BASE_IMAGE
FROM ${BASE_IMAGE}
COPY . /src/
RUN /bin/sh /src/tests/containers/install-test-packages.sh
WORKDIR /src/
ENV DOTNET_PerfMapEnabled=1 DOTNET_EnableEventLog=1 DOTNET_EnableWriteXorExecute=1
CMD ["/bin/bash", "/src/tests/session-integration-tests.sh"]
