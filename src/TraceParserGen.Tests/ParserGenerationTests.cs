using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace TraceParserGen.Tests
{
    /// <summary>
    /// Tests for TraceParserGen.exe that validate it can generate parsers from manifests
    /// </summary>
    public class ParserGenerationTests : TestBase
    {
        public ParserGenerationTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void CanGenerateParserFromManifest()
        {
            // Arrange
            string manifestPath = Path.Combine(TestDataDir, "SimpleTest.manifest.xml");
            string outputCsPath = Path.Combine(OutputDir, "SimpleTestParser.cs");
            
            Output.WriteLine($"Manifest: {manifestPath}");
            Output.WriteLine($"Output: {outputCsPath}");

            Assert.True(File.Exists(manifestPath), $"Manifest file not found: {manifestPath}");

            // Act - Step 1: Run TraceParserGen.exe
            string traceParserGenPath = GetTraceParserGenExePath();
            Output.WriteLine($"TraceParserGen.exe: {traceParserGenPath}");

            var exitCode = RunTraceParserGen(traceParserGenPath, manifestPath, outputCsPath);

            // Assert - Step 1: Verify TraceParserGen succeeded
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputCsPath), $"Generated C# file not found: {outputCsPath}");

            // Verify the generated file has expected content
            string generatedContent = File.ReadAllText(outputCsPath);
            Assert.Contains("class", generatedContent);
            Assert.Contains("TraceEventParser", generatedContent);

            Output.WriteLine("Successfully generated parser from manifest");

            // Act - Step 2: Create and build a test console application
            string testProjectDir = Path.Combine(OutputDir, "TestApp");
            Directory.CreateDirectory(testProjectDir);

            CreateTestConsoleApp(testProjectDir, outputCsPath);

            // Act - Step 3: Build the test application
            var buildExitCode = BuildTestApp(testProjectDir);
            Assert.Equal(0, buildExitCode);

            // Act - Step 4: Run the test application
            var runExitCode = RunTestApp(testProjectDir);

            // Assert - Step 4: Verify test app ran successfully (no crashes, no asserts)
            Assert.Equal(0, runExitCode);

            Output.WriteLine("Test completed successfully");
        }

        private int RunTraceParserGen(string exePath, string manifestPath, string outputPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"\"{manifestPath}\" \"{outputPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Output.WriteLine($"Running: {startInfo.FileName} {startInfo.Arguments}");

            using (var process = Process.Start(startInfo))
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(output))
                {
                    Output.WriteLine("STDOUT:");
                    Output.WriteLine(output);
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Output.WriteLine("STDERR:");
                    Output.WriteLine(error);
                }

                return process.ExitCode;
            }
        }

        private void CreateTestConsoleApp(string projectDir, string generatedParserPath)
        {
            // Get the path to TraceEvent assembly - it's in the test project's output directory
            // since we have a ProjectReference
            string traceEventAssembly = Path.Combine(Environment.CurrentDirectory, "Microsoft.Diagnostics.Tracing.TraceEvent.dll");
            
            // Create the .csproj file
            string csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net462</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""Microsoft.Diagnostics.Tracing.TraceEvent"">
      <HintPath>{traceEventAssembly}</HintPath>
    </Reference>
  </ItemGroup>
</Project>";

            File.WriteAllText(Path.Combine(projectDir, "TestApp.csproj"), csprojContent);

            // Copy the generated parser file
            string destParserPath = Path.Combine(projectDir, Path.GetFileName(generatedParserPath));
            File.Copy(generatedParserPath, destParserPath, true);

            // Create a simple trace file to use for testing
            // We'll use one of the existing test trace files
            string sampleTracePath = Path.Combine(TestDataDir, "..", "..", "TraceEvent", "TraceEvent.Tests", "inputs", "net.4.5.x86.etl.zip");
            
            // Create Program.cs that uses the generated parser with a real trace file
            string programContent = $@"using System;
using System.Linq;
using System.Reflection;
using Microsoft.Diagnostics.Tracing;

class Program
{{
    static int Main(string[] args)
    {{
        try
        {{
            Console.WriteLine(""Starting parser test..."");
            
            // Find all TraceEventParser-derived types in the current assembly
            var assembly = Assembly.GetExecutingAssembly();
            var parserTypes = assembly.GetTypes()
                .Where(t => typeof(TraceEventParser).IsAssignableFrom(t) && !t.IsAbstract)
                .ToList();

            Console.WriteLine($""Found {{parserTypes.Count}} parser type(s)"");

            if (parserTypes.Count == 0)
            {{
                Console.WriteLine(""ERROR: No parser types found"");
                return 1;
            }}

            // Get trace file path (from args or use default)
            string traceFilePath = args.Length > 0 ? args[0] : ""{sampleTracePath.Replace("\\", "\\\\")}"";
            
            if (!System.IO.File.Exists(traceFilePath))
            {{
                Console.WriteLine($""ERROR: Trace file not found: {{traceFilePath}}"");
                return 1;
            }}
            
            Console.WriteLine($""Using trace file: {{traceFilePath}}"");
            
            using (var source = TraceEventDispatcher.GetDispatcherFromFileName(traceFilePath))
            {{
                foreach (var parserType in parserTypes)
                {{
                    Console.WriteLine($""  Testing parser: {{parserType.Name}}"");
                    
                    // Create an instance of the parser
                    var parser = (TraceEventParser)Activator.CreateInstance(parserType, source);
                    
                    int eventCount = 0;
                    
                    // Hook the All event to count events processed by this parser
                    parser.All += (TraceEvent data) =>
                    {{
                        eventCount++;
                    }};
                    
                    // Process the trace (this will trigger events if any match)
                    source.Process();
                    
                    Console.WriteLine($""    Processed {{eventCount}} event(s) from this parser"");
                }}
            }}

            Console.WriteLine(""Parser test completed successfully"");
            return 0;
        }}
        catch (Exception ex)
        {{
            Console.WriteLine($""ERROR: {{ex.Message}}"");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }}
    }}
}}";

            File.WriteAllText(Path.Combine(projectDir, "Program.cs"), programContent);
        }

        private int BuildTestApp(string projectDir)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build -c Release",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Output.WriteLine($"Building test app in: {projectDir}");

            using (var process = Process.Start(startInfo))
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(output))
                {
                    Output.WriteLine("Build STDOUT:");
                    Output.WriteLine(output);
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Output.WriteLine("Build STDERR:");
                    Output.WriteLine(error);
                }

                return process.ExitCode;
            }
        }

        private int RunTestApp(string projectDir)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run -c Release",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Output.WriteLine($"Running test app in: {projectDir}");

            using (var process = Process.Start(startInfo))
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(output))
                {
                    Output.WriteLine("Run STDOUT:");
                    Output.WriteLine(output);
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Output.WriteLine("Run STDERR:");
                    Output.WriteLine(error);
                }

                return process.ExitCode;
            }
        }
    }
}
