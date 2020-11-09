####################################################
# Docker image for performance testing of .NET Core.
####################################################
FROM ubuntu:14.04

# Install curl so that we can download dependencies.
RUN apt-get -y update && apt-get install -y curl

# Install CLI dependencies.
RUN apt-get -y install libunwind8 gettext libicu52 libuuid1 libcurl3 libssl1.0.0 zlib1g liblttng-ust0

# Download and decompress the latest CLI.
RUN mkdir dotnet_cli && cd dotnet_cli && curl -O https://dotnetcli.blob.core.windows.net/dotnet/preview/Binaries/Latest/dotnet-dev-ubuntu-x64.latest.tar.gz && tar -xzvf dotnet-dev-ubuntu-x64.latest.tar.gz

# Create, restore and build a new HelloWorld application.
RUN mkdir hw && cd hw && /dotnet_cli/dotnet new && \
	echo "using System;\n\nnamespace ConsoleApplication\n{\n\tpublic class Program\n\t{\n\t\tpublic static void Main(string[] args)\n\t\t{\n\t\t\tConsole.WriteLine(\"This application will allocate new objects in a loop forever.\");\n\t\t\twhile(true){ object o = new object(); }\n\t\t}\n\t}\n}" > Program.cs && \
	/dotnet_cli/dotnet restore && /dotnet_cli/dotnet build -c Release

# Download the latest perfcollect.
RUN mkdir /perf && cd /perf && curl -OL https://aka.ms/perfcollect && chmod +x perfcollect

# Install perf and LTTng dependencies.
RUN apt-get -y install linux-tools-common linux-tools-`uname -r` linux-cloud-tools-`uname -r` lttng-tools lttng-modules-dkms liblttng-ust0 zip

# Set tracing environment variables.
ENV COMPlus_PerfMapEnabled 1
ENV COMPlus_EnableEventLog 1

# Run the app.
CMD cd /hw && /dotnet_cli/dotnet run -c Release
