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
            // TraceParserGen.exe is copied to the output directory during build
            string exePath = Path.Combine(Environment.CurrentDirectory, "TraceParserGen.exe");
            
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException($"Could not find TraceParserGen.exe at {exePath}. Please build the TraceParserGen.Tests project.");
            }
            
            return exePath;
        }

    }
}
