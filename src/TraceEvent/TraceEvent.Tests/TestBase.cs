using System;
using System.IO;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public abstract class TestBase
    {
        protected static readonly string OriginalBaselineDir = FindInputDir();
        protected static readonly string TestDataDir = @".\inputs";
        protected static readonly string UnZippedDataDir = @".\unzipped";
        protected static readonly string BaseOutputDir = @".\output";

        protected TestBase(ITestOutputHelper output)
        {
            Output = output;
            OutputDir = Path.Combine(BaseOutputDir, Guid.NewGuid().ToString("N").Substring(0, 8));
        }

        protected ITestOutputHelper Output
        {
            get;
        }

        protected string OutputDir
        {
            get;
        }

        /// <summary>
        ///  Tries to find the original place in the source base where input data comes from 
        ///  This may not always work if the tests are copied away from the source code (cloud test does this).  
        /// </summary>
        /// <returns></returns>
        private static string FindInputDir()
        {
            string dir = Environment.CurrentDirectory;
            while (dir != null)
            {
                string candidate = Path.Combine(dir, @"TraceEvent\TraceEvent.Tests\inputs");
                if (Directory.Exists(candidate))
                    return Path.GetFullPath(candidate);
                dir = Path.GetDirectoryName(dir);
            }
            return @"%PERFVIEW%\src\TraceEvent\TraceEvent.Tests\inputs";
        }
    }
}
