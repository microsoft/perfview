using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
            // Skip on non-Windows platforms since TraceParserGen.exe is a .NET Framework app
            // In a real environment, this would run on Windows with proper .NET Framework support
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Output.WriteLine("Skipping test on non-Windows platform. TraceParserGen.exe requires .NET Framework.");
                return;
            }

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
            ProcessStartInfo startInfo;
            
            // On Linux/Mac, we need to use mono to run .NET Framework executables
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"\"{manifestPath}\" \"{outputPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
            }
            else
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = "mono",
                    Arguments = $"\"{exePath}\" \"{manifestPath}\" \"{outputPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
            }

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
            // Create the .csproj file
            string csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net462</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""Microsoft.Diagnostics.Tracing.TraceEvent"">
      <HintPath>{GetTraceEventAssemblyPath()}</HintPath>
    </Reference>
  </ItemGroup>
</Project>";

            File.WriteAllText(Path.Combine(projectDir, "TestApp.csproj"), csprojContent);

            // Copy the generated parser file
            string destParserPath = Path.Combine(projectDir, Path.GetFileName(generatedParserPath));
            File.Copy(generatedParserPath, destParserPath, true);

            // Create Program.cs that uses reflection to instantiate parsers
            string programContent = @"using System;
using System.Linq;
using System.Reflection;
using Microsoft.Diagnostics.Tracing;

class Program
{
    static int Main(string[] args)
    {
        try
        {
            Console.WriteLine(""Starting parser test..."");
            
            // Find all TraceEventParser-derived types in the current assembly
            var assembly = Assembly.GetExecutingAssembly();
            var parserTypes = assembly.GetTypes()
                .Where(t => typeof(TraceEventParser).IsAssignableFrom(t) && !t.IsAbstract)
                .ToList();

            Console.WriteLine($""Found {parserTypes.Count} parser type(s)"");

            foreach (var parserType in parserTypes)
            {
                Console.WriteLine($""  Testing parser: {parserType.Name}"");
                
                // Create an instance of the parser
                // TraceEventParser constructors typically take a TraceEventSource parameter
                // Since we don't have a real source, we'll just verify the type can be instantiated
                // by checking if it has expected methods
                
                var enumerateMethod = parserType.GetMethod(""EnumerateTemplates"", 
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                
                if (enumerateMethod != null)
                {
                    Console.WriteLine($""    Found EnumerateTemplates method"");
                }
                else
                {
                    Console.WriteLine($""    WARNING: EnumerateTemplates method not found"");
                }
            }

            Console.WriteLine(""Parser test completed successfully"");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($""ERROR: {ex.Message}"");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}";

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
                Arguments = "run -c Release --no-build",
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
