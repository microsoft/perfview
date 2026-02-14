using System;
using System.Diagnostics;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace TraceParserGen.Tests
{
    /// <summary>
    /// Base class for TraceParserGen tests
    /// </summary>
    public abstract class TestBase : IDisposable
    {
        protected static string TestDataDir = Path.Combine(Environment.CurrentDirectory, "inputs");

        protected TestBase(ITestOutputHelper output)
        {
            Output = output;
            OutputDir = Path.Combine(Path.GetTempPath(), "TraceParserGen.Tests", Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(OutputDir);
        }

        protected ITestOutputHelper Output { get; }

        protected string OutputDir { get; }

        protected string GetTraceParserGenExePath()
        {
            string exePath = Path.Combine(Environment.CurrentDirectory, "TraceParserGen.exe");
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException($"Could not find TraceParserGen.exe at {exePath}. Please build the TraceParserGen.Tests project.");
            }

            return exePath;
        }

        /// <summary>
        /// Runs TraceParserGen.exe with the given arguments and returns the exit code, stdout, and stderr.
        /// </summary>
        protected (int exitCode, string stdout, string stderr) RunTraceParserGen(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = GetTraceParserGenExePath(),
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Output.WriteLine($"Running: {startInfo.FileName} {startInfo.Arguments}");

            using (var process = Process.Start(startInfo))
            {
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    Output.WriteLine($"STDOUT: {stdout}");
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    Output.WriteLine($"STDERR: {stderr}");
                }

                return (process.ExitCode, stdout, stderr);
            }
        }

        /// <summary>
        /// Runs TraceParserGen on a manifest and returns the generated C# content.
        /// </summary>
        protected string GenerateParserFromManifest(string manifestFileName, string outputFileName = null, string extraArgs = "")
        {
            string manifestPath = Path.Combine(TestDataDir, manifestFileName);
            if (outputFileName == null)
            {
                outputFileName = Path.ChangeExtension(manifestFileName, ".cs");
            }

            string outputPath = Path.Combine(OutputDir, outputFileName);

            string args = $"{extraArgs} \"{manifestPath}\" \"{outputPath}\"".Trim();
            var (exitCode, stdout, stderr) = RunTraceParserGen(args);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath), $"Generated C# file not found: {outputPath}");

            return File.ReadAllText(outputPath);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(OutputDir))
                {
                    Directory.Delete(OutputDir, true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
