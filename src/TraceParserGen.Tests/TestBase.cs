using System;
using System.IO;
using Xunit.Abstractions;

namespace TraceParserGen.Tests
{
    /// <summary>
    /// Base class for TraceParserGen tests
    /// </summary>
    public abstract class TestBase
    {
        protected static string OriginalInputDir = FindInputDir();
        protected static string TestDataDir = Path.GetFullPath("inputs");
        protected static string BaseOutputDir = Path.GetFullPath("output");

        protected TestBase(ITestOutputHelper output)
        {
            Output = output;
            OutputDir = Path.Combine(BaseOutputDir, Guid.NewGuid().ToString("N").Substring(0, 8));
            
            // Ensure output directory exists
            if (!Directory.Exists(OutputDir))
            {
                Directory.CreateDirectory(OutputDir);
            }
        }

        protected ITestOutputHelper Output { get; }

        protected string OutputDir { get; }

        /// <summary>
        /// Finds the input directory for test files
        /// </summary>
        private static string FindInputDir()
        {
            string dir = Environment.CurrentDirectory;
            while (dir != null)
            {
                string candidate = Path.Combine(dir, @"TraceParserGen.Tests\inputs");
                if (Directory.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }

                dir = Path.GetDirectoryName(dir);
            }
            return @"%PERFVIEW%\src\TraceParserGen.Tests\inputs";
        }

        /// <summary>
        /// Gets the path to the TraceParserGen.exe executable
        /// </summary>
        protected string GetTraceParserGenExePath()
        {
            // Search for TraceParserGen.exe in common build output locations
            string[] searchPaths = new[]
            {
                // Relative to test output directory
                Path.Combine(Environment.CurrentDirectory, @"../../../../TraceParserGen/bin/Release/net462/TraceParserGen.exe"),
                Path.Combine(Environment.CurrentDirectory, @"../../../../TraceParserGen/bin/Debug/net462/TraceParserGen.exe"),
                
                // Also try from bin directory (for net462 tests)
                Path.Combine(Environment.CurrentDirectory, @"../../../TraceParserGen/bin/Release/net462/TraceParserGen.exe"),
                Path.Combine(Environment.CurrentDirectory, @"../../../TraceParserGen/bin/Debug/net462/TraceParserGen.exe"),
            };

            foreach (var path in searchPaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            throw new FileNotFoundException("Could not find TraceParserGen.exe. Please build the TraceParserGen project first.");
        }

        /// <summary>
        /// Gets the path to the TraceEvent assembly
        /// </summary>
        protected string GetTraceEventAssemblyPath()
        {
            // Search for TraceEvent DLL in common build output locations
            string[] searchPaths = new[]
            {
                // Relative to test output directory  
                Path.Combine(Environment.CurrentDirectory, @"../../../../TraceEvent/bin/Release/netstandard2.0/Microsoft.Diagnostics.Tracing.TraceEvent.dll"),
                Path.Combine(Environment.CurrentDirectory, @"../../../../TraceEvent/bin/Debug/netstandard2.0/Microsoft.Diagnostics.Tracing.TraceEvent.dll"),
                
                // Also try from bin directory (for net462 tests)
                Path.Combine(Environment.CurrentDirectory, @"../../../TraceEvent/bin/Release/netstandard2.0/Microsoft.Diagnostics.Tracing.TraceEvent.dll"),
                Path.Combine(Environment.CurrentDirectory, @"../../../TraceEvent/bin/Debug/netstandard2.0/Microsoft.Diagnostics.Tracing.TraceEvent.dll"),
            };

            foreach (var path in searchPaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            throw new FileNotFoundException("Could not find Microsoft.Diagnostics.Tracing.TraceEvent.dll. Please build the TraceEvent project first.");
        }
    }
}
