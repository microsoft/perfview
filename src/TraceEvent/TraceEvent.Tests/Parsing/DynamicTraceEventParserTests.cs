using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;

using Xunit;

using FormatHint = Microsoft.Diagnostics.Tracing.Parsers.TdhFormatter.FormatHint;
using TdhInputType = Microsoft.Diagnostics.Tracing.Parsers.RegisteredTraceEventParser.TdhInputType;
using TdhOutputType = Microsoft.Diagnostics.Tracing.Parsers.RegisteredTraceEventParser.TdhOutputType;

namespace TraceEventTests
{
    public class DynamicTraceEventParserTests
    {
        [Theory]
        [InlineData(@"..\outside", ".._outside.manifest.xml")]
        [InlineData(@"..\..\..\Startup\x", ".._.._.._Startup_x.manifest.xml")]
        [InlineData(@"C:\Windows\System32\evil", "C__Windows_System32_evil.manifest.xml")]
        public void WriteAllManifests_SanitizesPathTraversalProviderNames(string providerName, string expectedFileName)
        {
            // Integration smoke test: an attacker-controlled provider name containing
            // path-traversal characters must produce a manifest file whose name has
            // been sanitized AND whose final path remains inside the requested
            // directory.  The exhaustive sanitizer-behavior tests live in
            // PathUtilitiesTests.SanitizeFileName_*.
            string parentDirectory = CreateTempDirectory();
            string outputDirectory = Path.Combine(parentDirectory, "manifests");
            try
            {
                using (var source = new EventPipeEventSource(new MemoryStream()))
                {
                    var parser = new DynamicTraceEventParser(source);
                    parser.AddDynamicProvider(CreateProviderManifest(providerName, Guid.NewGuid()));

                    parser.WriteAllManifests(outputDirectory);
                }

                string[] manifestFiles = Directory.GetFiles(outputDirectory, "*.manifest.xml");
                string manifestPath = Assert.Single(manifestFiles);
                Assert.Equal(expectedFileName, Path.GetFileName(manifestPath));
                AssertPathInDirectory(manifestPath, outputDirectory);

                // No files leaked outside the output directory.
                Assert.Empty(Directory.GetFiles(parentDirectory, "*.manifest.xml", SearchOption.TopDirectoryOnly));
            }
            finally
            {
                Directory.Delete(parentDirectory, recursive: true);
            }
        }

        [Fact]
        public void WriteAllManifests_FallsBackToProviderGuidForEmptyName()
        {
            // PathUtilities.SanitizeFileName returns null for empty input;
            // WriteAllManifests then falls back to the provider's GUID so distinct
            // providers still produce distinct manifest files (rather than
            // colliding on a single "_.manifest.xml").
            Guid providerGuid = new Guid("12345678-1234-1234-1234-1234567890ab");
            string outputDirectory = CreateTempDirectory();
            try
            {
                using (var source = new EventPipeEventSource(new MemoryStream()))
                {
                    var parser = new DynamicTraceEventParser(source);
                    parser.AddDynamicProvider(CreateProviderManifest(string.Empty, providerGuid));

                    parser.WriteAllManifests(outputDirectory);
                }

                string[] manifestFiles = Directory.GetFiles(outputDirectory, "*.manifest.xml");
                string manifestPath = Assert.Single(manifestFiles);
                Assert.Equal(providerGuid.ToString("N") + ".manifest.xml", Path.GetFileName(manifestPath));
            }
            finally
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }

        [Fact]
        public void WriteAllManifests_PreservesValidProviderName()
        {
            string outputDirectory = CreateTempDirectory();
            try
            {
                const string providerName = "Contoso.Provider-Valid_1";
                using (var source = new EventPipeEventSource(new MemoryStream()))
                {
                    var parser = new DynamicTraceEventParser(source);
                    parser.AddDynamicProvider(CreateProviderManifest(providerName, Guid.NewGuid()));

                    parser.WriteAllManifests(outputDirectory);
                }

                string manifestPath = Path.Combine(outputDirectory, providerName + ".manifest.xml");
                Assert.True(File.Exists(manifestPath));
                Assert.Contains("name=\"" + providerName + "\"", File.ReadAllText(manifestPath));
            }
            finally
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }

        [Fact]
        public void WriteAllManifests_CreatesOutputDirectory()
        {
            string parentDirectory = CreateTempDirectory();
            string outputDirectory = Path.Combine(parentDirectory, "nested", "manifests");
            try
            {
                using (var source = new EventPipeEventSource(new MemoryStream()))
                {
                    var parser = new DynamicTraceEventParser(source);
                    parser.AddDynamicProvider(CreateProviderManifest("Contoso.Provider", Guid.NewGuid()));

                    parser.WriteAllManifests(outputDirectory);
                }

                Assert.True(Directory.Exists(outputDirectory));
                Assert.Single(Directory.GetFiles(outputDirectory, "*.manifest.xml"));
            }
            finally
            {
                Directory.Delete(parentDirectory, recursive: true);
            }
        }

        [Fact]
        public void WriteAllManifests_MultipleProvidersCollidingAfterSanitizationLastWriterWins()
        {
            // Two distinct attacker-controlled provider names that sanitize to the
            // same file name.  The current contract is that the second WriteToFile
            // overwrites the first -- this test pins that behavior so it does not
            // silently regress to e.g. throwing or corrupting the output directory.
            string outputDirectory = CreateTempDirectory();
            try
            {
                using (var source = new EventPipeEventSource(new MemoryStream()))
                {
                    var parser = new DynamicTraceEventParser(source);
                    parser.AddDynamicProvider(CreateProviderManifest(@"a\b", Guid.NewGuid()));
                    parser.AddDynamicProvider(CreateProviderManifest("a/b", Guid.NewGuid()));

                    parser.WriteAllManifests(outputDirectory);
                }

                string[] manifestFiles = Directory.GetFiles(outputDirectory, "*.manifest.xml");
                string manifestPath = Assert.Single(manifestFiles);
                Assert.Equal("a_b.manifest.xml", Path.GetFileName(manifestPath));
                AssertPathInDirectory(manifestPath, outputDirectory);
            }
            finally
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }

        [Theory]
        [InlineData(FormatHint.None, TdhInputType.Int32, TdhOutputType.Null)]
        [InlineData(FormatHint.None, TdhInputType.Int32, TdhOutputType.Int)]
        [InlineData(FormatHint.Decimal, TdhInputType.UInt64, TdhOutputType.Null)]
        [InlineData(FormatHint.Decimal, TdhInputType.UInt64, TdhOutputType.UnsignedLong)]
        [InlineData(FormatHint.None, TdhInputType.Pointer, TdhOutputType.HexBinary)]
        [InlineData(FormatHint.Hex, TdhInputType.UInt8, TdhOutputType.HexInt8)]
        [InlineData(FormatHint.Hex, TdhInputType.Int16, TdhOutputType.HexInt16)]
        [InlineData(FormatHint.Hex, TdhInputType.Int32, TdhOutputType.HexInt32)]
        [InlineData(FormatHint.Hex, TdhInputType.UInt64, TdhOutputType.HexInt64)]
        // Input type of pointer overrides hex formatting on output
        [InlineData(FormatHint.Pointer, TdhInputType.Pointer, TdhOutputType.HexInt8)]
        [InlineData(FormatHint.Pointer, TdhInputType.Pointer, TdhOutputType.HexInt16)]
        [InlineData(FormatHint.Pointer, TdhInputType.Pointer, TdhOutputType.HexInt32)]
        [InlineData(FormatHint.Pointer, TdhInputType.Pointer, TdhOutputType.HexInt64)]
        [InlineData(FormatHint.Pid, TdhInputType.UInt32, TdhOutputType.Pid)]
        [InlineData(FormatHint.Tid, TdhInputType.UInt32, TdhOutputType.Tid)]
        [InlineData(FormatHint.Port, TdhInputType.UInt16, TdhOutputType.Port)]
        [InlineData(FormatHint.IPv4, TdhInputType.UInt32, TdhOutputType.Ipv4)]
        [InlineData(FormatHint.IPv6, TdhInputType.Binary, TdhOutputType.Ipv6)]
        [InlineData(FormatHint.SocketAddress, TdhInputType.Binary, TdhOutputType.SocketAddress)]
        [InlineData(FormatHint.GenericError, TdhInputType.Int32, TdhOutputType.ErrorCode)]
        [InlineData(FormatHint.GenericError, TdhInputType.UInt32, TdhOutputType.ErrorCode)]
        [InlineData(FormatHint.Win32Error, TdhInputType.Int32, TdhOutputType.Win32Error)]
        [InlineData(FormatHint.Win32Error, TdhInputType.UInt32, TdhOutputType.Win32Error)]
        [InlineData(FormatHint.NtStatus, TdhInputType.Int32, TdhOutputType.NtStatus)]
        [InlineData(FormatHint.NtStatus, TdhInputType.UInt32, TdhOutputType.NtStatus)]
        [InlineData(FormatHint.HResult, TdhInputType.Int32, TdhOutputType.HResult)]
        [InlineData(FormatHint.HResult, TdhInputType.UInt32, TdhOutputType.HResult)]
        [InlineData(FormatHint.Pointer, TdhInputType.UInt32, TdhOutputType.CodePointer)]
        [InlineData(FormatHint.Pointer, TdhInputType.UInt64, TdhOutputType.CodePointer)]
        // HexIntXX input types are hex formatted even if there is no output hint
        [InlineData(FormatHint.Hex, TdhInputType.HexInt32, TdhOutputType.Null)]
        [InlineData(FormatHint.Hex, TdhInputType.HexInt64, TdhOutputType.Null)]
        public void ComputeFormatHintFromInOutTypes_MapsToExpectedFormatHint(object expected, object inType, object outType)
        {
            // The values are passed as object to avoid CS0051: "Inconsistent accessibility:
            // parameter type 'X' is less accessible than method 'Y'"
            Assert.Equal((FormatHint)expected, DynamicTraceEventData.PayloadFetch.ComputeFormatHintFromInOutTypes((TdhInputType)inType, (TdhOutputType)outType));
        }

        [Fact]
        public void StructValue_ToString_AppliesScalarFormattingHints()
        {
            var fieldFetches = new[]
            {
                new DynamicTraceEventData.PayloadFetch(0, TdhInputType.UInt16, TdhOutputType.UnsignedShort),
                new DynamicTraceEventData.PayloadFetch(2, TdhInputType.UInt32, TdhOutputType.HexInt32),
            };
            var value = new DynamicTraceEventData.StructValue(fieldFetches);
            value.Add("UInt16Field", (UInt16)42);
            value.Add("UInt32HexField", 0xDEADBEEF);

            Assert.Equal(
                "{ \"UInt16Field\":\"42\", \"UInt32HexField\":\"0xDEADBEEF\" }",
                value.ToString());
        }

        [Theory]
        [InlineData(TdhInputType.Int16, TdhOutputType.Null, typeof(short))]
        [InlineData(TdhInputType.Int16, TdhOutputType.String, typeof(char))]
        [InlineData(TdhInputType.UInt16, TdhOutputType.Null, typeof(ushort))]
        [InlineData(TdhInputType.UInt16, TdhOutputType.String, typeof(char))]
        [InlineData(TdhInputType.Int32, TdhOutputType.Null, typeof(int))]
        [InlineData(TdhInputType.UInt32, TdhOutputType.Null, typeof(uint))]
        [InlineData(TdhInputType.UInt32, TdhOutputType.HexInt32, typeof(uint))]
        [InlineData(TdhInputType.Int64, TdhOutputType.Null, typeof(long))]
        [InlineData(TdhInputType.UInt64, TdhOutputType.Null, typeof(ulong))]
        public void PayloadFetch_TdhIntegerTypes_PreserveSignedness(object inType, object outType, Type expectedType)
        {
            // Regression test for https://github.com/microsoft/perfview/issues/2493
            var fetch = new DynamicTraceEventData.PayloadFetch(0, (TdhInputType)inType, (TdhOutputType)outType);
            Assert.Equal(expectedType, fetch.Type);
        }

        [Fact]
        public void PayloadValue_TdhUnsignedIntegers_DecodeMaxValues()
        {
            // Regression test for https://github.com/microsoft/perfview/issues/2493
            byte[] payload = new byte[2 + 4 + 8 + 8];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = 0xFF;
            }

            var fetches = new[]
            {
                new DynamicTraceEventData.PayloadFetch(0, TdhInputType.UInt16, TdhOutputType.Null),
                new DynamicTraceEventData.PayloadFetch(2, TdhInputType.UInt32, TdhOutputType.Null),
                new DynamicTraceEventData.PayloadFetch(6, TdhInputType.UInt64, TdhOutputType.Null),
                new DynamicTraceEventData.PayloadFetch(14, TdhInputType.UInt64, TdhOutputType.HexInt64),
            };

            WithEvent(payload, fetches, traceEvent =>
            {
                Assert.Equal(ushort.MaxValue, Assert.IsType<ushort>(traceEvent.PayloadValue(0)));
                Assert.Equal(uint.MaxValue, Assert.IsType<uint>(traceEvent.PayloadValue(1)));
                Assert.Equal(ulong.MaxValue, Assert.IsType<ulong>(traceEvent.PayloadValue(2)));

                // Preserve decimal formatting for TDH integers unless an explicit hint requests hex.
                Assert.Equal(ushort.MaxValue.ToString("n0"), traceEvent.PayloadString(0));
                Assert.Equal(uint.MaxValue.ToString("n0"), traceEvent.PayloadString(1));
                Assert.Equal(ulong.MaxValue.ToString("n0"), traceEvent.PayloadString(2));
                Assert.Equal("0xFFFFFFFFFFFFFFFF", traceEvent.PayloadString(3));

                Assert.Equal("65535", traceEvent.PayloadString(0, CultureInfo.InvariantCulture));
                Assert.Equal("4294967295", traceEvent.PayloadString(1, CultureInfo.InvariantCulture));
                Assert.Equal("18446744073709551615", traceEvent.PayloadString(2, CultureInfo.InvariantCulture));
            });
        }

        [Theory]
        [InlineData(0U, false, "0")]
        [InlineData(0x0100007FU, false, "127.0.0.1")]
        [InlineData(0x0100007FU, true, "0x0100007F")]
        public void PayloadString_UnsignedIPv4Address_PreservesNameBasedFormatting(uint value, bool hexadecimal, string expected)
        {
            var outType = hexadecimal ? TdhOutputType.HexInt32 : TdhOutputType.Null;
            var fetches = new[] { new DynamicTraceEventData.PayloadFetch(0, TdhInputType.UInt32, outType) };
            WithEvent(BitConverter.GetBytes(value), fetches, traceEvent =>
            {
                traceEvent.payloadNames[0] = "IPv4Address";
                Assert.Equal(expected, traceEvent.PayloadString(0));
            });
        }

        [Fact]
        public void PayloadString_UnhintedUnsignedScalars_UseDecimalWithoutChangingMetadata()
        {
            var fetches = new[]
            {
                new DynamicTraceEventData.PayloadFetch(0, 2, typeof(ushort)),
                new DynamicTraceEventData.PayloadFetch(2, 4, typeof(uint)),
            };
            WithEvent(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }, fetches, traceEvent =>
            {
                Assert.Equal(ushort.MaxValue.ToString("n0"), traceEvent.PayloadString(0));
                Assert.Equal(uint.MaxValue.ToString("n0"), traceEvent.PayloadString(1));
                Assert.Equal("65535", traceEvent.PayloadString(0, CultureInfo.InvariantCulture));
                Assert.Equal("4294967295", traceEvent.PayloadString(1, CultureInfo.InvariantCulture));
                Assert.Equal(FormatHint.None, traceEvent.payloadFetches[0].FormatHint);
                Assert.Equal(FormatHint.None, traceEvent.payloadFetches[1].FormatHint);

                traceEvent.payloadNames[1] = "objectId";
                Assert.Equal(uint.MaxValue.ToString("n0"), traceEvent.PayloadString(1));
            });
        }

        [Theory]
        [InlineData(TdhOutputType.Null)]
        [InlineData(TdhOutputType.HexInt32)]
        public void PayloadString_UnsignedValueMap_TakesPrecedenceOverFormatting(object outType)
        {
            var fetch = new DynamicTraceEventData.PayloadFetch(0, TdhInputType.UInt32, (TdhOutputType)outType);
            fetch.Map = new Dictionary<long, string> { { uint.MaxValue, "AllBitsSet" } };
            WithEvent(BitConverter.GetBytes(uint.MaxValue), new[] { fetch }, traceEvent =>
            {
                Assert.Equal("AllBitsSet", traceEvent.PayloadString(0));
            });
        }

        [Fact]
        public void PayloadString_UnsupportedHint_FallsBackToUnsignedDecimal()
        {
            var fetch = new DynamicTraceEventData.PayloadFetch(0, TdhInputType.UInt16, TdhOutputType.HResult);
            WithEvent(BitConverter.GetBytes(ushort.MaxValue), new[] { fetch }, traceEvent =>
            {
                Assert.Equal(ushort.MaxValue.ToString("n0"), traceEvent.PayloadString(0));
                Assert.Equal("65535", traceEvent.PayloadString(0, CultureInfo.InvariantCulture));
            });
        }

        [Fact]
        public void PayloadString_UnhintedUnsignedArray_PreservesPlainDecimalElements()
        {
            var element = new DynamicTraceEventData.PayloadFetch(0, TdhInputType.UInt32, TdhOutputType.Null);
            var fetch = DynamicTraceEventData.PayloadFetch.FixedCountArrayPayloadFetch(0, element, 2);
            WithEvent(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xe8, 0x03, 0x00, 0x00 }, new[] { fetch }, traceEvent =>
            {
                Assert.Equal("[4294967295,1000]", traceEvent.PayloadString(0));
                Assert.Equal(FormatHint.None, traceEvent.payloadFetches[0].Array.Element.FormatHint);
            });
        }

        [Fact]
        public void PayloadString_TdhUInt64Array_DoesNotGroupDecimalElements()
        {
            var element = new DynamicTraceEventData.PayloadFetch(0, TdhInputType.UInt64, TdhOutputType.Null);
            var fetch = DynamicTraceEventData.PayloadFetch.FixedCountArrayPayloadFetch(0, element, 2);
            using (var payload = new MemoryStream())
            using (var writer = new BinaryWriter(payload))
            {
                writer.Write(1000UL);
                writer.Write(2000UL);
                WithEvent(payload.ToArray(), new[] { fetch }, traceEvent =>
                {
                    Assert.Equal("[1000,2000]", traceEvent.PayloadString(0));
                    Assert.Equal("[1000,2000]", traceEvent.PayloadString(0, CultureInfo.InvariantCulture));
                });
            }
        }

        [Fact]
        public void StructValue_ToString_PreservesUnhintedUnsignedFormatting()
        {
            var fetches = new[]
            {
                new DynamicTraceEventData.PayloadFetch(0, TdhInputType.UInt16, TdhOutputType.Null),
                new DynamicTraceEventData.PayloadFetch(2, TdhInputType.UInt32, TdhOutputType.Null),
            };
            var value = new DynamicTraceEventData.StructValue(fetches);
            value.Add("UInt16Field", ushort.MaxValue);
            value.Add("UInt32Field", uint.MaxValue);

            Assert.Equal("{ \"UInt16Field\":\"65535\", \"UInt32Field\":4294967295 }", value.ToString());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PayloadString_UnsignedLookupFailure_PreservesErrorHandling(bool explicitHint)
        {
            var traceEvent = new ThrowingUnsignedTraceEvent();
            if (explicitHint)
            {
                traceEvent.payloadFetches[0].FormatHint = FormatHint.Hex;
                Assert.Throws<InvalidOperationException>(() => traceEvent.PayloadString(0));
            }
            else
            {
                Assert.Equal("<<<EXCEPTION_DURING_VALUE_LOOKUP InvalidOperationException>>>", traceEvent.PayloadString(0));
            }
        }

        [Theory]
        [InlineData(0UL, "0x00000000")]
        [InlineData(112UL, "0x00000070")]
        [InlineData(0x80070138UL, "0x80070138")]
        [InlineData(ulong.MaxValue, "0xffffffffffffffff")]
        public void PayloadString_UInt64WithoutFormatHint_PreservesHexadecimal(ulong value, string expected)
        {
            var fetches = new[] { new DynamicTraceEventData.PayloadFetch(0, 8, typeof(ulong)) };
            WithEvent(BitConverter.GetBytes(value), fetches, traceEvent =>
            {
                Assert.Equal(value, Assert.IsType<ulong>(traceEvent.PayloadValue(0)));
                Assert.Equal(expected, traceEvent.PayloadString(0));
                Assert.Equal(expected, traceEvent.PayloadString(0, CultureInfo.InvariantCulture));
            });
        }

        [Fact]
        public void PayloadString_TdhUInt64ObjectId_PreservesHexadecimal()
        {
            var fetches = new[] { new DynamicTraceEventData.PayloadFetch(0, TdhInputType.UInt64, TdhOutputType.Null) };
            WithEvent(BitConverter.GetBytes(0xabcdefUL), fetches, traceEvent =>
            {
                traceEvent.payloadNames[0] = "objectId";
                Assert.Equal("0x00abcdef", traceEvent.PayloadString(0));
            });
        }

        [Fact]
        public void PayloadString_ValueMapWithLargeUInt64_DoesNotThrow()
        {
            byte[] payload = new byte[8];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = 0xFF;
            }

            var map = new Dictionary<long, string> { { -1, "AllBitsSet" } };
            var fetches = new[] { new DynamicTraceEventData.PayloadFetch(0, 8, typeof(ulong), map) };

            WithEvent(payload, fetches, traceEvent =>
            {
                Assert.Equal("AllBitsSet", traceEvent.PayloadString(0));
            });
        }

        [Fact]
        public void ParseProviderEvents_HonorsManifestOutTypeFormatting()
        {
            string manifest =
                "<instrumentationManifest xmlns=\"http://schemas.microsoft.com/win/2004/08/events\">" +
                "  <instrumentation>" +
                "    <events>" +
                "      <provider name=\"Contoso.Provider\" guid=\"" + Guid.NewGuid() + "\">" +
                "        <events>" +
                "          <event value=\"1\" symbol=\"MyEvent\" template=\"MyEventArgs\" />" +
                "        </events>" +
                "        <templates>" +
                "          <template tid=\"MyEventArgs\">" +
                "            <data name=\"Count\" inType=\"win:UInt32\" />" +
                "            <data name=\"Flags\" inType=\"win:UInt64\" outType=\"win:HexInt64\" />" +
                "            <data name=\"Keywords\" inType=\"win:UInt64\" />" +
                "            <data name=\"Pointer\" inType=\"win:Pointer\" />" +
                "          </template>" +
                "        </templates>" +
                "      </provider>" +
                "    </events>" +
                "  </instrumentation>" +
                "</instrumentationManifest>";

            var providerManifest = new ProviderManifest(new MemoryStream(Encoding.UTF8.GetBytes(manifest)));
            DynamicTraceEventData template = null;
            providerManifest.ParseProviderEvents(t =>
            {
                if ((int)t.ID == 1)
                {
                    template = t;
                }
                return EventFilterResponse.AcceptEvent;
            }, false);

            Assert.NotNull(template);
            Assert.Equal(new[] { "Count", "Flags", "Keywords", "Pointer" }, template.payloadNames);
            Assert.Equal(typeof(uint), template.payloadFetches[0].Type);
            Assert.Equal(FormatHint.None, template.payloadFetches[0].FormatHint);
            Assert.Equal(typeof(ulong), template.payloadFetches[1].Type);
            Assert.Equal(FormatHint.Hex, template.payloadFetches[1].FormatHint);
            Assert.Equal(typeof(ulong), template.payloadFetches[2].Type);
            Assert.Equal(FormatHint.None, template.payloadFetches[2].FormatHint);
            Assert.Equal(typeof(IntPtr), template.payloadFetches[3].Type);
            Assert.Equal(FormatHint.None, template.payloadFetches[3].FormatHint);

            using (var payload = new MemoryStream())
            using (var writer = new BinaryWriter(payload))
            using (var source = new EventPipeEventSource(new MemoryStream()))
            {
                source._QPCFreq = 1;
                source._syncTimeQPC = 1;
                source._syncTimeUTC = DateTime.UtcNow;
                source.sessionStartTimeQPC = 1;

                writer.Write(uint.MaxValue);
                writer.Write(0xabcdefUL);
                writer.Write(0x70UL);
                writer.Write(0x123456789abcdef0UL);

                WithEvent(payload.ToArray(), template.payloadFetches, traceEvent =>
                {
                    traceEvent.payloadNames = template.payloadNames;
                    traceEvent.traceEventSource = source;

                    Assert.Equal("4294967295", traceEvent.PayloadString(0, CultureInfo.InvariantCulture));
                    Assert.Equal("0x0000000000ABCDEF", traceEvent.PayloadString(1));
                    Assert.Equal("0x00000070", traceEvent.PayloadString(2));
                    Assert.Equal("0x123456789abcdef0", traceEvent.PayloadString(3));

                    string rendered = traceEvent.ToString();
                    Assert.Contains("Count=\"" + uint.MaxValue.ToString("n0") + "\"", rendered);
                    Assert.Contains("Flags=\"0x0000000000ABCDEF\"", rendered);
                    Assert.Contains("Keywords=\"0x00000070\"", rendered);
                    Assert.Contains("Pointer=\"0x123456789abcdef0\"", rendered);
                });
            }
        }

        [Fact]
        public void StructValue_ToString_AppliesNestedFormattingHints()
        {
            var innerFieldFetches = new[]
            {
                new DynamicTraceEventData.PayloadFetch(0, TdhInputType.Int32, TdhOutputType.HResult),
            };
            var innerClassInfo = new DynamicTraceEventData.PayloadFetchClassInfo
            {
                FieldNames = new[] { "InnerError" },
                FieldFetches = innerFieldFetches,
            };
            DynamicTraceEventData.PayloadFetch innerFetch =
                DynamicTraceEventData.PayloadFetch.StructPayloadFetch(0, innerClassInfo);
            DynamicTraceEventData.PayloadFetch innerArrayFetch =
                DynamicTraceEventData.PayloadFetch.FixedCountArrayPayloadFetch(0, innerFetch, 2);

            var firstArrayElement = new DynamicTraceEventData.StructValue(innerFieldFetches);
            firstArrayElement.Add("InnerError", unchecked((Int32)0x80004005));
            var secondArrayElement = new DynamicTraceEventData.StructValue(innerFieldFetches);
            secondArrayElement.Add("InnerError", 0);
            var child = new DynamicTraceEventData.StructValue(innerFieldFetches);
            child.Add("InnerError", unchecked((Int32)0x80070005));

            var value = new DynamicTraceEventData.StructValue(new[] { innerArrayFetch, innerFetch });
            value.Add("ArrayOfInner", new[] { firstArrayElement, secondArrayElement });
            value.Add("ChildOfInner", child);

            Assert.Equal(
                "{ \"ArrayOfInner\":[ { \"InnerError\":\"0x80004005\" }, { \"InnerError\":\"0x00000000\" } ], \"ChildOfInner\":{ \"InnerError\":\"0x80070005\" } }",
                value.ToString());
        }

        private sealed class ThrowingUnsignedTraceEvent : DynamicTraceEventData
        {
            public ThrowingUnsignedTraceEvent()
                : base(null, 1, 0, "Task", Guid.Empty, 0, "Opcode", Guid.Empty, "Provider")
            {
                payloadFetches = new[] { new PayloadFetch(0, TdhInputType.UInt32, TdhOutputType.Null) };
            }

            public override object PayloadValue(int index)
            {
                throw new InvalidOperationException("Payload lookup failed.");
            }
        }

        private static unsafe void WithEvent(byte[] payload, DynamicTraceEventData.PayloadFetch[] fetches, Action<DynamicTraceEventData> action)
        {
            fixed (byte* payloadBytes = payload)
            {
                TraceEventNativeMethods.EVENT_RECORD eventRecord = new TraceEventNativeMethods.EVENT_RECORD();
                eventRecord.EventHeader.Id = 1;
                eventRecord.EventHeader.Flags = TraceEventNativeMethods.EVENT_HEADER_FLAG_64_BIT_HEADER;
                eventRecord.UserDataLength = checked((ushort)payload.Length);
                eventRecord.UserData = (IntPtr)payloadBytes;

                var names = new string[fetches.Length];
                for (int i = 0; i < fetches.Length; i++)
                {
                    names[i] = "Field" + i;
                }

                var traceEvent = new DynamicTraceEventData(null, 1, 0, "Task", Guid.Empty, 0, "Opcode", Guid.Empty, "Provider");
                traceEvent.payloadFetches = fetches;
                traceEvent.payloadNames = names;
                traceEvent.eventRecord = &eventRecord;
                traceEvent.userData = eventRecord.UserData;

                action(traceEvent);
            }
        }

        private static ProviderManifest CreateProviderManifest(string providerName, Guid providerGuid)
        {
            string manifest =
                "<instrumentationManifest xmlns=\"http://schemas.microsoft.com/win/2004/08/events\">" +
                "  <instrumentation>" +
                "    <events>" +
                "      <provider name=\"" + SecurityElement.Escape(providerName) + "\" guid=\"" + providerGuid + "\" />" +
                "    </events>" +
                "  </instrumentation>" +
                "</instrumentationManifest>";

            return new ProviderManifest(new MemoryStream(Encoding.UTF8.GetBytes(manifest)));
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "TraceEventTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void AssertPathInDirectory(string path, string directoryPath)
        {
            string fullDirectoryPath = Path.GetFullPath(directoryPath);
            if (!fullDirectoryPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) &&
                !fullDirectoryPath.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                fullDirectoryPath += Path.DirectorySeparatorChar;
            }

            Assert.StartsWith(fullDirectoryPath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
        }
    }
}
