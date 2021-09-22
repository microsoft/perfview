using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace PerfViewTests.GraphSerialization
{
    public abstract class TestBase
    {
        // All of these are normalized to full paths. in PrepareTestData.
        // It also cleans out the output directory
        protected static string OriginalBaselineDir = FindInputDir();
        protected static string TestDataDir = @".\GraphSerialization\inputs";
        protected static string BaseOutputDir = @".\GraphSerialization\output";

        protected TestBase()
        {
            OutputDir = Path.Combine(Path.GetFullPath(BaseOutputDir), Guid.NewGuid().ToString("N").Substring(0, 8));
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
                string candidate = Path.Combine(dir, @"PerfView.Tests\GraphSerialization\inputs");
                if (Directory.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }

                dir = Path.GetDirectoryName(dir);
            }
            return @"%PERFVIEW%\src\PerfView.Tests\GraphSerialization\inputs";
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        protected void PrepareTestData()
        {
            Assert.True(Directory.Exists(TestDataDir));
            TestDataDir = Path.GetFullPath(TestDataDir);
            Assert.True(Directory.Exists(OriginalBaselineDir));
            OriginalBaselineDir = Path.GetFullPath(OriginalBaselineDir);

            if (Directory.Exists(OutputDir))
            {
                Directory.Delete(OutputDir, true);
            }

            Directory.CreateDirectory(OutputDir);
            Assert.True(Path.GetFullPath(OutputDir) == OutputDir);

            Assert.True(Directory.Exists(BaseOutputDir));
            BaseOutputDir = Path.GetFullPath(BaseOutputDir);
        }
    }
}
