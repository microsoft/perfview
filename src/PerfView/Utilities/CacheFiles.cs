using System.IO;
using System;
using Microsoft.Diagnostics.Utilities;
using System.Reflection;
using System.Diagnostics;

namespace Utilities
{
    /// <summary>
    /// Some applications need to make files that are associated with the application
    /// but also have affinity with other files on the disk.   This class helps manage this
    /// </summary>
    static class CacheFiles
    {
        public static float KeepTimeInDays { get; set; } 
        public static string CacheDir
        {
            get
            {
                if (s_CacheDir == null)
                {
                    var exeAssembly = Assembly.GetExecutingAssembly();
                    var exePath = exeAssembly.ManifestModule.FullyQualifiedName;
                    var exeName = Path.GetFileNameWithoutExtension(exePath);
                    var tempDir = Environment.GetEnvironmentVariable("TEMP");
                    if (tempDir == null)
                        tempDir = ".";
                    s_CacheDir = Path.Combine(tempDir, exeName);
                    Directory.CreateDirectory(s_CacheDir);
                }
                return s_CacheDir;
            }
            set { s_CacheDir = value; }
        }

        /// <summary>
        /// Find a path name for the file 'baseFilePath' (which can be a path name to anywhere).
        /// which has the extension 'extension'.  It will always return something in 'CacheDir'
        /// and thus might go away.
        /// </summary>
        /// <remarks>
        /// Note that the file 'baseFilePath' is assumed to exist.
        /// </remarks>
        public static string FindFile(string baseFilePath, string extension = "")
        {
            // TODO FIX NOW add collision detection

            // We expect the original file to exist
            Debug.Assert(File.Exists(baseFilePath));

            var baseFileName = Path.GetFileName(baseFilePath);
            var baseFileInfo = new FileInfo(baseFilePath);

            // The hash is a combination of full path, size and last write timestamp
            var hashData = Tuple.Create(Path.GetFullPath(baseFilePath), baseFileInfo.Length, baseFileInfo.LastWriteTimeUtc);
            int hash = hashData.GetHashCode();

            string ret = Path.Combine(CacheDir, baseFileName + "_" + hash.ToString("x") + extension);
            if (File.Exists(ret))
            {
                // See if it is up to date. 
                if (File.GetLastWriteTimeUtc(ret) < baseFileInfo.LastWriteTimeUtc)
                    FileUtilities.ForceDelete(ret);
                else
                {
                    // Set the last access time so we can clean up files based on their last usage.
                    // However if someone else is actual using the file, we don't want an 'in use' 
                    // exception, so treat this part as optional.  
                    try { File.SetLastAccessTimeUtc(ret, DateTime.UtcNow); }
                    catch (Exception) { }
                }
            }
            if (!s_didCleanup)
            {
                s_didCleanup = true;
                Cleanup();
            }
            return ret;
        }
        static public void Cleanup()
        {
            CleanupDirectory(CacheDir, KeepTimeInDays);
        }

        static public void CleanupDirectory(string directory, float keepTimeInDays)
        {
            if (keepTimeInDays == 0)
                keepTimeInDays = 5;

            var nowUTC = DateTime.UtcNow;
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if ((nowUTC - File.GetLastAccessTimeUtc(file)).TotalDays > keepTimeInDays)
                {
                    try
                    {
                        FileUtilities.ForceDelete(file);
                    }
                    catch (Exception) { }
                }
            }
        }

        /// <summary>
        /// Returns true if the output file is up to date with respect to the input file (exists and created after it). 
        /// </summary>
        public static bool UpToDate(string outputFile, string inputFile)
        {
            return File.Exists(outputFile) && File.GetLastWriteTimeUtc(inputFile) <= File.GetLastWriteTimeUtc(outputFile);
        }

        #region private 
        static string s_CacheDir;
        static bool s_didCleanup;
        #endregion 
    }
}
