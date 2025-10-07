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
        protected static string TestDataDir = Path.Combine(Environment.CurrentDirectory, "inputs");

        protected TestBase(ITestOutputHelper output)
        {
            Output = output;
            OutputDir = Path.Combine(Path.GetTempPath(), "TraceParserGen.Tests", Guid.NewGuid().ToString("N").Substring(0, 8));
            
            Directory.CreateDirectory(OutputDir);
        }

        protected ITestOutputHelper Output { get; }

        protected string OutputDir { get; }

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
