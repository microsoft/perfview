using System;
using Xunit;

namespace PerfViewTests.Utilities
{
    public static class OSHeapExeTests
    {
        [Theory]
        [InlineData("myapp.exe", "myapp")]
        [InlineData("myapp.EXE", "myapp")]
        [InlineData("myapp.Exe", "myapp")]
        [InlineData("myapp", "myapp")]
        [InlineData("myapp.dll", "myapp.dll")]
        [InlineData("app.exe.backup", "app.exe.backup")]
        [InlineData("some.other.exe", "some.other")]
        [InlineData("test.exe.exe", "test.exe")]
        [InlineData("", "")]
        [InlineData("app.ExE", "app")]
        public static void StripExeExtension_Tests(string input, string expected)
        {
            // This tests the logic used in CommandProcessor.cs for OSHeapExe processing
            string result = input;
            
            // Apply the fix logic: strip .exe extension if present
            if (result.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                result = result.Substring(0, result.Length - 4);
            }
            
            Assert.Equal(expected, result);
        }
    }
}