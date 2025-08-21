using System.IO;
using System.Text;
using Xunit.Abstractions;
using Microsoft.Diagnostics.Symbols;
using Xunit;
using System;

namespace TraceEventTests
{
    public class R2RPerfMapSymbolModuleTests : TestBase
    {
        public R2RPerfMapSymbolModuleTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void TestR2RPerfMapHeaders()
        {
            string contents =
                "FFFFFFFF 00 B0FBDF80EA6AA8A4F79673F8E8B0FBB0\n" +
                "FFFFFFFE 00 1\n" +
                "FFFFFFFD 00 2\n" +
                "FFFFFFFC 00 3\n" +
                "FFFFFFFB 00 1\n";

            byte[] bytes = Encoding.UTF8.GetBytes(contents);
            using (MemoryStream input = new MemoryStream(bytes))
            {
                R2RPerfMapSymbolModule symbolModule = new R2RPerfMapSymbolModule(input, 0);
                Assert.Equal(new Guid("B0FBDF80EA6AA8A4F79673F8E8B0FBB0"), symbolModule.Signature);
                Assert.Equal((uint)1, symbolModule.Version);
                Assert.Equal(R2RPerfMapOS.Linux, symbolModule.TargetOS);
                Assert.Equal(R2RPerfMapArchitecture.X64, symbolModule.TargetArchitecture);
                Assert.Equal(R2RPerfMapABI.Default, symbolModule.TargetABI);
            }
        }

        [Fact]
        public void TestR2RInvalidSignature()
        {
            string contents =
                "FFFFFFFF 00 B0FBDF80EA6AA8A4F79673F8E8B0FBB\n" +  // One digit missing.
                "FFFFFFFE 00 1\n" +
                "FFFFFFFD 00 2\n" +
                "FFFFFFFC 00 3\n" +
                "FFFFFFFB 00 1\n";

            byte[] bytes = Encoding.UTF8.GetBytes(contents);
            using (MemoryStream input = new MemoryStream(bytes))
            {
                Assert.Throws<FormatException>(() => new R2RPerfMapSymbolModule(input, 0));
            }
        }

        [Fact]
        public void TestR2RInvalidOS()
        {
            string contents =
                "FFFFFFFF 00 B0FBDF80EA6AA8A4F79673F8E8B0FBB0\n" +
                "FFFFFFFE 00 1\n" +
                "FFFFFFFD 00 7\n" +
                "FFFFFFFC 00 3\n" +
                "FFFFFFFB 00 1\n";

            byte[] bytes = Encoding.UTF8.GetBytes(contents);
            using (MemoryStream input = new MemoryStream(bytes))
            {
                Assert.Throws<FormatException>(() => new R2RPerfMapSymbolModule(input, 0));
            }
        }

        [Fact]
        public void TestR2RInvalidArchitecture()
        {
            string contents =
                "FFFFFFFF 00 B0FBDF80EA6AA8A4F79673F8E8B0FBB0\n" +
                "FFFFFFFE 00 1\n" +
                "FFFFFFFD 00 2\n" +
                "FFFFFFFC 00 5\n" +
                "FFFFFFFB 00 1\n";

            byte[] bytes = Encoding.UTF8.GetBytes(contents);
            using (MemoryStream input = new MemoryStream(bytes))
            {
                Assert.Throws<FormatException>(() => new R2RPerfMapSymbolModule(input, 0));
            }
        }

        [Fact]
        public void TestR2RInvalidAbi()
        {
            string contents =
                "FFFFFFFF 00 B0FBDF80EA6AA8A4F79673F8E8B0FBB0\n" +
                "FFFFFFFE 00 1\n" +
                "FFFFFFFD 00 2\n" +
                "FFFFFFFC 00 3\n" +
                "FFFFFFFB 00 3\n";

            byte[] bytes = Encoding.UTF8.GetBytes(contents);
            using (MemoryStream input = new MemoryStream(bytes))
            {
                Assert.Throws<FormatException>(() => new R2RPerfMapSymbolModule(input, 0));
            }
        }

        [Fact]
        public void TestR2RPerfMapSingleSymbol()
        {
            string contents =
                "FFFFFFFF 00 B0FBDF80EA6AA8A4F79673F8E8B0FBB0\n" +
                "FFFFFFFE 00 1\n" +
                "FFFFFFFD 00 2\n" +
                "FFFFFFFC 00 3\n" +
                "FFFFFFFB 00 1\n" +
                "0011AA00 23 Interop::CheckIo(Interop+Error, System.String, System.Boolean)";

            byte[] bytes = Encoding.UTF8.GetBytes(contents);
            using (MemoryStream input = new MemoryStream(bytes))
            {
                R2RPerfMapSymbolModule symbolModule = new R2RPerfMapSymbolModule(input, 0);
                Assert.Equal(new Guid("B0FBDF80EA6AA8A4F79673F8E8B0FBB0"), symbolModule.Signature);
                Assert.Equal((uint)1, symbolModule.Version);
                Assert.Equal(R2RPerfMapOS.Linux, symbolModule.TargetOS);
                Assert.Equal(R2RPerfMapArchitecture.X64, symbolModule.TargetArchitecture);
                Assert.Equal(R2RPerfMapABI.Default, symbolModule.TargetABI);
                for (uint i = 1; i < 0x0011AA00; i <<= 1)
                {
                    uint symbolStart = 0;
                    string name = symbolModule.FindNameForRva(i, ref symbolStart);
                    Assert.Equal(string.Empty, name);
                    Assert.Equal((uint)0, symbolStart);
                }
                for (uint i = 0x0011AA00; i < 0x0011AA00 + 0x23; i += 1)
                {
                    uint symbolStart = 0;
                    string name = symbolModule.FindNameForRva(i, ref symbolStart);
                    Assert.Equal("Interop::CheckIo(Interop+Error, System.String, System.Boolean)", name);
                    Assert.Equal((uint)0x0011AA00, symbolStart);
                }
                for (uint i = 0x0011AA00 + 0x23; i < 0x80000000; i <<= 1)
                {
                    uint symbolStart = 0;
                    string name = symbolModule.FindNameForRva(i, ref symbolStart);
                    Assert.Equal(string.Empty, name);
                    Assert.Equal((uint)0, symbolStart);
                }
            }
        }

        [Fact]
        public void TestR2RPerfMapMultipleSymbols()
        {
            string contents =
                "FFFFFFFF 00 B0FBDF80EA6AA8A4F79673F8E8B0FBB0\n" +
                "FFFFFFFE 00 1\n" +
                "FFFFFFFD 00 2\n" +
                "FFFFFFFC 00 3\n" +
                "FFFFFFFB 00 1\n" +
                "0011AA00 23 Interop::CheckIo(Interop+Error, System.String, System.Boolean)\n" +
                "0011AA30 52 System.Int64 Interop::CheckIo(System.Int64, System.String, System.Boolean)\n" +
                "0011AA90 13 System.Int32 Interop::CheckIo(System.Int32, System.String, System.Boolean)\n" +
                "0011AAB0 15 System.IntPtr Interop::CheckIo(System.IntPtr, System.String, System.Boolean)\n" +
                "0011AAD0 48E System.Exception Interop::GetExceptionForIoErrno(Interop + ErrorInfo, System.String, System.Boolean)\n" +
                "0011AF60 112 System.Exception Interop::GetIOException(Interop + ErrorInfo, System.String)\n" +
                "0011B080 5D Interop::GetRandomBytes(System.Byte *, System.Int32)\n" +
                "0011B0E0 7E Interop::GetCryptographicallySecureRandomBytes(System.Byte *, System.Int32)";
            byte[] bytes = Encoding.UTF8.GetBytes(contents);
            using (MemoryStream input = new MemoryStream(bytes))
            {
                R2RPerfMapSymbolModule symbolModule = new R2RPerfMapSymbolModule(input, 0);
                Assert.Equal(new Guid("B0FBDF80EA6AA8A4F79673F8E8B0FBB0"), symbolModule.Signature);
                Assert.Equal((uint)1, symbolModule.Version);
                Assert.Equal(R2RPerfMapOS.Linux, symbolModule.TargetOS);
                Assert.Equal(R2RPerfMapArchitecture.X64, symbolModule.TargetArchitecture);
                Assert.Equal(R2RPerfMapABI.Default, symbolModule.TargetABI);

                // Valid matches.
                uint symbolStart = 0;
                Assert.Equal("System.Int64 Interop::CheckIo(System.Int64, System.String, System.Boolean)", symbolModule.FindNameForRva(0x0011AA30, ref symbolStart));
                Assert.Equal("System.Exception Interop::GetExceptionForIoErrno(Interop + ErrorInfo, System.String, System.Boolean)", symbolModule.FindNameForRva(0x0011AAD0, ref symbolStart));
                Assert.Equal("Interop::GetCryptographicallySecureRandomBytes(System.Byte *, System.Int32)", symbolModule.FindNameForRva(0x0011B0E0, ref symbolStart));
                
                // Invalid matches.
                Assert.Equal(string.Empty, symbolModule.FindNameForRva(0x0011B0DD, ref symbolStart));
                Assert.Equal(string.Empty, symbolModule.FindNameForRva(0x0011AAC5, ref symbolStart));
                Assert.Equal(string.Empty, symbolModule.FindNameForRva(0x0011A9FF, ref symbolStart));
            }
        }
        
        [Fact]
        public void TestR2RPerfMapMissingAttributesSingleSymbol()
        {
            string contents =
                "FFFFFFFF 00 B0FBDF80EA6AA8A4F79673F8E8B0FBB0\n" +
                "FFFFFFFE 00 1\n" +
                "0011AA00 23 Interop::CheckIo(Interop+Error, System.String, System.Boolean)";

            byte[] bytes = Encoding.UTF8.GetBytes(contents);
            using (MemoryStream input = new MemoryStream(bytes))
            {
                R2RPerfMapSymbolModule symbolModule = new R2RPerfMapSymbolModule(input, 0);
                Assert.Equal(new Guid("B0FBDF80EA6AA8A4F79673F8E8B0FBB0"), symbolModule.Signature);
                Assert.Equal((uint)1, symbolModule.Version);
                Assert.Equal(default, symbolModule.TargetOS);
                Assert.Equal(default, symbolModule.TargetArchitecture);
                Assert.Equal(default, symbolModule.TargetABI);
                for (uint i = 1; i < 0x0011AA00; i <<= 1)
                {
                    uint symbolStart = 0;
                    string name = symbolModule.FindNameForRva(i, ref symbolStart);
                    Assert.Equal(string.Empty, name);
                    Assert.Equal((uint)0, symbolStart);
                }
                for (uint i = 0x0011AA00; i < 0x0011AA00 + 0x23; i += 1)
                {
                    uint symbolStart = 0;
                    string name = symbolModule.FindNameForRva(i, ref symbolStart);
                    Assert.Equal("Interop::CheckIo(Interop+Error, System.String, System.Boolean)", name);
                    Assert.Equal((uint)0x0011AA00, symbolStart);
                }
                for (uint i = 0x0011AA00 + 0x23; i < 0x80000000; i <<= 1)
                {
                    uint symbolStart = 0;
                    string name = symbolModule.FindNameForRva(i, ref symbolStart);
                    Assert.Equal(string.Empty, name);
                    Assert.Equal((uint)0, symbolStart);
                }
            }
        }
    }
}
