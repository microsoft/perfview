####################################################
# Docker image for performance testing of .NET Core.
####################################################
# DisableDockerDetector "Example DockerFile"
FROM ubuntu:22.04

# Install curl and the .NET 7 SDK.
RUN apt-get -y update && apt-get install -y curl dotnet-sdk-7.0

# Create, restore and build a new HelloWorld application.
RUN mkdir hw && cd hw && dotnet new console && \
	echo "using System;\n\nnamespace ConsoleApplication\n{\n\tpublic class Program\n\t{\n\t\tpublic static void Main(string[] args)\n\t\t{\n\t\t\tConsole.WriteLine(\"This application will allocate new objects in a loop forever.\");\n\t\t\twhile(true){ object o = new object(); }\n\t\t}\n\t}\n}" > Program.cs && \
	dotnet build -c Release

# Download and install the latest perfcollect.
RUN mkdir /perf && cd /perf && curl -OL https://aka.ms/perfcollect && chmod +x perfcollect && ./perfcollect install

# Set tracing environment variables.
ENV COMPlus_PerfMapEnabled 1
ENV COMPlus_EnableEventLog 1

# Run the app.
CMD cd /hw && bin/Release/net7.0/hw
