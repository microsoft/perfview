using System;
using System.IO;
using Microsoft.Diagnostics.Symbols;
using Xunit;
using Xunit.Abstractions;

using static Microsoft.Diagnostics.Symbols.NativeSymbolModule.MicrosoftPdbSourceFile;

namespace TraceEventTests
{
    /// <summary>
    /// Tests for <see cref="NativeSymbolModule.MicrosoftPdbSourceFile.TryGetSafeSourceCachePath"/>, the
    /// helper that synthesizes a structurally-safe source-cache path under a given cache directory.  This
    /// is the path-traversal containment: PDB-supplied SRCSRVTRG strings (which can contain '..\..\',
    /// mixed separators, alternate roots, etc.) must not be able to cause writes outside the cache
    /// directory.
    /// </summary>
    public class NativeSymbolModuleTests : TestBase
    {
        public NativeSymbolModuleTests(ITestOutputHelper output) : base(output)
        {
        }

        private const string CacheDir = @"C:\cache";

        [Fact]
        public void TryGetSafeSourceCachePath_NullCacheDir_ReturnsFalse()
        {
            Assert.False(TryGetSafeSourceCachePath(null, @"C:\src\Program.cs", out string safe));
            Assert.Null(safe);
        }

        [Fact]
        public void TryGetSafeSourceCachePath_EmptyCacheDir_ReturnsFalse()
        {
            Assert.False(TryGetSafeSourceCachePath(string.Empty, @"C:\src\Program.cs", out string safe));
            Assert.Null(safe);
        }

        [Fact]
        public void TryGetSafeSourceCachePath_NullTarget_ReturnsFalse()
        {
            Assert.False(TryGetSafeSourceCachePath(CacheDir, null, out string safe));
            Assert.Null(safe);
        }

        [Fact]
        public void TryGetSafeSourceCachePath_EmptyTarget_ReturnsFalse()
        {
            Assert.False(TryGetSafeSourceCachePath(CacheDir, string.Empty, out string safe));
            Assert.Null(safe);
        }

        [Fact]
        public void TryGetSafeSourceCachePath_TargetWithoutFileName_ReturnsFalse()
        {
            // Path.GetFileName(@"C:\foo\") returns string.Empty.
            Assert.False(TryGetSafeSourceCachePath(CacheDir, @"C:\foo\", out string safe));
            Assert.Null(safe);
        }

        [Fact]
        public void TryGetSafeSourceCachePath_FileNameWithInvalidChars_ReturnsFalse()
        {
            // Pipe is in Path.GetInvalidFileNameChars() on Windows.  Use a backslash-separated path so
            // Path.GetFileName returns the offending segment unchanged on net462 / netcoreapp.
            Assert.False(TryGetSafeSourceCachePath(CacheDir, @"C:\src\bad|name.cs", out string safe));
            Assert.Null(safe);
        }

        [Theory]
        [InlineData(@"C:\path\..")]
        [InlineData(@"C:\path\.")]
        [InlineData("https://example.com/path/..")]
        [InlineData("https://example.com/path/.")]
        [InlineData(@"..")]
        [InlineData(@".")]
        [InlineData(@".. ")]     // trailing space; Windows kernel normalizes to ".."
        [InlineData(@". ")]      // trailing space; Windows kernel normalizes to "."
        [InlineData(@"...")]     // trailing dot run; Windows kernel normalizes to ".."
        [InlineData(@"..  ")]    // multiple trailing spaces
        [InlineData(@".. .")]    // mixed trailing dots/spaces
        [InlineData(@"   ")]     // pure whitespace -- TrimEnd reduces to empty
        [InlineData("https://example.com/path/.. ")]
        public void TryGetSafeSourceCachePath_DotSegmentFileName_ReturnsFalse(string target)
        {
            // Path.GetFileName returns "." or ".." (or a trailing-space/dot variant) for these targets,
            // and none of those character sequences appears in Path.GetInvalidFileNameChars(), so the
            // structural containment invariant would be broken (<cacheDir>\<hash>\.. resolves to
            // <cacheDir>; the Windows kernel strips trailing spaces and dots so ".. " resolves identically
            // to "..") unless we explicitly reject them.
            Assert.False(TryGetSafeSourceCachePath(CacheDir, target, out string safe));
            Assert.Null(safe);
        }

        [Fact]
        public void TryGetSafeSourceCachePath_HttpUrlWithPathTraversal_IsStructurallyContained()
        {
            // Even when the URL path contains '..' segments before the final file name, the trailing
            // segment is what Path.GetFileName extracts -- and the structural containment must hold.
            Assert.True(TryGetSafeSourceCachePath(CacheDir, "https://example.com/a/../../../etc/passwd", out string safe));

            string canonical = Path.GetFullPath(CacheDir);
            string resolved = Path.GetFullPath(safe);
            Assert.StartsWith(canonical, resolved, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("passwd", Path.GetFileName(safe));
        }

        [Fact]
        public void TryGetSafeSourceCachePath_HttpUrlWithQueryString_ReturnsFalse()
        {
            // '?' is in Path.GetInvalidFileNameChars() on Windows, so a URL whose final path segment carries
            // a query string must be rejected rather than silently truncated.
            Assert.False(TryGetSafeSourceCachePath(CacheDir, "https://example.com/foo.cs?evil=1", out string safe));
            Assert.Null(safe);
        }

        [Fact]
        public void TryGetSafeSourceCachePath_BasicTarget_ProducesPathUnderCacheDir()
        {
            Assert.True(TryGetSafeSourceCachePath(CacheDir, @"C:\src\Program.cs", out string safe));
            Assert.NotNull(safe);

            // The result must be under the canonical cacheDir.
            string canonical = Path.GetFullPath(CacheDir);
            Assert.StartsWith(canonical, safe, StringComparison.OrdinalIgnoreCase);

            // The final segment is the file name from the target.
            Assert.Equal("Program.cs", Path.GetFileName(safe));

            // The middle segment is a 32-character hex hash subdirectory.
            string subdir = Path.GetFileName(Path.GetDirectoryName(safe));
            Assert.Equal(32, subdir.Length);
            foreach (char c in subdir)
            {
                Assert.True((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'), $"unexpected hex character '{c}'");
            }
        }

        [Fact]
        public void TryGetSafeSourceCachePath_PathTraversalInTarget_IsStructurallyContained()
        {
            // The PDB tries to escape the cache directory via '..\..\..\' traversal in SRCSRVTRG.
            Assert.True(TryGetSafeSourceCachePath(CacheDir, @"..\..\..\evil.exe", out string safe));

            // No matter how malicious the target is, the resulting path is anchored at the canonical
            // cache directory.  The file-name component is only ever 'evil.exe' (a single segment).
            string canonical = Path.GetFullPath(CacheDir);
            Assert.StartsWith(canonical, safe, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("evil.exe", Path.GetFileName(safe));

            // Most importantly: Path.GetFullPath(safe) must still resolve under canonical cacheDir.  This
            // is the structural property the rest of the system relies on.
            string resolved = Path.GetFullPath(safe);
            Assert.StartsWith(canonical, resolved, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void TryGetSafeSourceCachePath_AlternateRootInTarget_IsStructurallyContained()
        {
            Assert.True(TryGetSafeSourceCachePath(CacheDir, @"D:\Windows\System32\drivers\etc\hosts", out string safe));

            string canonical = Path.GetFullPath(CacheDir);
            string resolved = Path.GetFullPath(safe);
            Assert.StartsWith(canonical, resolved, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("hosts", Path.GetFileName(safe));
        }

        [Fact]
        public void TryGetSafeSourceCachePath_UncRootInTarget_IsStructurallyContained()
        {
            Assert.True(TryGetSafeSourceCachePath(CacheDir, @"\\evil-server\share\payload.dll", out string safe));

            string canonical = Path.GetFullPath(CacheDir);
            string resolved = Path.GetFullPath(safe);
            Assert.StartsWith(canonical, resolved, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("payload.dll", Path.GetFileName(safe));
        }

        [Fact]
        public void TryGetSafeSourceCachePath_HttpUrl_ProducesContainedPath()
        {
            Assert.True(TryGetSafeSourceCachePath(CacheDir, "https://example.com/team/Program.cs", out string safe));

            string canonical = Path.GetFullPath(CacheDir);
            Assert.StartsWith(canonical, safe, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Program.cs", Path.GetFileName(safe));
        }

        [Fact]
        public void TryGetSafeSourceCachePath_CanonicalizesCacheDirWithDot()
        {
            // Path.GetFullPath should resolve '.' references and produce an absolute cache directory.
            string relative = Path.Combine(Path.GetTempPath(), ".", "PerfViewCacheTest");
            Assert.True(TryGetSafeSourceCachePath(relative, @"C:\src\Program.cs", out string safe));

            string canonical = Path.GetFullPath(relative);
            Assert.StartsWith(canonical, safe, StringComparison.OrdinalIgnoreCase);
            // No literal "\.\" should remain in the safe path.
            Assert.DoesNotContain(@"\.\", safe);
        }

        [Fact]
        public void TryGetSafeSourceCachePath_CanonicalizesCacheDirWithTrailingSeparator()
        {
            Assert.True(TryGetSafeSourceCachePath(@"C:\cache\", @"C:\src\Program.cs", out string safe));
            string canonical = Path.GetFullPath(@"C:\cache\");
            Assert.StartsWith(canonical, safe, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void TryGetSafeSourceCachePath_DifferentTargetsSameFileName_MapToDistinctSubdirs()
        {
            // 'Program.cs' from two different repos must land in different cache directories.  Otherwise
            // a malicious PDB could collide with an existing trusted cache entry.
            string targetA = "https://contoso.example.com/projA/Program.cs";
            string targetB = "https://fabrikam.example.com/projB/Program.cs";

            Assert.True(TryGetSafeSourceCachePath(CacheDir, targetA, out string safeA));
            Assert.True(TryGetSafeSourceCachePath(CacheDir, targetB, out string safeB));

            Assert.NotEqual(safeA, safeB);
            Assert.Equal("Program.cs", Path.GetFileName(safeA));
            Assert.Equal("Program.cs", Path.GetFileName(safeB));
            Assert.NotEqual(Path.GetDirectoryName(safeA), Path.GetDirectoryName(safeB));
        }

        [Fact]
        public void TryGetSafeSourceCachePath_SameTarget_IsDeterministic()
        {
            // The cache path for a given (cacheDir, target) pair must be deterministic across calls in
            // the same process (and, because the hash is XxHash128, across runs/processes too).  This is
            // what makes the cache-hit short-circuit in GetSourceFromSrcServer work: opening the same
            // file twice must produce the same safe path and therefore find the existing file.
            string target = "https://example.com/team/repo/file.cs";

            Assert.True(TryGetSafeSourceCachePath(CacheDir, target, out string first));
            Assert.True(TryGetSafeSourceCachePath(CacheDir, target, out string second));

            Assert.Equal(first, second);
        }

        [Fact]
        public void TryGetSafeSourceCachePath_KnownTarget_HasStableHash()
        {
            // Pin the XxHash128 of a known target so that an accidental algorithm change (e.g. swapping
            // the hash, changing encoding, changing byte ordering) is caught by this test.  If this test
            // ever breaks legitimately because the hash is being changed on purpose, it can be updated.
            Assert.True(TryGetSafeSourceCachePath(CacheDir, "https://example.com/source.cs", out string safe));

            string subdir = Path.GetFileName(Path.GetDirectoryName(safe));
            Assert.Equal("6ea7349872f1f7e170a97a688b999a47", subdir);
        }
    }
}
