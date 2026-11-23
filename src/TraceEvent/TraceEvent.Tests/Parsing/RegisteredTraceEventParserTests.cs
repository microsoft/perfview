using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using FastSerialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class RegisteredTraceEventParserTests
    {
        private readonly ITestOutputHelper _output;

        public RegisteredTraceEventParserTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Test that GetManifestForRegisteredProvider does not produce duplicate string IDs in the stringTable.
        /// </summary>
        [WindowsFact]
        public void GetManifestForRegisteredProvider_NoDuplicateStringTableEntries()
        {
            const string providerName = "Microsoft-JScript";

            string manifest = RegisteredTraceEventParser.GetManifestForRegisteredProvider(providerName);

            Assert.NotNull(manifest);
            Assert.NotEmpty(manifest);

            _output.WriteLine($"Generated manifest for {providerName} (length: {manifest.Length} chars)");

            var stringIdPattern = new Regex(@"<string\s+id=""([^""]+)""", RegexOptions.Compiled);
            var matches = stringIdPattern.Matches(manifest);

            var stringIds = new List<string>();
            foreach (Match match in matches)
            {
                stringIds.Add(match.Groups[1].Value);
            }

            _output.WriteLine($"Found {stringIds.Count} string entries in stringTable");

            var duplicates = stringIds
                .GroupBy(id => id)
                .Where(g => g.Count() > 1)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToList();

            if (duplicates.Any())
            {
                _output.WriteLine($"Found {duplicates.Count} duplicate string IDs:");
                foreach (var dup in duplicates)
                {
                    _output.WriteLine($"  '{dup.Id}' appears {dup.Count} times");
                }
            }

            Assert.Empty(duplicates);
        }

        /// <summary>
        /// Test that GetManifestForRegisteredProvider produces well-formed XML that escapes special characters once.
        /// Microsoft-Windows-Ntfs has map entries named &lt;unknown&gt;.
        /// </summary>
        [WindowsFact]
        public void GetManifestForRegisteredProvider_ProperlyEscapesXmlCharacters()
        {
            const string providerName = "Microsoft-Windows-Ntfs";

            string manifest = RegisteredTraceEventParser.GetManifestForRegisteredProvider(providerName);

            Assert.NotNull(manifest);
            Assert.NotEmpty(manifest);

            _output.WriteLine($"Generated manifest for {providerName} (length: {manifest.Length} chars)");

            var xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.LoadXml(manifest);
                _output.WriteLine("Manifest is well-formed XML");
            }
            catch (XmlException ex)
            {
                _output.WriteLine($"Manifest XML parsing failed: {ex.Message}");
                _output.WriteLine($"Line {ex.LineNumber}, Position {ex.LinePosition}");
                
                var lines = manifest.Split('\n');
                if (ex.LineNumber > 0 && ex.LineNumber <= lines.Length)
                {
                    int start = Math.Max(0, ex.LineNumber - 3);
                    int end = Math.Min(lines.Length, ex.LineNumber + 2);
                    _output.WriteLine("\nContext:");
                    for (int i = start; i < end; i++)
                    {
                        string marker = (i == ex.LineNumber - 1) ? ">>> " : "    ";
                        _output.WriteLine($"{marker}{i + 1}: {lines[i]}");
                    }
                }
                
                throw;
            }

            var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("e", "http://schemas.microsoft.com/win/2004/08/events");
            nsmgr.AddNamespace("win", "http://manifests.microsoft.com/win/2004/08/windows/events");

            var keywords = xmlDoc.SelectNodes("//e:keyword", nsmgr);
            var tasks = xmlDoc.SelectNodes("//e:task", nsmgr);
            var opcodes = xmlDoc.SelectNodes("//e:opcode", nsmgr);
            var valueMaps = xmlDoc.SelectNodes("//e:valueMap", nsmgr);
            var bitMaps = xmlDoc.SelectNodes("//e:bitMap", nsmgr);
            var stringElements = xmlDoc.SelectNodes("//e:string", nsmgr);

            _output.WriteLine($"Found {keywords?.Count ?? 0} keywords");
            _output.WriteLine($"Found {tasks?.Count ?? 0} tasks");
            _output.WriteLine($"Found {opcodes?.Count ?? 0} opcodes");
            _output.WriteLine($"Found {valueMaps?.Count ?? 0} valueMaps");
            _output.WriteLine($"Found {bitMaps?.Count ?? 0} bitMaps");
            _output.WriteLine($"Found {stringElements?.Count ?? 0} string entries");

            Assert.Contains(stringElements.Cast<XmlElement>(), element => element.GetAttribute("value") == "<unknown>");
            Assert.Contains("value=\"&lt;unknown&gt;\"", manifest);
        }

        /// <summary>
        /// Test that the manifest output does not contain double-escaped XML entities.
        /// </summary>
        [WindowsFact]
        public void GetManifestForRegisteredProvider_NoDoubleEscapedEntities()
        {
            const string providerName = "Microsoft-Windows-DotNETRuntime";

            string manifest = RegisteredTraceEventParser.GetManifestForRegisteredProvider(providerName);

            Assert.NotNull(manifest);
            Assert.NotEmpty(manifest);

            // Double-escaping patterns that would indicate XmlEscape + XmlWriter double-escaping
            string[] doubleEscapePatterns = new[]
            {
                "&amp;amp;",
                "&amp;lt;",
                "&amp;gt;",
                "&amp;quot;",
                "&amp;apos;"
            };

            foreach (var pattern in doubleEscapePatterns)
            {
                Assert.DoesNotContain(pattern, manifest);
            }

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(manifest);
            var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("e", "http://schemas.microsoft.com/win/2004/08/events");
            Assert.NotEmpty(xmlDoc.SelectNodes("//e:string", nsmgr).Cast<XmlNode>());
        }

        /// <summary>
        /// Test that events sharing an Id and Version keep their own fields. Kerberos event 13 is two Event Log
        /// message ids, 0x0000000D and 0x8000000D, that differ only in the severity bits.
        /// </summary>
        [WindowsFact]
        public void GetManifestForRegisteredProvider_EventsSharingIdAndVersionKeepTheirOwnFields()
        {
            const string providerName = "Microsoft-Windows-Security-Kerberos";

            string manifest = RegisteredTraceEventParser.GetManifestForRegisteredProvider(providerName);

            Assert.NotNull(manifest);
            Assert.NotEmpty(manifest);

            _output.WriteLine($"Generated manifest for {providerName} (length: {manifest.Length} chars)");

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(manifest);

            var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("e", "http://schemas.microsoft.com/win/2004/08/events");

            // Template id -> its field names
            var templateFields = new Dictionary<string, string>();
            foreach (XmlNode template in xmlDoc.SelectNodes("//e:template", nsmgr))
            {
                var fieldNames = new List<string>();
                foreach (XmlNode data in template.SelectNodes("e:data", nsmgr))
                {
                    fieldNames.Add(data.Attributes?["name"]?.Value);
                }
                templateFields[template.Attributes?["tid"]?.Value] = string.Join(", ", fieldNames);
            }

            var eventFields = new List<string>();
            foreach (XmlNode eventNode in xmlDoc.SelectNodes("//e:event[@value='13'][@version='0']", nsmgr))
            {
                string templateId = eventNode.Attributes?["template"]?.Value;
                string fields = templateId != null ? templateFields[templateId] : string.Empty;
                _output.WriteLine($"Event 13 opcode={eventNode.Attributes?["opcode"]?.Value ?? "(none)"}: {fields}");
                eventFields.Add(fields);
            }

            Assert.Equal(2, eventFields.Count);
            Assert.Contains("Error, __binLength, binary", eventFields);
            Assert.Contains("Username, __binLength, binary", eventFields);
        }

        [Fact]
        public unsafe void EmbeddedTdhMetadataParser_ToleratesOutOfRangeTopLevelStringOffset()
        {
            byte[] buffer = CreateSinglePropertyEventInfoBuffer();
            fixed (byte* eventInfoBuffer = buffer)
            {
                var eventInfo = (RegisteredTraceEventParser.TRACE_EVENT_INFO*)eventInfoBuffer;
                eventInfo->ProviderNameOffset = buffer.Length + 2;

                DynamicTraceEventData template = new RegisteredTraceEventParser.TdhEventParser(eventInfoBuffer, buffer.Length, null, null).ParseEventMetaData();

                // An out-of-range descriptive string offset is tolerated: the event still parses and the
                // unreadable provider name falls back to its default rather than rejecting the event.
                Assert.NotNull(template);
                Assert.Equal("UnknownProvider", template.ProviderName);
            }
        }

        [Fact]
        public unsafe void EmbeddedTdhMetadataParser_DecodesUInt32AsUnsigned()
        {
            // Regression test for https://github.com/microsoft/perfview/issues/2493
            byte[] buffer = CreateSinglePropertyEventInfoBuffer();
            fixed (byte* eventInfoBuffer = buffer)
            {
                DynamicTraceEventData template = new RegisteredTraceEventParser.TdhEventParser(eventInfoBuffer, buffer.Length, null, null).ParseEventMetaData();

                Assert.NotNull(template);
                Assert.Equal(typeof(uint), template.payloadFetches[0].Type);
            }
        }

        [Fact]
        public unsafe void EmbeddedTdhMetadataParser_ValidatesPropertyArrayBounds()
        {
            byte[] buffer = CreateSinglePropertyEventInfoBuffer();
            fixed (byte* eventInfoBuffer = buffer)
            {
                var eventInfo = (RegisteredTraceEventParser.TRACE_EVENT_INFO*)eventInfoBuffer;
                eventInfo->PropertyCount = 2;
                eventInfo->TopLevelPropertyCount = 2;

                var parser = new RegisteredTraceEventParser.TdhEventParser(eventInfoBuffer, EventInfoPropertyArrayOffset + EventPropertyInfoSize, null, null);

                Assert.Null(parser.ParseEventMetaData());
            }
        }

        [Fact]
        public unsafe void EmbeddedTdhMetadataParser_ToleratesOutOfRangePropertyNameOffset()
        {
            byte[] buffer = CreateSinglePropertyEventInfoBuffer();
            fixed (byte* eventInfoBuffer = buffer)
            {
                var eventInfo = (RegisteredTraceEventParser.TRACE_EVENT_INFO*)eventInfoBuffer;
                eventInfo->EventPropertyInfoArray.NameOffset = buffer.Length + 2;

                DynamicTraceEventData template = new RegisteredTraceEventParser.TdhEventParser(eventInfoBuffer, buffer.Length, null, null).ParseEventMetaData();

                // An out-of-range property name offset is tolerated: the field keeps its place in the
                // payload (with an empty name) rather than rejecting the event.
                Assert.NotNull(template);
                Assert.Equal(new[] { "" }, template.PayloadNames);
            }
        }

        [Fact]
        public unsafe void EmbeddedTdhMetadataParser_ValidatesMapOffsets()
        {
            byte[] buffer = CreateMapInfoBuffer();
            fixed (byte* eventMapBuffer = buffer)
            {
                var eventMap = (RegisteredTraceEventParser.EVENT_MAP_INFO*)eventMapBuffer;
                eventMap->MapEntryArray.NameOffset = buffer.Length + 2;

                Assert.Null(RegisteredTraceEventParser.TdhEventParser.ParseMap(eventMap, eventMapBuffer, buffer.Length));
            }
        }

        [Fact]
        public unsafe void EmbeddedTdhMetadataParser_ValidatesStructRecursionDepth()
        {
            byte[] buffer = CreateSinglePropertyEventInfoBuffer();
            fixed (byte* eventInfoBuffer = buffer)
            {
                var eventInfo = (RegisteredTraceEventParser.TRACE_EVENT_INFO*)eventInfoBuffer;
                eventInfo->EventPropertyInfoArray.Flags = RegisteredTraceEventParser.PROPERTY_FLAGS.Struct;
                eventInfo->EventPropertyInfoArray.InType = (RegisteredTraceEventParser.TdhInputType)0;   // StructStartIndex.
                eventInfo->EventPropertyInfoArray.OutType = (RegisteredTraceEventParser.TdhOutputType)1; // NumOfStructMembers.

                var parser = new RegisteredTraceEventParser.TdhEventParser(eventInfoBuffer, buffer.Length, null, null);

                Assert.Null(parser.ParseEventMetaData());
            }
        }

        [Fact]
        public unsafe void EmbeddedTdhMetadataParser_ParsesValidLengthBoundMetadata()
        {
            byte[] buffer = CreateSinglePropertyEventInfoBuffer();
            fixed (byte* eventInfoBuffer = buffer)
            {
                var parser = new RegisteredTraceEventParser.TdhEventParser(eventInfoBuffer, buffer.Length, null, null);

                DynamicTraceEventData template = parser.ParseEventMetaData();

                Assert.NotNull(template);
                Assert.Equal(new[] { "Field" }, template.PayloadNames);
            }
        }

        [Fact]
        public unsafe void EmbeddedTdhMetadataParser_ParsesValidStructProperty()
        {
            // Build a TRACE_EVENT_INFO with two properties: a top-level struct property whose
            // single member is the UInt32 property at index 1.  This exercises the happy-path
            // recursion into ParseFields for nested struct members, and ensures that a non-zero
            // value in the MapNameOffset union slot of the struct property (which actually
            // overlays the struct 'padding' field in the native union) does not cause
            // ValidateEventInfo to falsely reject the event.
            byte[] buffer = CreateStructPropertyEventInfoBuffer();
            fixed (byte* eventInfoBuffer = buffer)
            {
                DynamicTraceEventData template = new RegisteredTraceEventParser.TdhEventParser(
                    eventInfoBuffer, buffer.Length, null, null).ParseEventMetaData();

                Assert.NotNull(template);
                Assert.Equal(new[] { "Outer" }, template.PayloadNames);
            }
        }

        [Fact]
        public unsafe void EmbeddedTdhMetadataParser_CapturesFormatHintFromOutType()
        {
            byte[] buffer = CreateSinglePropertyEventInfoBuffer();
            fixed (byte* eventInfoBuffer = buffer)
            {
                var eventInfo = (RegisteredTraceEventParser.TRACE_EVENT_INFO*)eventInfoBuffer;
                eventInfo->EventPropertyInfoArray.OutType = RegisteredTraceEventParser.TdhOutputType.HexInt32;

                DynamicTraceEventData template = new RegisteredTraceEventParser.TdhEventParser(eventInfoBuffer, buffer.Length, null, null).ParseEventMetaData();

                Assert.NotNull(template);
                Assert.Equal(new[] { "Field" }, template.PayloadNames);
                Assert.Equal(TdhFormatter.FormatHint.Hex, template.payloadFetches[0].FormatHint);
            }
        }

        [Fact]
        public unsafe void EmbeddedTdhMetadataParser_FormatHintSurvivesSerialization()
        {
            byte[] buffer = CreateSinglePropertyEventInfoBuffer();

            DynamicTraceEventData template;
            fixed (byte* eventInfoBuffer = buffer)
            {
                var eventInfo = (RegisteredTraceEventParser.TRACE_EVENT_INFO*)eventInfoBuffer;
                eventInfo->EventPropertyInfoArray.OutType = RegisteredTraceEventParser.TdhOutputType.HexInt32;
                template = new RegisteredTraceEventParser.TdhEventParser(eventInfoBuffer, buffer.Length, null, null).ParseEventMetaData();
            }
            Assert.NotNull(template);
            Assert.Equal(TdhFormatter.FormatHint.Hex, template.payloadFetches[0].FormatHint);

            // Round-trip the template through FastSerialization and confirm the hint is preserved.
            var stream = new MemoryStream();
            new Serializer(stream, template, leaveOpen: true).Dispose();

            stream.Position = 0;

            using var deserializer = new Deserializer(stream, "test", leaveOpen: true, SerializationSettings.Default);
            deserializer.RegisterFactory(typeof(DynamicTraceEventData), () => new DynamicTraceEventData(null, 0, 0, null, Guid.Empty, 0, null, Guid.Empty, null));
            var roundTripped = (DynamicTraceEventData)deserializer.GetEntryObject();
            Assert.Equal(TdhFormatter.FormatHint.Hex, roundTripped.payloadFetches[0].FormatHint);
        }

        [Fact]
        public unsafe void EmbeddedTdhMetadataParser_RejectsExponentialStructDag()
        {
            // A crafted TRACE_EVENT_INFO whose two struct properties each declare the SAME pair of
            // properties (indices 0 and 1) as their members forms a shared DAG.  Without a cumulative
            // work budget, ParseFields re-parses the same properties and the work doubles at every
            // recursion level (~2^32 invocations from a tiny blob) -> multi-hour hang + OOM.  The
            // PropertyCount work budget must reject this quickly because a property is revisited.
            // (Without the budget this test hangs, which is what makes it a genuine regression.)
            byte[] buffer = CreateRecursiveStructDagEventInfoBuffer();
            fixed (byte* eventInfoBuffer = buffer)
            {
                var parser = new RegisteredTraceEventParser.TdhEventParser(eventInfoBuffer, buffer.Length, null, null);

                Assert.Null(parser.ParseEventMetaData());
            }
        }

        [Fact]
        public unsafe void EmbeddedTdhMetadataParser_RejectsNestedStructArrayPrefixUnderflow()
        {
            // A nested struct whose first (and only) member is a variable-length array whose count
            // index points at the field immediately before the struct frame (startField - 1).  Inside
            // the nested frame curField == 0 and no field has been added yet, so the "length/count is
            // right before the array" prefix logic would index fieldFetches[-1].  The parser must not
            // throw on the untrusted metadata path: it detects the prefix underflow and rejects the
            // template (returns null) rather than crashing.
            byte[] buffer = CreateNestedStructArrayPrefixBuffer();
            fixed (byte* eventInfoBuffer = buffer)
            {
                DynamicTraceEventData template = new RegisteredTraceEventParser.TdhEventParser(
                    eventInfoBuffer, buffer.Length, null, null).ParseEventMetaData();

                Assert.Null(template);
            }
        }

        private static unsafe byte[] CreateRecursiveStructDagEventInfoBuffer()
        {
            byte[] buffer = new byte[EventInfoPropertyArrayOffset + (2 * EventPropertyInfoSize) + 128];
            int stringOffset = EventInfoPropertyArrayOffset + (2 * EventPropertyInfoSize);
            int providerNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Provider");
            int taskNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Task");
            int opcodeNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Opcode");
            int aNameOffset = AppendUnicodeString(buffer, ref stringOffset, "A");
            int bNameOffset = AppendUnicodeString(buffer, ref stringOffset, "B");

            fixed (byte* eventInfoBuffer = buffer)
            {
                var eventInfo = (RegisteredTraceEventParser.TRACE_EVENT_INFO*)eventInfoBuffer;
                eventInfo->ProviderGuid = Guid.NewGuid();
                eventInfo->EventGuid = Guid.NewGuid();
                eventInfo->EventDescriptor.Id = 1;
                eventInfo->ProviderNameOffset = providerNameOffset;
                eventInfo->TaskNameOffset = taskNameOffset;
                eventInfo->OpcodeNameOffset = opcodeNameOffset;
                eventInfo->PropertyCount = 2;
                eventInfo->TopLevelPropertyCount = 2;

                RegisteredTraceEventParser.EVENT_PROPERTY_INFO* properties = &eventInfo->EventPropertyInfoArray;

                // Both properties are structs whose members are the WHOLE property array [0, 2).
                // StructStartIndex=0 / NumOfStructMembers=2 encoded via the InType/OutType union slots.
                for (int i = 0; i < 2; i++)
                {
                    properties[i].Flags = RegisteredTraceEventParser.PROPERTY_FLAGS.Struct;
                    properties[i].InType = (RegisteredTraceEventParser.TdhInputType)0;   // StructStartIndex = 0
                    properties[i].OutType = (RegisteredTraceEventParser.TdhOutputType)2; // NumOfStructMembers = 2
                }
                properties[0].NameOffset = aNameOffset;
                properties[1].NameOffset = bNameOffset;
            }

            return buffer;
        }

        private static unsafe byte[] CreateNestedStructArrayPrefixBuffer()
        {
            byte[] buffer = new byte[EventInfoPropertyArrayOffset + (2 * EventPropertyInfoSize) + 128];
            int stringOffset = EventInfoPropertyArrayOffset + (2 * EventPropertyInfoSize);
            int providerNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Provider");
            int taskNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Task");
            int opcodeNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Opcode");
            int outerNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Outer");
            int innerNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Inner");

            fixed (byte* eventInfoBuffer = buffer)
            {
                var eventInfo = (RegisteredTraceEventParser.TRACE_EVENT_INFO*)eventInfoBuffer;
                eventInfo->ProviderGuid = Guid.NewGuid();
                eventInfo->EventGuid = Guid.NewGuid();
                eventInfo->EventDescriptor.Id = 1;
                eventInfo->ProviderNameOffset = providerNameOffset;
                eventInfo->TaskNameOffset = taskNameOffset;
                eventInfo->OpcodeNameOffset = opcodeNameOffset;
                eventInfo->PropertyCount = 2;
                eventInfo->TopLevelPropertyCount = 1;

                RegisteredTraceEventParser.EVENT_PROPERTY_INFO* properties = &eventInfo->EventPropertyInfoArray;

                // Top-level struct property whose single member is property index 1.
                properties[0].Flags = RegisteredTraceEventParser.PROPERTY_FLAGS.Struct;
                properties[0].NameOffset = outerNameOffset;
                properties[0].InType = (RegisteredTraceEventParser.TdhInputType)1;   // StructStartIndex = 1
                properties[0].OutType = (RegisteredTraceEventParser.TdhOutputType)1; // NumOfStructMembers = 1

                // Nested member: a variable-length (ParamCount) array whose count index points at
                // startField - 1 (= 0), i.e. just before this nested frame.  In the nested frame
                // curField == 0 and fieldFetches is empty, so the prefix removal would underflow.
                properties[1].Flags = RegisteredTraceEventParser.PROPERTY_FLAGS.ParamCount;
                properties[1].NameOffset = innerNameOffset;
                properties[1].InType = RegisteredTraceEventParser.TdhInputType.UInt32;
                properties[1].CountOrCountIndex = 0; // == startField(1) + curField(0) - 1
            }

            return buffer;
        }

        private static unsafe byte[] CreateStructPropertyEventInfoBuffer()
        {
            byte[] buffer = new byte[EventInfoPropertyArrayOffset + (2 * EventPropertyInfoSize) + 128];
            int stringOffset = EventInfoPropertyArrayOffset + (2 * EventPropertyInfoSize);
            int providerNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Provider");
            int taskNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Task");
            int opcodeNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Opcode");
            int outerNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Outer");
            int innerNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Inner");

            fixed (byte* eventInfoBuffer = buffer)
            {
                var eventInfo = (RegisteredTraceEventParser.TRACE_EVENT_INFO*)eventInfoBuffer;
                eventInfo->ProviderGuid = Guid.NewGuid();
                eventInfo->EventGuid = Guid.NewGuid();
                eventInfo->EventDescriptor.Id = 1;
                eventInfo->EventDescriptor.Task = 1;
                eventInfo->EventDescriptor.Opcode = 1;
                eventInfo->ProviderNameOffset = providerNameOffset;
                eventInfo->TaskNameOffset = taskNameOffset;
                eventInfo->OpcodeNameOffset = opcodeNameOffset;
                eventInfo->PropertyCount = 2;
                eventInfo->TopLevelPropertyCount = 1;

                RegisteredTraceEventParser.EVENT_PROPERTY_INFO* properties = &eventInfo->EventPropertyInfoArray;

                // Top-level struct property pointing at property index 1 as its single member.
                // Encode StructStartIndex=1 / NumOfStructMembers=1 via the union members.
                properties[0].Flags = RegisteredTraceEventParser.PROPERTY_FLAGS.Struct;
                properties[0].NameOffset = outerNameOffset;
                properties[0].InType = (RegisteredTraceEventParser.TdhInputType)1;   // StructStartIndex
                properties[0].OutType = (RegisteredTraceEventParser.TdhOutputType)1; // NumOfStructMembers
                // Deliberately set the slot that overlays MapNameOffset to a non-zero garbage
                // value to mimic real TDH output where the struct 'padding' field is not
                // guaranteed to be zero.  ValidateEventInfo must not interpret this as a
                // string offset for struct-typed properties.
                properties[0].MapNameOffset = unchecked((int)0xDEADBEEF);

                // The nested member.
                properties[1].NameOffset = innerNameOffset;
                properties[1].InType = RegisteredTraceEventParser.TdhInputType.UInt32;
            }

            return buffer;
        }

        private static unsafe byte[] CreateSinglePropertyEventInfoBuffer()
        {
            byte[] buffer = new byte[EventInfoPropertyArrayOffset + EventPropertyInfoSize + 128];
            int stringOffset = EventInfoPropertyArrayOffset + EventPropertyInfoSize;
            int providerNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Provider");
            int taskNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Task");
            int opcodeNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Opcode");
            int propertyNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Field");

            fixed (byte* eventInfoBuffer = buffer)
            {
                var eventInfo = (RegisteredTraceEventParser.TRACE_EVENT_INFO*)eventInfoBuffer;
                eventInfo->ProviderGuid = Guid.NewGuid();
                eventInfo->EventGuid = Guid.NewGuid();
                eventInfo->EventDescriptor.Id = 1;
                eventInfo->EventDescriptor.Task = 1;
                eventInfo->EventDescriptor.Opcode = 1;
                eventInfo->ProviderNameOffset = providerNameOffset;
                eventInfo->TaskNameOffset = taskNameOffset;
                eventInfo->OpcodeNameOffset = opcodeNameOffset;
                eventInfo->PropertyCount = 1;
                eventInfo->TopLevelPropertyCount = 1;

                RegisteredTraceEventParser.EVENT_PROPERTY_INFO* propertyInfo = &eventInfo->EventPropertyInfoArray;
                propertyInfo->NameOffset = propertyNameOffset;
                propertyInfo->InType = RegisteredTraceEventParser.TdhInputType.UInt32;
            }

            return buffer;
        }

        private static unsafe byte[] CreateMapInfoBuffer()
        {
            byte[] buffer = new byte[EventMapInfoEntryArrayOffset + EventMapEntrySize + 128];
            int stringOffset = EventMapInfoEntryArrayOffset + EventMapEntrySize;
            int mapNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Map");
            int entryNameOffset = AppendUnicodeString(buffer, ref stringOffset, "Entry");

            fixed (byte* eventMapBuffer = buffer)
            {
                var eventMap = (RegisteredTraceEventParser.EVENT_MAP_INFO*)eventMapBuffer;
                eventMap->NameOffset = mapNameOffset;
                eventMap->Flag = RegisteredTraceEventParser.MAP_FLAGS.EVENTMAP_INFO_FLAG_MANIFEST_VALUEMAP;
                eventMap->EntryCount = 1;
                eventMap->MapEntryArray.NameOffset = entryNameOffset;
                eventMap->MapEntryArray.Value = 1;
            }

            return buffer;
        }

        private static int AppendUnicodeString(byte[] buffer, ref int offset, string value)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(value + '\0');
            int originalOffset = offset;
            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
            offset += bytes.Length;
            return originalOffset;
        }

        private static readonly int EventInfoPropertyArrayOffset = (int)Marshal.OffsetOf(typeof(RegisteredTraceEventParser.TRACE_EVENT_INFO), "EventPropertyInfoArray");
        private static readonly int EventPropertyInfoSize = Marshal.SizeOf(typeof(RegisteredTraceEventParser.EVENT_PROPERTY_INFO));
        private static readonly int EventMapInfoEntryArrayOffset = (int)Marshal.OffsetOf(typeof(RegisteredTraceEventParser.EVENT_MAP_INFO), "MapEntryArray");
        private static readonly int EventMapEntrySize = Marshal.SizeOf(typeof(RegisteredTraceEventParser.EVENT_MAP_ENTRY));
    }
}
