using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        /// Test that GetManifestForRegisteredProvider produces well-formed XML.
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
        }

        /// <summary>
        /// Test that the new XmlWriter-based implementation produces semantically identical output
        /// to the legacy string-based implementation. The comparison normalizes both outputs as XML
        /// to account for formatting differences.
        /// </summary>
        [WindowsFact]
        public unsafe void GetManifestForRegisteredProvider_NewAndLegacyImplementationsProduceSameOutput()
        {
            const string providerName = "Microsoft-JScript";
            var providerGuid = TraceEventProviders.GetProviderGuidByName(providerName);

            _output.WriteLine($"Testing provider: {providerName} ({providerGuid})");

            string newManifest = RegisteredTraceEventParser.GetManifestForRegisteredProvider(providerGuid);
            string legacyManifest = GetManifestForRegisteredProvider_Legacy(providerGuid);

            Assert.NotNull(newManifest);
            Assert.NotEmpty(newManifest);
            Assert.NotNull(legacyManifest);
            Assert.NotEmpty(legacyManifest);

            _output.WriteLine($"New manifest length: {newManifest.Length} chars");
            _output.WriteLine($"Legacy manifest length: {legacyManifest.Length} chars");

            var newXmlDoc = new XmlDocument();
            var legacyXmlDoc = new XmlDocument();

            newXmlDoc.LoadXml(newManifest);
            _output.WriteLine("New manifest is well-formed XML");

            legacyXmlDoc.LoadXml(legacyManifest);
            _output.WriteLine("Legacy manifest is well-formed XML");

            NormalizeXml(newXmlDoc);
            NormalizeXml(legacyXmlDoc);

            string normalizedNew = newXmlDoc.OuterXml;
            string normalizedLegacy = legacyXmlDoc.OuterXml;

            if (normalizedNew != normalizedLegacy)
            {
                _output.WriteLine("Normalized XML documents are different");
                _output.WriteLine($"Normalized new manifest length: {normalizedNew.Length}");
                _output.WriteLine($"Normalized legacy manifest length: {normalizedLegacy.Length}");

                int diffIndex = 0;
                int minLength = Math.Min(normalizedNew.Length, normalizedLegacy.Length);
                for (int i = 0; i < minLength; i++)
                {
                    if (normalizedNew[i] != normalizedLegacy[i])
                    {
                        diffIndex = i;
                        break;
                    }
                }

                int contextStart = Math.Max(0, diffIndex - 100);

                _output.WriteLine($"\nFirst difference at position {diffIndex}:");
                _output.WriteLine($"New:    ...{normalizedNew.Substring(contextStart, Math.Min(200, normalizedNew.Length - contextStart))}...");
                _output.WriteLine($"Legacy: ...{normalizedLegacy.Substring(contextStart, Math.Min(200, normalizedLegacy.Length - contextStart))}...");
            }
            else
            {
                _output.WriteLine("Both implementations produce identical normalized XML");
            }

            Assert.Equal(normalizedLegacy, normalizedNew);
        }

        /// <summary>
        /// Test that the new XmlWriter-based implementation produces semantically identical output
        /// to the legacy string-based implementation for the Microsoft-Windows-DotNETRuntime provider,
        /// which is a complex provider with many events, keywords, tasks, opcodes, and maps.
        /// </summary>
        [WindowsFact]
        public unsafe void GetManifestForRegisteredProvider_DotNETRuntime_NewAndLegacyMatch()
        {
            const string providerName = "Microsoft-Windows-DotNETRuntime";
            var providerGuid = TraceEventProviders.GetProviderGuidByName(providerName);

            _output.WriteLine($"Testing provider: {providerName} ({providerGuid})");

            string newManifest = RegisteredTraceEventParser.GetManifestForRegisteredProvider(providerGuid);
            string legacyManifest = GetManifestForRegisteredProvider_Legacy(providerGuid);

            Assert.NotNull(newManifest);
            Assert.NotEmpty(newManifest);
            Assert.NotNull(legacyManifest);
            Assert.NotEmpty(legacyManifest);

            _output.WriteLine($"New manifest length: {newManifest.Length} chars");
            _output.WriteLine($"Legacy manifest length: {legacyManifest.Length} chars");

            var newXmlDoc = new XmlDocument();
            var legacyXmlDoc = new XmlDocument();

            newXmlDoc.LoadXml(newManifest);
            _output.WriteLine("New manifest is well-formed XML");

            legacyXmlDoc.LoadXml(legacyManifest);
            _output.WriteLine("Legacy manifest is well-formed XML");

            NormalizeXml(newXmlDoc);
            NormalizeXml(legacyXmlDoc);

            string normalizedNew = newXmlDoc.OuterXml;
            string normalizedLegacy = legacyXmlDoc.OuterXml;

            if (normalizedNew != normalizedLegacy)
            {
                _output.WriteLine("Normalized XML documents are different");

                int diffIndex = 0;
                int minLength = Math.Min(normalizedNew.Length, normalizedLegacy.Length);
                for (int i = 0; i < minLength; i++)
                {
                    if (normalizedNew[i] != normalizedLegacy[i])
                    {
                        diffIndex = i;
                        break;
                    }
                }

                int contextStart = Math.Max(0, diffIndex - 100);

                _output.WriteLine($"\nFirst difference at position {diffIndex}:");
                _output.WriteLine($"New:    ...{normalizedNew.Substring(contextStart, Math.Min(200, normalizedNew.Length - contextStart))}...");
                _output.WriteLine($"Legacy: ...{normalizedLegacy.Substring(contextStart, Math.Min(200, normalizedLegacy.Length - contextStart))}...");
            }
            else
            {
                _output.WriteLine("Both implementations produce identical normalized XML");
            }

            Assert.Equal(normalizedLegacy, normalizedNew);
        }

        #region Legacy Implementation (for comparison testing only)

        /// <summary>
        /// Legacy implementation of GetManifestForRegisteredProvider using string concatenation.
        /// Moved here from RegisteredTraceEventParser to keep it out of the production library.
        /// </summary>
        private static unsafe string GetManifestForRegisteredProvider_Legacy(Guid providerGuid)
        {
            int buffSize = 84000;
            var buffer = new byte[buffSize];
            byte* enumBuffer = null;

            TraceEventNativeMethods.EVENT_RECORD eventRecord = new TraceEventNativeMethods.EVENT_RECORD();
            eventRecord.EventHeader.ProviderId = providerGuid;

            string providerName = null;
            SortedDictionary<int, StringWriter> events = new SortedDictionary<int, StringWriter>();
            SortedDictionary<int, LegacyTaskInfo> tasks = new SortedDictionary<int, LegacyTaskInfo>();
            Dictionary<string, string> templateIntern = new Dictionary<string, string>(8);

            Dictionary<string, string> enumIntern = new Dictionary<string, string>();
            StringWriter enumLocalizations = new StringWriter();

            HashSet<string> emittedStringIds = new HashSet<string>();

            Dictionary<string, int> taskNames = new Dictionary<string, int>();
            Dictionary<string, int> opcodeNames = new Dictionary<string, int>();
            Dictionary<string, int> eventNames = new Dictionary<string, int>();

            SortedDictionary<ulong, string> keywords = new SortedDictionary<ulong, string>();
            List<ProviderDataItem> keywordsItems = TraceEventProviders.GetProviderKeywords(providerGuid);
            if (keywordsItems != null)
            {
                foreach (var keywordItem in keywordsItems)
                {
                    if (keywordItem.Value >= 1000000000000UL)
                    {
                        continue;
                    }

                    keywords[keywordItem.Value] = MakeLegalIdentifier(keywordItem.Name);
                }
            }

            int status;

            for (; ; )
            {
                int size = buffer.Length;
                status = RegisteredTraceEventParser.TdhEnumerateManifestProviderEvents(eventRecord.EventHeader.ProviderId, buffer, ref size);
                if (status != 122 || 20000000 < size)
                {
                    break;
                }

                buffer = new byte[size];
            }

            if (status == 0)
            {
                const int NumberOfEventsOffset = 0;
                const int FirstDescriptorOffset = 8;
                int eventCount = BitConverter.ToInt32(buffer, NumberOfEventsOffset);
                var descriptors = new RegisteredTraceEventParser.EVENT_DESCRIPTOR[eventCount];
                fixed (RegisteredTraceEventParser.EVENT_DESCRIPTOR* pDescriptors = descriptors)
                {
                    Marshal.Copy(buffer, FirstDescriptorOffset, (IntPtr)pDescriptors, descriptors.Length * sizeof(RegisteredTraceEventParser.EVENT_DESCRIPTOR));
                }

                foreach (var descriptor in descriptors)
                {
                    for (; ; )
                    {
                        int size = buffer.Length;
                        status = RegisteredTraceEventParser.TdhGetManifestEventInformation(eventRecord.EventHeader.ProviderId, descriptor, buffer, ref size);
                        if (status != 122 || 20000000 < size)
                        {
                            break;
                        }

                        buffer = new byte[size];
                    }

                    if (status != 0)
                    {
                        continue;
                    }

                    fixed (byte* eventInfoBuff = buffer)
                    {
                        var eventInfo = (RegisteredTraceEventParser.TRACE_EVENT_INFO*)eventInfoBuff;
                        RegisteredTraceEventParser.EVENT_PROPERTY_INFO* propertyInfos = &eventInfo->EventPropertyInfoArray;

                        if (providerName == null)
                        {
                            if (eventInfo->ProviderNameOffset != 0)
                            {
                                providerName = new string((char*)(&eventInfoBuff[eventInfo->ProviderNameOffset]));
                            }
                            else
                            {
                                providerName = "provider(" + eventInfo->ProviderGuid.ToString() + ")";
                            }
                        }

                        string taskName = null;
                        if (eventInfo->TaskNameOffset != 0)
                        {
                            taskName = MakeLegalIdentifier((new string((char*)(&eventInfoBuff[eventInfo->TaskNameOffset]))));
                        }
                        if (taskName == null)
                        {
                            taskName = "task_" + eventInfo->EventDescriptor.Task.ToString();
                        }

                        int taskNumForName;
                        if (taskNames.TryGetValue(taskName, out taskNumForName) && taskNumForName != eventInfo->EventDescriptor.Task)
                        {
                            taskName = taskName + "_" + eventInfo->EventDescriptor.Task.ToString();
                        }

                        taskNames[taskName] = eventInfo->EventDescriptor.Task;

                        string opcodeName = "";
                        if (eventInfo->EventDescriptor.Opcode != 0)
                        {
                            if (eventInfo->OpcodeNameOffset != 0)
                            {
                                opcodeName = MakeLegalIdentifier((new string((char*)(&eventInfoBuff[eventInfo->OpcodeNameOffset]))));
                            }
                            else
                            {
                                opcodeName = "opcode_" + eventInfo->EventDescriptor.Opcode.ToString();
                            }
                        }

                        int opcodeNumForName;
                        if (opcodeNames.TryGetValue(opcodeName, out opcodeNumForName) && opcodeNumForName != eventInfo->EventDescriptor.Opcode)
                        {
                            if (eventInfo->OpcodeNameOffset == 0)
                            {
                                opcodeName = "opcode";
                            }

                            opcodeName = opcodeName + "_" + eventInfo->EventDescriptor.Task.ToString() + "_" + eventInfo->EventDescriptor.Opcode.ToString();
                        }
                        opcodeNames[opcodeName] = eventInfo->EventDescriptor.Opcode;

                        string eventName = taskName;
                        if (!taskName.EndsWith(opcodeName, StringComparison.OrdinalIgnoreCase))
                        {
                            eventName += Capitalize(opcodeName);
                        }

                        int eventNumForName;
                        if (eventNames.TryGetValue(eventName, out eventNumForName) && eventNumForName != eventInfo->EventDescriptor.Id)
                        {
                            eventName = eventName + eventInfo->EventDescriptor.Id.ToString();
                        }

                        eventNames[eventName] = eventInfo->EventDescriptor.Id;

                        LegacyTaskInfo taskInfo;
                        if (!tasks.TryGetValue(eventInfo->EventDescriptor.Task, out taskInfo))
                        {
                            tasks[eventInfo->EventDescriptor.Task] = taskInfo = new LegacyTaskInfo() { Name = taskName };
                        }

                        var symbolName = eventName;
                        if (eventInfo->EventDescriptor.Version > 0)
                        {
                            symbolName += "_V" + eventInfo->EventDescriptor.Version;
                        }

                        StringWriter eventWriter;
                        if (!events.TryGetValue(eventInfo->EventDescriptor.Id, out eventWriter))
                        {
                            events[eventInfo->EventDescriptor.Id] = eventWriter = new StringWriter();
                        }

                        eventWriter.Write("     <event value=\"{0}\" symbol=\"{1}\" version=\"{2}\" task=\"{3}\"",
                            eventInfo->EventDescriptor.Id,
                            symbolName,
                            eventInfo->EventDescriptor.Version,
                            taskName);
                        if (eventInfo->EventDescriptor.Opcode != 0)
                        {
                            string opcodeId;
                            if (eventInfo->EventDescriptor.Opcode < 10)
                            {
                                if (eventInfo->EventDescriptor.Opcode == (byte)TraceEventOpcode.DataCollectionStart)
                                {
                                    opcodeId = "win:DC_Start";
                                }
                                else if (eventInfo->EventDescriptor.Opcode == (byte)TraceEventOpcode.DataCollectionStop)
                                {
                                    opcodeId = "win:DC_Stop";
                                }
                                else
                                {
                                    opcodeId = "win:" + opcodeName;
                                }
                            }
                            else
                            {
                                opcodeId = opcodeName;
                                if (taskInfo.Opcodes == null)
                                {
                                    taskInfo.Opcodes = new SortedDictionary<int, string>();
                                }

                                if (!taskInfo.Opcodes.ContainsKey(eventInfo->EventDescriptor.Opcode))
                                {
                                    taskInfo.Opcodes[eventInfo->EventDescriptor.Opcode] = opcodeId;
                                }
                            }
                            eventWriter.Write(" opcode=\"{0}\"", opcodeId);
                        }
                        if ((int)TraceEventLevel.Always <= eventInfo->EventDescriptor.Level && eventInfo->EventDescriptor.Level <= (int)TraceEventLevel.Verbose)
                        {
                            var asLevel = (TraceEventLevel)eventInfo->EventDescriptor.Level;
                            var levelName = "win:" + asLevel;
                            eventWriter.Write(" level=\"{0}\"", levelName);
                        }

                        var keywordStr = GetKeywordStr(keywords, (ulong)eventInfo->EventDescriptor.Keyword);
                        if (keywordStr.Length > 0)
                        {
                            eventWriter.Write(" keywords=\"" + keywordStr + "\"", eventInfo->EventDescriptor.Keyword);
                        }

                        if (eventInfo->TopLevelPropertyCount != 0)
                        {
                            var templateWriter = new StringWriter();
                            string[] propertyNames = new string[eventInfo->TopLevelPropertyCount];
                            for (int j = 0; j < eventInfo->TopLevelPropertyCount; j++)
                            {
                                RegisteredTraceEventParser.EVENT_PROPERTY_INFO* propertyInfo = &propertyInfos[j];
                                var propertyName = new string((char*)(&eventInfoBuff[propertyInfo->NameOffset]));
                                propertyNames[j] = propertyName;
                                var enumAttrib = "";

                                if (propertyInfo->MapNameOffset != 0)
                                {
                                    string mapName = new string((char*)(&eventInfoBuff[propertyInfo->MapNameOffset]));

                                    if (enumBuffer == null)
                                    {
                                        enumBuffer = (byte*)System.Runtime.InteropServices.Marshal.AllocHGlobal(buffSize);
                                    }

                                    if (!enumIntern.ContainsKey(mapName))
                                    {
                                        RegisteredTraceEventParser.EVENT_MAP_INFO* enumInfo = (RegisteredTraceEventParser.EVENT_MAP_INFO*)enumBuffer;
                                        var hr = RegisteredTraceEventParser.TdhGetEventMapInformation(&eventRecord, mapName, enumInfo, ref buffSize);
                                        if (hr == 0)
                                        {
                                            if (enumInfo->Flag == RegisteredTraceEventParser.MAP_FLAGS.EVENTMAP_INFO_FLAG_MANIFEST_VALUEMAP ||
                                                enumInfo->Flag == RegisteredTraceEventParser.MAP_FLAGS.EVENTMAP_INFO_FLAG_MANIFEST_BITMAP)
                                            {
                                                StringWriter enumWriter = new StringWriter();
                                                string enumName = new string((char*)(&enumBuffer[enumInfo->NameOffset]));
                                                enumAttrib = " map=\"" + XmlUtilities.XmlEscape(enumName) + "\"";
                                                if (enumInfo->Flag == RegisteredTraceEventParser.MAP_FLAGS.EVENTMAP_INFO_FLAG_MANIFEST_VALUEMAP)
                                                {
                                                    enumWriter.WriteLine("     <valueMap name=\"{0}\">", XmlUtilities.XmlEscape(enumName));
                                                }
                                                else
                                                {
                                                    enumWriter.WriteLine("     <bitMap name=\"{0}\">", XmlUtilities.XmlEscape(enumName));
                                                }

                                                RegisteredTraceEventParser.EVENT_MAP_ENTRY* mapEntries = &enumInfo->MapEntryArray;
                                                for (int k = 0; k < enumInfo->EntryCount; k++)
                                                {
                                                    int value = mapEntries[k].Value;
                                                    string valueName = new string((char*)(&enumBuffer[mapEntries[k].NameOffset])).Trim();
                                                    string escapedValueName = XmlUtilities.XmlEscape(valueName);
                                                    string stringId = XmlUtilities.XmlEscape($"map_{enumName}{valueName}");
                                                    enumWriter.WriteLine("      <map value=\"0x{0:x}\" message=\"$(string.{1})\"/>", value, stringId);
                                                    if (emittedStringIds.Add(stringId))
                                                    {
                                                        enumLocalizations.WriteLine("    <string id=\"{0}\" value=\"{1}\"/>", stringId, escapedValueName);
                                                    }
                                                }
                                                if (enumInfo->Flag == RegisteredTraceEventParser.MAP_FLAGS.EVENTMAP_INFO_FLAG_MANIFEST_VALUEMAP)
                                                {
                                                    enumWriter.WriteLine("     </valueMap>");
                                                }
                                                else
                                                {
                                                    enumWriter.WriteLine("     </bitMap>");
                                                }

                                                enumIntern[mapName] = enumWriter.ToString();
                                            }
                                        }
                                    }
                                }

                                propertyName = Regex.Replace(propertyName, "[^A-Za-z0-9_]", "");
                                RegisteredTraceEventParser.TdhInputType propertyType = propertyInfo->InType;
                                string countOrLengthAttrib = "";

                                if ((propertyInfo->Flags & RegisteredTraceEventParser.PROPERTY_FLAGS.ParamCount) != 0)
                                {
                                    countOrLengthAttrib = " count=\"" + propertyNames[propertyInfo->CountOrCountIndex] + "\"";
                                }
                                else if ((propertyInfo->Flags & RegisteredTraceEventParser.PROPERTY_FLAGS.ParamLength) != 0)
                                {
                                    countOrLengthAttrib = " length=\"" + propertyNames[propertyInfo->LengthOrLengthIndex] + "\"";
                                }

                                templateWriter.WriteLine("      <data name=\"{0}\" inType=\"win:{1}\"{2}{3}/>", propertyName, propertyType.ToString(), enumAttrib, countOrLengthAttrib);
                            }
                            var templateStr = templateWriter.ToString();

                            string templateName;
                            if (!templateIntern.TryGetValue(templateStr, out templateName))
                            {
                                templateName = eventName + "Args";
                                if (eventInfo->EventDescriptor.Version > 0)
                                {
                                    templateName += "_V" + eventInfo->EventDescriptor.Version;
                                }

                                templateIntern[templateStr] = templateName;
                            }
                            eventWriter.Write(" template=\"{0}\"", templateName);
                        }
                        eventWriter.WriteLine("/>");
                    }
                }
            }
            if (enumBuffer != null)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)enumBuffer);
            }

            if (providerName == null)
            {
                throw new ApplicationException("Could not find provider with at GUID of " + providerGuid.ToString());
            }

            StringWriter manifest = new StringWriter();
            manifest.WriteLine("<instrumentationManifest xmlns=\"http://schemas.microsoft.com/win/2004/08/events\">");
            manifest.WriteLine(" <instrumentation xmlns:xs=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:win=\"http://manifests.microsoft.com/win/2004/08/windows/events\">");
            manifest.WriteLine("  <events>");
            manifest.WriteLine("   <provider name=\"{0}\" guid=\"{{{1}}}\" resourceFileName=\"{0}\" messageFileName=\"{0}\" symbol=\"{2}\" source=\"Xml\" >",
                    providerName, providerGuid, Regex.Replace(providerName, @"[^\w]", ""));

            StringWriter localizedStrings = new StringWriter();

            if (keywords != null)
            {
                manifest.WriteLine("    <keywords>");
                foreach (var keyValue in keywords)
                {
                    string escapedValue = XmlUtilities.XmlEscape(keyValue.Value);
                    string stringId = XmlUtilities.XmlEscape($"keyword_{keyValue.Value}");
                    manifest.WriteLine("     <keyword name=\"{0}\" message=\"$(string.{1})\" mask=\"0x{2:x}\"/>",
                        escapedValue, stringId, keyValue.Key);
                    if (emittedStringIds.Add(stringId))
                    {
                        localizedStrings.WriteLine("    <string id=\"{0}\" value=\"{1}\"/>", stringId, escapedValue);
                    }
                }
                manifest.WriteLine("    </keywords>");
            }

            manifest.WriteLine("    <tasks>");
            foreach (var taskValue in tasks.Keys)
            {
                var task = tasks[taskValue];
                string escapedTaskName = XmlUtilities.XmlEscape(task.Name);
                string taskStringId = XmlUtilities.XmlEscape($"task_{task.Name}");
                manifest.WriteLine("     <task name=\"{0}\" message=\"$(string.{1})\" value=\"{2}\"{3}>", escapedTaskName, taskStringId, taskValue,
                    task.Opcodes == null ? "/" : "");
                if (emittedStringIds.Add(taskStringId))
                {
                    localizedStrings.WriteLine("    <string id=\"{0}\" value=\"{1}\"/>", taskStringId, escapedTaskName);
                }
                if (task.Opcodes != null)
                {
                    manifest.WriteLine(">");
                    manifest.WriteLine("      <opcodes>");
                    foreach (var keyValue in task.Opcodes)
                    {
                        string escapedOpcodeName = XmlUtilities.XmlEscape(keyValue.Value);
                        string opcodeStringId = XmlUtilities.XmlEscape($"opcode_{task.Name}{keyValue.Value}");
                        manifest.WriteLine("       <opcode name=\"{0}\" message=\"$(string.{1})\" value=\"{2}\"/>",
                            escapedOpcodeName, opcodeStringId, keyValue.Key);
                        if (emittedStringIds.Add(opcodeStringId))
                        {
                            localizedStrings.WriteLine("    <string id=\"{0}\" value=\"{1}\"/>", opcodeStringId, escapedOpcodeName);
                        }
                    }
                    manifest.WriteLine("      </opcodes>");
                    manifest.WriteLine("     </task>");
                }
            }
            manifest.WriteLine("    </tasks>");

            if (enumIntern.Count > 0)
            {
                manifest.WriteLine("    <maps>");
                foreach (var map in enumIntern.Values)
                {
                    manifest.Write(map);
                }

                manifest.WriteLine("    </maps>");
                localizedStrings.Write(enumLocalizations.ToString());
            }

            manifest.WriteLine("    <events>");
            foreach (StringWriter eventStr in events.Values)
            {
                manifest.Write(eventStr.ToString());
            }

            manifest.WriteLine("    </events>");

            manifest.WriteLine("    <templates>");
            foreach (var keyValue in templateIntern)
            {
                manifest.WriteLine("     <template tid=\"{0}\">", keyValue.Value);
                manifest.Write(keyValue.Key);
                manifest.WriteLine("     </template>");
            }
            manifest.WriteLine("    </templates>");
            manifest.WriteLine("   </provider>");
            manifest.WriteLine("  </events>");
            manifest.WriteLine(" </instrumentation>");
            string strings = localizedStrings.ToString();
            if (strings.Length > 0)
            {
                manifest.WriteLine(" <localization>");
                manifest.WriteLine("  <resources culture=\"{0}\">", IetfLanguageTag(CultureInfo.CurrentCulture));
                manifest.WriteLine("   <stringTable>");
                manifest.Write(strings);
                manifest.WriteLine("   </stringTable>");
                manifest.WriteLine("  </resources>");
                manifest.WriteLine(" </localization>");
            }

            manifest.WriteLine("</instrumentationManifest>");
            return manifest.ToString();
        }

        #endregion

        #region Private copies of helper methods (duplicated from RegisteredTraceEventParser)

        private class LegacyTaskInfo
        {
            public string Name;
            public SortedDictionary<int, string> Opcodes;
        }

        private static string MakeLegalIdentifier(string name)
        {
            name = name.Replace(" ", "");
            name = name.Replace("-", "_");
            return name;
        }

        private static string Capitalize(string str)
        {
            if (str.Length == 0)
            {
                return str;
            }

            char c = str[0];
            if (Char.IsUpper(c))
            {
                return str;
            }

            return (str.Substring(1).ToUpper() + str.Substring(1));
        }

        private static string GetKeywordStr(SortedDictionary<ulong, string> keywords, ulong keywordSet)
        {
            var ret = "";
            for (int i = 0; i < 48; i++)
            {
                ulong keyword = 1UL << i;
                if ((keyword & keywordSet) != 0)
                {
                    string keywordStr;
                    if (!keywords.TryGetValue(keyword, out keywordStr))
                    {
                        keywordStr = "keyword_" + keyword.ToString("x");
                        keywords[keyword] = keywordStr;
                    }
                    if (ret.Length != 0)
                    {
                        ret += " ";
                    }

                    ret += keywordStr;
                }
            }
            return ret;
        }

        private static string IetfLanguageTag(CultureInfo culture)
        {
            switch (culture.Name)
            {
                case "zh-CHT":
                    return "zh-Hant";
                case "zh-CHS":
                    return "zh-Hans";
                default:
                    return culture.Name;
            }
        }

        #endregion

        #region XML Normalization Helpers

        /// <summary>
        /// Normalize an XML document by removing non-significant text nodes and sorting attributes.
        /// </summary>
        private void NormalizeXml(XmlDocument doc)
        {
            RemoveNonSignificantTextNodes(doc.DocumentElement);
            SortAttributes(doc.DocumentElement);
        }

        /// <summary>
        /// Removes whitespace-only text nodes and stray text content (like the legacy ">" formatting quirk)
        /// from structural elements that should only contain child elements.
        /// </summary>
        private void RemoveNonSignificantTextNodes(XmlNode node)
        {
            if (node == null) return;

            var nodesToRemove = new List<XmlNode>();
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Text || child.NodeType == XmlNodeType.Whitespace)
                {
                    // Keep text nodes that are the only child (meaningful content).
                    // Remove text nodes in elements that also have child elements (formatting artifacts).
                    bool hasElementSiblings = false;
                    foreach (XmlNode sibling in node.ChildNodes)
                    {
                        if (sibling.NodeType == XmlNodeType.Element)
                        {
                            hasElementSiblings = true;
                            break;
                        }
                    }

                    if (hasElementSiblings || string.IsNullOrWhiteSpace(child.Value))
                    {
                        nodesToRemove.Add(child);
                    }
                }
                else
                {
                    RemoveNonSignificantTextNodes(child);
                }
            }

            foreach (var nodeToRemove in nodesToRemove)
            {
                node.RemoveChild(nodeToRemove);
            }
        }

        private void SortAttributes(XmlNode node)
        {
            if (node == null) return;

            if (node.Attributes != null && node.Attributes.Count > 0)
            {
                var attributes = new List<XmlAttribute>();
                foreach (XmlAttribute attr in node.Attributes)
                {
                    attributes.Add(attr);
                }

                attributes.Sort((a, b) =>
                {
                    int nsCompare = string.Compare(a.NamespaceURI, b.NamespaceURI, StringComparison.Ordinal);
                    if (nsCompare != 0) return nsCompare;
                    return string.Compare(a.LocalName, b.LocalName, StringComparison.Ordinal);
                });

                node.Attributes.RemoveAll();
                foreach (var attr in attributes)
                {
                    node.Attributes.Append(attr);
                }
            }

            foreach (XmlNode child in node.ChildNodes)
            {
                SortAttributes(child);
            }
        }

        #endregion
    }
}
