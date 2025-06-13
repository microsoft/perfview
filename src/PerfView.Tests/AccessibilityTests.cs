using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfView;

namespace PerfView.Tests
{
    [TestClass]
    public class AccessibilityTests
    {
        [TestMethod]
        public void TreeView_FileList_ReportsCorrectItemCount()
        {
            // Create a temporary directory with known file count
            var tempDir = CreateTempDirectoryWithFiles(6);
            
            try
            {
                // Create PerfViewDirectory
                var perfViewDir = new PerfViewDirectory(tempDir);
                
                // Get children without filter
                var children = perfViewDir.Children;
                
                // Should have 6 files + 1 parent directory (..) = 7 total
                Assert.AreEqual(7, children.Count, "Expected 6 files + 1 parent directory");
                
                // Verify that the last item is the parent directory
                Assert.AreEqual("..", children.Last().Name, "Last item should be parent directory");
                
                // Count only non-parent directory items (actual files/subdirs)
                var actualFileCount = children.Count(c => c.Name != "..");
                Assert.AreEqual(6, actualFileCount, "Should have exactly 6 files/directories excluding parent");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TreeView_FileList_ExcludesSystemEntries()
        {
            // Create a temporary directory with known file count plus system files
            var tempDir = CreateTempDirectoryWithSystemFiles();
            
            try
            {
                // Create PerfViewDirectory
                var perfViewDir = new PerfViewDirectory(tempDir);
                
                // Get children without filter
                var children = perfViewDir.Children;
                
                // Should not include hidden system files
                var visibleChildren = children.Where(c => c.Name != ".." && 
                    !c.Name.StartsWith(".") && 
                    !c.Name.StartsWith("~") &&
                    !c.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) &&
                    !c.Name.Equals("thumbs.db", StringComparison.OrdinalIgnoreCase)).ToList();
                
                // Should have only the actual files, not system files
                Assert.AreEqual(3, visibleChildren.Count, "Should have exactly 3 non-system files");
                
                // Verify that the parent directory is still included (for navigation)
                Assert.IsTrue(children.Any(c => c.Name == ".."), "Should still include parent directory for navigation");
                
                // Verify that system files are filtered out
                Assert.IsFalse(children.Any(c => c.Name == ".hidden"), "Should exclude hidden files");
                Assert.IsFalse(children.Any(c => c.Name == "desktop.ini"), "Should exclude desktop.ini");
                Assert.IsFalse(children.Any(c => c.Name == "thumbs.db"), "Should exclude thumbs.db");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    // Remove hidden attribute before deleting
                    foreach (var file in Directory.GetFiles(tempDir))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private string CreateTempDirectoryWithSystemFiles()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            
            // Create normal files that should be visible
            File.WriteAllText(Path.Combine(tempDir, "trace1.etl"), "etl content");
            File.WriteAllText(Path.Combine(tempDir, "trace2.etl"), "etl content");
            File.WriteAllText(Path.Combine(tempDir, "data.txt"), "text content");
            
            // Create system/hidden files that should be filtered out
            var hiddenFile = Path.Combine(tempDir, ".hidden");
            File.WriteAllText(hiddenFile, "hidden content");
            File.SetAttributes(hiddenFile, FileAttributes.Hidden);
            
            File.WriteAllText(Path.Combine(tempDir, "desktop.ini"), "desktop ini content");
            File.WriteAllText(Path.Combine(tempDir, "thumbs.db"), "thumbs db content");
            File.WriteAllText(Path.Combine(tempDir, "~tempfile"), "temp file content");
            
            return tempDir;
        }

        private string CreateTempDirectoryWithFiles(int fileCount)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            
            for (int i = 1; i <= fileCount; i++)
            {
                File.WriteAllText(Path.Combine(tempDir, $"file{i}.etl"), "test content");
            }
            
            return tempDir;
        }

        private string CreateTempDirectoryWithMixedFiles()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            
            // Create different file types
            File.WriteAllText(Path.Combine(tempDir, "trace1.etl"), "etl content");
            File.WriteAllText(Path.Combine(tempDir, "trace2.etl"), "etl content");
            File.WriteAllText(Path.Combine(tempDir, "data.txt"), "text content");
            File.WriteAllText(Path.Combine(tempDir, "config.xml"), "xml content");
            File.WriteAllText(Path.Combine(tempDir, "readme.md"), "markdown content");
            
            return tempDir;
        }
    }
}