using PerfView.TestUtilities;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    [UseCulture("en-US")]
    public abstract class EventPipeTestBase : TestBase
    {
        protected EventPipeTestBase(ITestOutputHelper output)
            : base(output)
        {
        }

        private static IEnumerable<string> TestEventPipeZipFiles
            => Directory.EnumerateFiles(TestDataDir, "*.netperf.zip")
            .Union(Directory.EnumerateFiles(TestDataDir, "*.nettrace.zip"));

        // The test data is contained in files of the same name, but with a .zip extension.
        // Only the names are returned since the extracted files will be in a different directory.
        public static IEnumerable<object[]> TestEventPipeFiles
            => TestEventPipeZipFiles.Select(file => new[] { Path.GetFileNameWithoutExtension(file) });

        // Only the subset of data files starting in 2.1 were in a format capable of streaming
        public static IEnumerable<object[]> StreamableTestEventPipeFiles
            => TestEventPipeZipFiles.Where(file => file.Contains("dotnetcore2.1"))
                                    .Select(file => new[] { Path.GetFileNameWithoutExtension(file) });

        private static bool s_fileUnzipped;
        private static object s_fileLock = new object();

        protected void PrepareTestData()
        {
            lock (s_fileLock)
            {
                Assert.True(Directory.Exists(TestDataDir));
                TestDataDir = Path.GetFullPath(TestDataDir);
                Assert.True(Directory.Exists(OriginalBaselineDir));
                OriginalBaselineDir = Path.GetFullPath(OriginalBaselineDir);

                // This is atomic because this method is synchronized.  
                if (!s_fileUnzipped)
                {
                    Directory.CreateDirectory(UnZippedDataDir);

                    foreach (var zipFile in TestEventPipeZipFiles)
                    {
                        string eventPipeFilePath = Path.Combine(UnZippedDataDir, Path.GetFileNameWithoutExtension(zipFile));

                        if (!File.Exists(eventPipeFilePath) || File.GetLastWriteTimeUtc(eventPipeFilePath) < File.GetLastWriteTimeUtc(zipFile))
                        {
                            File.Delete(eventPipeFilePath);
                            ZipFile.ExtractToDirectory(zipFile, UnZippedDataDir);
                        }

                        Assert.True(File.Exists(eventPipeFilePath));
                    }

                    s_fileUnzipped = true;
                }

                if (Directory.Exists(OutputDir))
                {
                    Directory.Delete(OutputDir, true);
                }

                Directory.CreateDirectory(OutputDir);
                Output.WriteLine(string.Format("OutputDir: {0}", Path.GetFullPath(OutputDir)));
                Assert.True(Directory.Exists(OutputDir));

                Directory.CreateDirectory(NewBaselineDir);
                NewBaselineDir = Path.GetFullPath(NewBaselineDir);
                Output.WriteLine(string.Format("NewBaselineDir: {0}", NewBaselineDir));

                Assert.True(Directory.Exists(UnZippedDataDir));
                UnZippedDataDir = Path.GetFullPath(UnZippedDataDir);
                Assert.True(Directory.Exists(BaseOutputDir));
                BaseOutputDir = Path.GetFullPath(BaseOutputDir);
                Assert.True(Directory.Exists(NewBaselineDir));
            }
        }
    }
}
