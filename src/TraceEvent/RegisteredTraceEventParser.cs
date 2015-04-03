//     Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.RegularExpressions;
using FastSerialization;
using System.Diagnostics.Eventing;
using Microsoft.Diagnostics.Tracing.Session;
using System.IO;
using System.Text;
using System.Threading;

namespace Microsoft.Diagnostics.Tracing.Parsers
{
    /// <summary>
    /// RegisteredTraceEventParser uses the standard windows provider database (TDH, what gets registered with wevtutil)
    /// to find the names of events and fields of the events).   
    /// </summary>
    public unsafe sealed class RegisteredTraceEventParser : ExternalTraceEventParser
    {
        /// <summary>
        /// Create a new RegisteredTraceEventParser and attach it to the given TraceEventSource
        /// </summary>
        public RegisteredTraceEventParser(TraceEventSource source)
            : base(source) { }

        /// <summary>
        /// Given a provider name that has been registered with the operating system, get
        /// a string representing the ETW manifest for that provider.    Note that this
        /// manifest is not as rich as the original source manifest because some information
        /// is not actually compiled into the binary manifest that is registered with the OS.  
        /// </summary>
        public static string GetManifestForRegisteredProvider(string providerName)
        {
            var providerGuid = TraceEventProviders.GetProviderGuidByName(providerName);
            if (providerGuid == Guid.Empty)
                throw new ApplicationException("Could not find provider with name " + providerName);
            return GetManifestForRegisteredProvider(providerGuid);
        }
        /// <summary>
        /// Given a provider GUID that has been registered with the operating system, get
        /// a string representing the ETW manifest for that provider.    Note that this
        /// manifest is not as rich as the original source manifest because some information
        /// is not actually compiled into the binary manifest that is registered with the OS.  
        /// </summary>
        public static string GetManifestForRegisteredProvider(Guid providerGuid)
        {
#if !PUBLIC_ONLY || PERFVIEW
            int buffSize = 84000;       // Still in the small object heap.  
            byte* buffer = (byte*)System.Runtime.InteropServices.Marshal.AllocHGlobal(buffSize);
            byte* enumBuffer = null;

            TraceEventNativeMethods.EVENT_RECORD eventRecord = new TraceEventNativeMethods.EVENT_RECORD();
            eventRecord.EventHeader.ProviderId = providerGuid;

            // We keep events of a given event number together in the output
            string providerName = null;
            SortedDictionary<int, StringWriter> events = new SortedDictionary<int, StringWriter>();
            // We keep tasks separated by task ID
            SortedDictionary<int, TaskInfo> tasks = new SortedDictionary<int, TaskInfo>();
            // Templates where the KEY is the template string and the VALUE is the template name (backwards)  
            Dictionary<string, string> templateIntern = new Dictionary<string, string>(8);

            // Remember any enum types we have.   Value is XML for the enum (normal) 
            Dictionary<string, string> enumIntern = new Dictionary<string, string>();
            StringWriter enumLocalizations = new StringWriter();

            // Any task names used so far 
            Dictionary<string, int> taskNames = new Dictionary<string, int>();
            // Any  es used so far 
            Dictionary<string, int> opcodeNames = new Dictionary<string, int>();
            // This insures that we have unique event names. 
            Dictionary<string, int> eventNames = new Dictionary<string, int>();

            SortedDictionary<ulong, string> keywords = new SortedDictionary<ulong, string>();
            List<ProviderDataItem> keywordsItems = TraceEventProviders.GetProviderKeywords(providerGuid);
            if (keywordsItems != null)
            {
                foreach (var keywordItem in keywordsItems)
                {
                    // Skip the reserved keywords.   
                    if (keywordItem.Value >= 1000000000000UL)
                        continue;

                    keywords[keywordItem.Value] = MakeLegalIdentifier(keywordItem.Name);
                }
            }

            for (byte ver = 0; ver <= 255; ver++)
            {
                eventRecord.EventHeader.Version = ver;
                int count;
                int status;
                for (; ; )
                {
                    int dummy;
                    status = TdhGetAllEventsInformation(&eventRecord, IntPtr.Zero, out dummy, out count, buffer, ref buffSize);
                    if (status != 122 || 20000000 < buffSize) // 122 == Insufficient buffer keep it under 2Meg
                        break;
                    Marshal.FreeHGlobal((IntPtr)buffer);
                    buffer = (byte*)Marshal.AllocHGlobal(buffSize);
                }

                // TODO FIX NOW deal with too small of a buffer.  
                if (status == 0)
                {
                    TRACE_EVENT_INFO** eventInfos = (TRACE_EVENT_INFO**)buffer;
                    for (int i = 0; i < count; i++)
                    {
                        TRACE_EVENT_INFO* eventInfo = eventInfos[i];
                        byte* eventInfoBuff = (byte*)eventInfo;
                        EVENT_PROPERTY_INFO* propertyInfos = &eventInfo->EventPropertyInfoArray;

                        if (providerName == null)
                        {
                            if (eventInfo->ProviderNameOffset != 0)
                                providerName = new string((char*)(&eventInfoBuff[eventInfo->ProviderNameOffset]));
                            else
                                providerName = "provider(" + eventInfo->ProviderGuid.ToString() + ")";
                        }

                        // Compute task name
                        string taskName = null;
                        if (eventInfo->TaskNameOffset != 0)
                        {
                            taskName = MakeLegalIdentifier((new string((char*)(&eventInfoBuff[eventInfo->TaskNameOffset]))));
                        }
                        if (taskName == null)
                            taskName = "task_" + eventInfo->EventDescriptor.Task.ToString();

                        // Insure task name is unique.  
                        int taskNumForName;
                        if (taskNames.TryGetValue(taskName, out taskNumForName) && taskNumForName != eventInfo->EventDescriptor.Task)
                            taskName = taskName + "_" + eventInfo->EventDescriptor.Task.ToString();
                        taskNames[taskName] = eventInfo->EventDescriptor.Task;

                        // Compute opcode name
                        string opcodeName = "";
                        if (eventInfo->EventDescriptor.Opcode != 0)
                        {
                            if (eventInfo->OpcodeNameOffset != 0)
                                opcodeName = MakeLegalIdentifier((new string((char*)(&eventInfoBuff[eventInfo->OpcodeNameOffset]))));
                            else
                                opcodeName = "opcode_" + eventInfo->EventDescriptor.Opcode.ToString();
                        }

                        // Insure opcode name is unique.  
                        int opcodeNumForName;
                        if (opcodeNames.TryGetValue(opcodeName, out opcodeNumForName) && opcodeNumForName != eventInfo->EventDescriptor.Opcode)
                        {
                            // If we did not find a name, use 'opcode and the disambiguator
                            if (eventInfo->OpcodeNameOffset == 0)
                                opcodeName = "opcode";
                            opcodeName = opcodeName + "_" + eventInfo->EventDescriptor.Task.ToString() + "_" + eventInfo->EventDescriptor.Opcode.ToString();
                        }
                        opcodeNames[opcodeName] = eventInfo->EventDescriptor.Opcode;

                        // And event name 
                        string eventName = taskName;
                        if (!taskName.EndsWith(opcodeName, StringComparison.OrdinalIgnoreCase))
                            eventName += Capitalize(opcodeName);

                        // Insure uniqueness of the event name
                        int eventNumForName;
                        if (eventNames.TryGetValue(eventName, out eventNumForName) && eventNumForName != eventInfo->EventDescriptor.EventId)
                            eventName = eventName + eventInfo->EventDescriptor.EventId.ToString();
                        eventNames[eventName] = eventInfo->EventDescriptor.EventId;

                        // Get task information
                        TaskInfo taskInfo;
                        if (!tasks.TryGetValue(eventInfo->EventDescriptor.Task, out taskInfo))
                            tasks[eventInfo->EventDescriptor.Task] = taskInfo = new TaskInfo() { Name = taskName };

                        var symbolName = eventName;
                        if (eventInfo->EventDescriptor.Version > 0)
                            symbolName += "_V" + eventInfo->EventDescriptor.Version;

                        StringWriter eventWriter;
                        if (!events.TryGetValue(eventInfo->EventDescriptor.EventId, out eventWriter))
                            events[eventInfo->EventDescriptor.EventId] = eventWriter = new StringWriter();

                        eventWriter.Write("     <event value=\"{0}\" symbol=\"{1}\" version=\"{2}\" task=\"{3}\"",
                            eventInfo->EventDescriptor.EventId,
                            symbolName,
                            eventInfo->EventDescriptor.Version,
                            taskName);
                        if (eventInfo->EventDescriptor.Opcode != 0)
                        {
                            string opcodeId;
                            if (eventInfo->EventDescriptor.Opcode < 10)       // It is a reserved opcode.  
                                opcodeId = "win:" + opcodeName;
                            else
                            {
                                opcodeId = opcodeName;
                                if (taskInfo.Opcodes == null)
                                    taskInfo.Opcodes = new SortedDictionary<int, string>();

                                if (!taskInfo.Opcodes.ContainsKey(eventInfo->EventDescriptor.Opcode))
                                    taskInfo.Opcodes[eventInfo->EventDescriptor.Opcode] = opcodeId;
                            }
                            eventWriter.Write(" opcode=\"{0}\"", opcodeId);
                        }
                        // TODO handle cases outside standard levels 
                        if ((int)TraceEventLevel.Always <= eventInfo->EventDescriptor.Level && eventInfo->EventDescriptor.Level <= (int)TraceEventLevel.Verbose)
                        {
                            var asLevel = (TraceEventLevel)eventInfo->EventDescriptor.Level;
                            var levelName = "win:" + asLevel;
                            eventWriter.Write(" level=\"{0}\"", levelName);
                        }

                        var keywordStr = GetKeywordStr(keywords, (ulong)eventInfo->EventDescriptor.Keywords);
                        if (keywordStr.Length > 0)
                            eventWriter.Write(" keywords=\"" + keywordStr + "\"", eventInfo->EventDescriptor.Keywords);

                        if (eventInfo->TopLevelPropertyCount != 0)
                        {
                            var templateWriter = new StringWriter();
                            string[] propertyNames = new string[eventInfo->TopLevelPropertyCount];
                            for (int j = 0; j < eventInfo->TopLevelPropertyCount; j++)
                            {
                                EVENT_PROPERTY_INFO* propertyInfo = &propertyInfos[j];
                                var propertyName = new string((char*)(&eventInfoBuff[propertyInfo->NameOffset]));
                                propertyNames[j] = propertyName;
                                var enumAttrib = "";

                                // Deal with any maps (bit fields or enumerations)
                                if (propertyInfo->MapNameOffset != 0)
                                {
                                    string mapName = new string((char*)(&eventInfoBuff[propertyInfo->MapNameOffset]));

                                    if (enumBuffer == null)
                                        enumBuffer = (byte*)System.Runtime.InteropServices.Marshal.AllocHGlobal(buffSize);

                                    if (!enumIntern.ContainsKey(mapName))
                                    {
                                        EVENT_MAP_INFO* enumInfo = (EVENT_MAP_INFO*)enumBuffer;
                                        var hr = TdhGetEventMapInformation(&eventRecord, mapName, enumInfo, ref buffSize);
                                        if (hr == 0)
                                        {
                                            // We only support manifest enums for now.  
                                            if (enumInfo->Flag == MAP_FLAGS.EVENTMAP_INFO_FLAG_MANIFEST_VALUEMAP ||
                                                enumInfo->Flag == MAP_FLAGS.EVENTMAP_INFO_FLAG_MANIFEST_BITMAP)
                                            {
                                                StringWriter enumWriter = new StringWriter();
                                                string enumName = new string((char*)(&enumBuffer[enumInfo->NameOffset]));
                                                enumAttrib = " map=\"" + enumName + "\"";
                                                if (enumInfo->Flag == MAP_FLAGS.EVENTMAP_INFO_FLAG_MANIFEST_VALUEMAP)
                                                    enumWriter.WriteLine("     <valueMap name=\"{0}\">", enumName);
                                                else
                                                    enumWriter.WriteLine("     <bitMap name=\"{0}\">", enumName);

                                                EVENT_MAP_ENTRY* mapEntries = &enumInfo->MapEntryArray;
                                                for (int k = 0; k < enumInfo->EntryCount; k++)
                                                {
                                                    int value = mapEntries[k].Value;
                                                    string valueName = new string((char*)(&enumBuffer[mapEntries[k].NameOffset])).Trim();
                                                    enumWriter.WriteLine("      <map value=\"0x{0:x}\" message=\"$(string.map_{1}{2})\"/>", value, enumName, valueName);
                                                    enumLocalizations.WriteLine("    <string id=\"map_{0}{1}\" value=\"{2}\"/>", enumName, valueName, valueName);
                                                }
                                                if (enumInfo->Flag == MAP_FLAGS.EVENTMAP_INFO_FLAG_MANIFEST_VALUEMAP)
                                                    enumWriter.WriteLine("     </valueMap>", enumName);
                                                else
                                                    enumWriter.WriteLine("     </bitMap>", enumName);
                                                enumIntern[mapName] = enumWriter.ToString();
                                            }
                                        }
                                    }
                                }

                                // Remove anything that does not look like an ID (.e.g space)
                                propertyName = Regex.Replace(propertyName, "[^A-Za-z0-9_]", "");
                                TdhInputType propertyType = propertyInfo->InType;
                                string countOrLengthAttrib = "";

                                if ((propertyInfo->Flags & PROPERTY_FLAGS.ParamCount) != 0)
                                    countOrLengthAttrib = " count=\"" + propertyNames[propertyInfo->CountOrCountIndex] + "\"";
                                else if ((propertyInfo->Flags & PROPERTY_FLAGS.ParamLength) != 0)
                                    countOrLengthAttrib = " length=\"" + propertyNames[propertyInfo->LengthOrLengthIndex] + "\"";

                                templateWriter.WriteLine("      <data name=\"{0}\" inType=\"win:{1}\"{2}{3}/>", propertyName, propertyType.ToString(), enumAttrib, countOrLengthAttrib);
                            }
                            var templateStr = templateWriter.ToString();

                            // See if this template already exists, and if not make it 
                            string templateName;
                            if (!templateIntern.TryGetValue(templateStr, out templateName))
                            {
                                templateName = eventName + "Args";
                                if (eventInfo->EventDescriptor.Version > 0)
                                    templateName += "_V" + eventInfo->EventDescriptor.Version;
                                templateIntern[templateStr] = templateName;
                            }
                            eventWriter.Write(" template=\"{0}\"", templateName);
                        }
                        eventWriter.WriteLine("/>");
                    }
                }
                else if (status == 1168 && ver != 0)        // Not Found give up
                    break;
            }
            if (enumBuffer != null)
                System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)enumBuffer);

            System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)buffer);
            if (providerName == null)
                throw new ApplicationException("Could not find provider with at GUID of " + providerGuid.ToString());

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
                    manifest.WriteLine("     <keyword name=\"{0}\" message=\"$(string.keyword_{1})\" mask=\"0x{2:x}\"/>",
                        keyValue.Value, keyValue.Value, keyValue.Key);
                    localizedStrings.WriteLine("    <string id=\"keyword_{0}\" value=\"{1}\"/>", keyValue.Value, keyValue.Value);
                }
                manifest.WriteLine("    </keywords>");
            }

            manifest.WriteLine("    <tasks>");
            foreach (var taskValue in tasks.Keys)
            {
                var task = tasks[taskValue];
                manifest.WriteLine("     <task name=\"{0}\" message=\"$(string.task_{1})\" value=\"{2}\"{3}>", task.Name, task.Name, taskValue,
                    task.Opcodes == null ? "/" : "");       // If no opcodes, terminate immediately.  
                localizedStrings.WriteLine("    <string id=\"task_{0}\" value=\"{1}\"/>", task.Name, task.Name);
                if (task.Opcodes != null)
                {
                    manifest.WriteLine(">");
                    manifest.WriteLine("      <opcodes>");
                    foreach (var keyValue in task.Opcodes)
                    {
                        manifest.WriteLine("       <opcode name=\"{0}\" message=\"$(string.opcode_{1}{2})\" value=\"{3}\"/>",
                            keyValue.Value, task.Name, keyValue.Value, keyValue.Key);
                        localizedStrings.WriteLine("    <string id=\"opcode_{0}{1}\" value=\"{2}\"/>", task.Name, keyValue.Value, keyValue.Value);
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
                    manifest.Write(map);
                manifest.WriteLine("    </maps>");
                localizedStrings.Write(enumLocalizations.ToString());
            }

            manifest.WriteLine("    <events>");
            foreach (StringWriter eventStr in events.Values)
                manifest.Write(eventStr.ToString());
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
                manifest.WriteLine("  <resources culture=\"{0}\">", Thread.CurrentThread.CurrentCulture.IetfLanguageTag);
                manifest.WriteLine("   <stringTable>");
                manifest.Write(strings);
                manifest.WriteLine("   </stringTable>");
                manifest.WriteLine("  </resources>");
                manifest.WriteLine(" </localization>");
            }

            manifest.WriteLine("</instrumentationManifest>");
            return manifest.ToString(); ;
#else
            throw new NotImplementedException("Getting manifest for registered providers not supported on this version.");
#endif
        }

        #region private
        private static string MakeLegalIdentifier(string name)
        {
            // TODO FIX NOW beef this up.
            name = name.Replace(" ", "");
            name = name.Replace("-", "_");
            return name;
        }

        /// <summary>
        /// Generates a space separated list of set of keywords 'keywordSet' using the table 'keywords'
        /// It will generate new keyword names if needed and add them to 'keywords' if they are not present.  
        /// </summary>
        private static string GetKeywordStr(SortedDictionary<ulong, string> keywords, ulong keywordSet)
        {
            var ret = "";
            // TODO FIX NOW  what should we be doing here?   I do want pass along channel information
            // We skip the reserved keywords (48 and above)
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
                        ret += " ";
                    ret += keywordStr;
                }
            }
            return ret;
        }

        /// <summary>
        /// Class used to accumulate information about Tasks in the implementation of GetManifestForRegisteredProvider
        /// </summary>
        private class TaskInfo
        {
            public string Name;
            public SortedDictionary<int, string> Opcodes;
        }

        private static string Capitalize(string str)
        {
            if (str.Length == 0)
                return str;
            char c = str[0];
            if (Char.IsUpper(c))
                return str;
            return (str.Substring(1).ToUpper() + str.Substring(1));
        }

        internal override DynamicTraceEventData TryLookup(TraceEvent unknownEvent)
        {
            // Is this a TraceLogging style 
            DynamicTraceEventData ret = null;
            if (unknownEvent.Channel == TraceLoggingMarker)
            {
                bool hasETWEventInformation = false;
                for (int i = 0; i != unknownEvent.eventRecord->ExtendedDataCount; i++)
                {
                    var extType = unknownEvent.eventRecord->ExtendedData[i].ExtType;
                    if (extType == TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_EVENT_SCHEMA_TDH ||
                        extType == TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_EVENT_SCHEMA_TL)
                    {
                        hasETWEventInformation = true;
                        break;
                    }
                }

                if (!hasETWEventInformation)
                {
                    ret = CheckForTraceLoggingEventDefinition(unknownEvent);
                    if (ret != null)
                        return ret;
                }
            }

            // TODO react if 4K is not big enough, cache the buffer?, handle more types, handle structs...
            int buffSize = 4096;
            byte* buffer = (byte*)System.Runtime.InteropServices.Marshal.AllocHGlobal(buffSize);
            int status = TdhGetEventInformation(unknownEvent.eventRecord, 0, null, buffer, &buffSize);
            if (status == 0)
                ret = (new TdhEventParser(buffer)).ParseEventMetaData();

            System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)buffer);
            return ret;
        }

        /*************************** TraceLogging format Support ********************************/
        /// <summary>
        /// Events that have this special special marker in their channel indicate that the 
        /// data format is the new self-describing TraceLogging style 
        /// </summary>
        internal const TraceEventChannel TraceLoggingMarker = (TraceEventChannel)11;

        /// <summary>
        /// Given a TraceLogging event 'data' (which has Channel == 11), then parse it adding the
        /// definition to lookup logic if necessary.   Returns true if a new definition was added
        /// (which means you need to retry lookup).  
        /// </summary>
        private unsafe DynamicTraceEventData CheckForTraceLoggingEventDefinition(TraceEvent data)
        {
            Debug.Assert(data.Channel == TraceLoggingMarker);

            // Format for TraceLogging MetaData
            //
            // ProviderBlob
            //      ushort TotalProviderBlobLength;     // includes this short, null termination, and provider traits
            //      string UTF8NullTerminatedProviderName
            //      provider traits (can be ignored).
            // EventPayloadNameBlob
            //      ushort TotalEventBlobLength         // includes this short and all type info.  
            //      byte   Tags[N];                     // N varies, 1 or more. Top bit of each byte is ChainFlag. Stop reading when you hit a byte with ChainFlag unset.
            //      string UTF8NullTerminatedEventName
            //                                          // The following are repeated until you reach TotalEventBlobLength.
            //      string UTF8NullTerminatedFieldName
            //      byte   InType;                      // bits 0-4 = intype, bits 5-6 == CountFlags, bit 7 == ChainFlag
            //      byte   OutType                      // Only present if InType.ChainFlag. bits 0-6 = outtype, bit 7 = ChainFlag
            //      byte   Tags[N];                     // Only present if OutType.ChainFlag. N varies, 1 or more. Top bit of each byte is ChainFlag. Stop reading when you hit a byte with ChainFlag unset.
            //      ushort Count                        // Only present if CountFlag & FixedCountFlag.
            //      byte   Custom[Count];               // Only present if CountFlag == CustomCountFlag.

            int offset = data.GetInt16At(0);
            if (offset < 6 || data.EventDataLength < offset)
            {
                Trace.WriteLine("Error: TraceLogging header has illegal Offset " + offset + " for data len " + data.EventDataLength);
                return null;
            }
            string providerName = data.GetUTF8StringAt(2);

            int eventMetaDataEnd = offset + data.GetInt16At(offset); offset += 2;

            // Ignore event tags (read until we find a byte with high bit unset)
            do
            {
                offset++;
            }
            while (0 != (data.GetByteAt(offset - 1) & 0x80));

            string eventName = data.GetUTF8StringAt(offset); offset = data.SkipUTF8String(offset);

            var event_ = new DynamicTraceEventData(null, (int)data.ID, 0, eventName, Guid.Empty, 0, "", data.ProviderGuid, providerName);
            Trace.WriteLine("Got TraceLogging Provider " + providerName + " Event " + eventName);

            TraceLoggingFieldParser fieldParser = new TraceLoggingFieldParser(data, offset, eventMetaDataEnd);
            fieldParser.ParseFields(out event_.payloadNames, out event_.payloadFetches, (ushort)eventMetaDataEnd);

            return event_;
        }

        /// <summary>
        /// A helper class that knows how to parse fields with nested types.  
        /// </summary>
        private class TraceLoggingFieldParser
        {
            public TraceLoggingFieldParser(TraceEvent data, int metaDataStart, int eventMetaDataEnd)
            {
                this.data = data;
                this.offset = metaDataStart;
                this.eventMetaDataEnd = eventMetaDataEnd;
            }

            /// <summary>
            /// Parses at most 'maxFields' fields starting at the current position.  
            /// Will return the parse fields in 'payloadNamesRet' and 'payloadFetchesRet'
            /// Will return true if successful, false means an error occurred.  
            /// </summary>
            public bool ParseFields(out string[] payloadNamesRet, out DynamicTraceEventData.PayloadFetch[] payloadFetchesRet, ushort fieldOffset, int maxFields = int.MaxValue)
            {
                var payloadFetches = new List<DynamicTraceEventData.PayloadFetch>();
                var payloadNames = new List<string>();

                while (offset < eventMetaDataEnd)
                {
                    if (maxFields <= payloadFetches.Count)
                        break;

                    // Parse field name
                    string fieldName = data.GetUTF8StringAt(offset); offset = data.SkipUTF8String(offset);

                    int outType = 0;
                    int inTypeRaw = data.GetByteAt(offset); offset++;
                    int countFlags = inTypeRaw & InTypeCountMask;

                    if ((inTypeRaw & InTypeChainFlag) != 0)
                    {
                        outType = data.GetByteAt(offset); offset++;

                        // Skip tags, if present.
                        if ((outType & OutTypeChainFlag) != 0)
                        {
                            do
                            {
                                offset++;
                            }
                            while ((data.GetByteAt(offset - 1) & 0x80) != 0);
                        }

                        outType &= OutTypeTypeMask;
                    }

                    TdhInputType inType = (TdhInputType)(inTypeRaw & InTypeTypeMask);
                    ushort fixedCount = 0;
                    if ((countFlags & InTypeFixedCountFlag) != 0)
                    {
                        fixedCount = (ushort)data.GetInt16At(offset); offset += 2;
                        if (countFlags == InTypeCustomCountFlag)
                            offset += fixedCount;

                        if (inTypeRaw == 0 && countFlags == InTypeFixedCountFlag)
                        {
                            // Obsolete encoding for struct. Translate into new encoding.
                            inType = TdhInputType.Struct;
                            outType = fixedCount;
                            countFlags = 0;
                        }
                    }

                    DynamicTraceEventData.PayloadFetch payloadFetch;
                    if (inType == TdhInputType.Struct)
                    {
                        int numStructFields = outType;
                        Trace.WriteLine("   " + fieldName + " Is a nested type with " + numStructFields + " fields");
                        var classInfo = new DynamicTraceEventData.PayloadFetchClassInfo();
                        if (!ParseFields(out classInfo.FieldNames, out classInfo.FieldFetches, 0, numStructFields))
                            goto Fail;
                        payloadFetch = DynamicTraceEventData.PayloadFetch.StructPayloadFetch(fieldOffset, classInfo);
                    }
                    else
                    {
                        payloadFetch = new DynamicTraceEventData.PayloadFetch(fieldOffset, inType, outType);
                        if (payloadFetch.Size == DynamicTraceEventData.UNKNOWN_SIZE)
                        {
                            Trace.WriteLine("    Unknown type for  " + fieldName + " " + inType.ToString() + " fields from here will be missing.");
                            goto Fail;
                        }
                    }

                    // Is it an array? 
                    if (countFlags != 0)
                    {
                        payloadFetch = DynamicTraceEventData.PayloadFetch.ArrayPayloadFetch(fieldOffset, payloadFetch, fixedCount);
                        payloadFetch.Size = DynamicTraceEventData.SIZE16_PREFIX;    // It is not an explicit field beforehand, but a prefix. 
                    }

                    var size = payloadFetch.Size;
                    Debug.Assert(0 < size);
                    Trace.WriteLine("    Got TraceLogging Field " + fieldName + " " + (payloadFetch.Type ?? typeof(void)) + " size " + size.ToString("x") + " offset " + fieldOffset.ToString("x"));
                    payloadNames.Add(fieldName);
                    payloadFetches.Add(payloadFetch);
                    if (fieldOffset != ushort.MaxValue)
                    {
                        if (size < DynamicTraceEventData.SPECIAL_SIZES)
                            fieldOffset += size;
                        else
                            fieldOffset = ushort.MaxValue;
                    }
                }

                payloadNamesRet = payloadNames.ToArray();
                payloadFetchesRet = payloadFetches.ToArray();
                return true;

            Fail:
                payloadNamesRet = new string[0];
                payloadFetchesRet = new DynamicTraceEventData.PayloadFetch[0];
                return false;
            }

            #region private

            // TODO we may not need all of these.  
            internal const byte InTypeTypeMask = 31;
            internal const byte InTypeFixedCountFlag = 32;
            internal const byte InTypeVariableCountFlag = 64;
            internal const byte InTypeCustomCountFlag = 96;
            internal const byte InTypeCountMask = 96;
            internal const byte InTypeChainFlag = 128;

            internal const byte OutTypeTypeMask = 127;
            internal const byte OutTypeChainFlag = 128;

            TraceEvent data;
            int offset;
            int eventMetaDataEnd;
            #endregion // private
        }


        /*************************** End TraceLogging format Support *****************************/

        /// <summary>
        /// TdhEvenParser takes the Trace Diagnostics Helper (TDH) TRACE_EVENT_INFO structure and
        /// (passed as a byte*) and converts it to a DynamicTraceEventData which which 
        /// can be used to parse events of that type.   You first create TdhEventParser and then
        /// call ParseEventMetaData to do the parsing.  
        /// </summary>
        internal class TdhEventParser
        {
            /// <summary>
            /// Creates a new parser from the TRACE_EVENT_INFO held in 'buffer'.  Use
            /// ParseEventMetaData to then parse it into a DynamicTraceEventData structure
            /// </summary>
            /// <param name="eventInfo"></param>
            public TdhEventParser(byte* eventInfo)
            {
                this.buffer = eventInfo;
                this.eventInfo = (TRACE_EVENT_INFO*)eventInfo;
                this.propertyInfos = &this.eventInfo->EventPropertyInfoArray;
            }

            /// <summary>
            /// Actually performs the parsing of the TRACE_EVENT_INFO passed in the constructor
            /// </summary>
            /// <returns></returns>
            public DynamicTraceEventData ParseEventMetaData()
            {
                EVENT_PROPERTY_INFO* propertyInfos = &eventInfo->EventPropertyInfoArray;
                string taskName = null;
                if (eventInfo->TaskNameOffset != 0)
                    taskName = MakeLegalIdentifier(new string((char*)(&buffer[eventInfo->TaskNameOffset])));

                string opcodeName = null;
                if (eventInfo->OpcodeNameOffset != 0)
                {
                    opcodeName = new string((char*)(&buffer[eventInfo->OpcodeNameOffset]));
                    if (opcodeName.StartsWith("win:"))
                        opcodeName = opcodeName.Substring(4);
                    opcodeName = MakeLegalIdentifier(opcodeName);
                }

                string providerName = "UnknownProvider";
                if (eventInfo->ProviderNameOffset != 0)
                    providerName = new string((char*)(&buffer[eventInfo->ProviderNameOffset]));

                var eventID = eventInfo->EventDescriptor.EventId;
                // Mark it as a classic event if necessary. 
                if (eventInfo->DecodingSource == 1) // means it is from MOF (Classic)
                    eventID = (int)TraceEventID.Illegal;

                var newTemplate = new DynamicTraceEventData(null, eventID,
                    eventInfo->EventDescriptor.Task, taskName,
                    eventInfo->EventGuid,
                    eventInfo->EventDescriptor.Opcode, opcodeName,
                    eventInfo->ProviderGuid, providerName);

                if (eventID == (int)TraceEventID.Illegal)
                    newTemplate.lookupAsClassic = true;

                Trace.WriteLine("In TdhEventParser for event" + providerName + "/" + taskName + "/" + opcodeName + " with " + eventInfo->TopLevelPropertyCount + " fields");
                DynamicTraceEventData.PayloadFetchClassInfo fields = ParseFields(0, 0, eventInfo->TopLevelPropertyCount);
                newTemplate.payloadNames = fields.FieldNames;
                newTemplate.payloadFetches = fields.FieldFetches;

                return newTemplate;      // return this as the event template for this lookup. 
            }

            /// <summary>
            /// Parses at most 'maxFields' fields starting at the current position.  
            /// Will return the parse fields in 'payloadNamesRet' and 'payloadFetchesRet'
            /// Will return true if successful, false means an error occurred.  
            /// </summary>
            private DynamicTraceEventData.PayloadFetchClassInfo ParseFields(ushort fieldOffset, int startField, int numFields)
            {
                var ret = new DynamicTraceEventData.PayloadFetchClassInfo();
                ret.FieldNames = new string[numFields];
                ret.FieldFetches = new DynamicTraceEventData.PayloadFetch[numFields];

                int curField = 0;   // Needs to be outside the scope of the for
                for (; curField < numFields; curField++)
                {
                    var propertyInfo = &propertyInfos[curField + startField];
                    var propertyName = new string((char*)(&buffer[propertyInfo->NameOffset]));
                    // Remove anything that does not look like an ID (.e.g space)
                    ret.FieldNames[curField] = Regex.Replace(propertyName, "[^A-Za-z0-9_]", "");

                    // If it is an array, the field offset starts over at 0.  (since each element has a different offset from the beginning)
                    var arrayFieldOffset = fieldOffset;
                    if ((propertyInfo->Flags & PROPERTY_FLAGS.ParamCount) != 0)
                        fieldOffset = 0;

                    // Is this a nested struct?
                    if ((propertyInfo->Flags & PROPERTY_FLAGS.Struct) != 0)
                    {
                        int numStructFields = propertyInfo->NumOfStructMembers;
                        Trace.WriteLine("   " + propertyName + " Is a nested type with " + numStructFields + " fields {");
                        DynamicTraceEventData.PayloadFetchClassInfo classInfo = ParseFields(fieldOffset, propertyInfo->StructStartIndex, numStructFields);
                        if (classInfo == null)
                        {
                            Trace.WriteLine("    Failure parsing nested struct.");
                            goto Fail;
                        }
                        Trace.WriteLine(" } " + propertyName + " Nested struct completes.");
                        ret.FieldFetches[curField] = DynamicTraceEventData.PayloadFetch.StructPayloadFetch(fieldOffset, classInfo);
                    }
                    else // A normal type
                    {
                        ret.FieldFetches[curField] = new DynamicTraceEventData.PayloadFetch(fieldOffset, propertyInfo->InType, propertyInfo->OutType);
                        if (ret.FieldFetches[curField].Size == DynamicTraceEventData.UNKNOWN_SIZE)
                        {
                            Trace.WriteLine("    Unknown type for  " + propertyName + " " + propertyInfo->InType.ToString() + " fields from here will be missing.");
                            goto Fail;
                        }
                        // is this dynamically sized with another field specifying the length?
                        if ((propertyInfo->Flags & PROPERTY_FLAGS.ParamLength) != 0)
                        {
                            if (propertyInfo->LengthOrLengthIndex == curField - 1)
                            {
                                if (propertyInfos[curField - 1].LengthOrLengthIndex == 4)
                                    ret.FieldFetches[curField].Size = DynamicTraceEventData.SIZE32_PRECEEDS;
                                else if (propertyInfos[curField - 1].LengthOrLengthIndex == 2)
                                    ret.FieldFetches[curField].Size = DynamicTraceEventData.SIZE16_PRECEEDS;
                                else
                                {
                                    Trace.WriteLine("WARNING: Unexpected dynamic length, giving up");
                                    goto Fail;
                                }
                            }
                            if (ret.FieldFetches[curField].Size != DynamicTraceEventData.UNKNOWN_SIZE && propertyInfo->InType == TdhInputType.AnsiString)
                                ret.FieldFetches[curField].Size |= DynamicTraceEventData.IS_ANSI;
                        }
                    }

                    // Is it an array? 
                    if ((propertyInfo->Flags & PROPERTY_FLAGS.ParamCount) != 0)
                    {
                        ushort fixedCount = 0;
                        if ((propertyInfo->Flags & PROPERTY_FLAGS.ParamFixedLength) != 0)
                        {
                            fixedCount = propertyInfo->CountOrCountIndex;
                        }
                        else if (!(propertyInfo->CountOrCountIndex == startField + curField - 1 && propertyInfos[propertyInfo->CountOrCountIndex].InType == TdhInputType.UInt16))
                        {
                            Trace.WriteLine("    Error: Array is variable sized and does not follow  prefix convention.");
                            goto Fail;
                        }
                        Trace.WriteLine("    Field is an array of size " + fixedCount + " at offset " + arrayFieldOffset.ToString("x") + " where 0 means variable sized.");
                        ret.FieldFetches[curField] = DynamicTraceEventData.PayloadFetch.ArrayPayloadFetch(arrayFieldOffset, ret.FieldFetches[curField], fixedCount);
                        fieldOffset = ushort.MaxValue;           // Indicate that the offset must be computed at run time. 
                    }

                    var size = ret.FieldFetches[curField].Size;
                    Trace.WriteLine("    Got TraceLogging Field " + propertyName + " " + (ret.FieldFetches[curField].Type ?? typeof(void)) + " size " + size.ToString("x") + " offset " + fieldOffset.ToString("x"));

                    Debug.Assert(0 < size);
                    if (size >= DynamicTraceEventData.SPECIAL_SIZES)
                        fieldOffset = ushort.MaxValue;           // Indicate that the offset must be computed at run time. 
                    else if (fieldOffset != ushort.MaxValue)
                    {
                        Debug.Assert(fieldOffset + size < ushort.MaxValue);
                        fieldOffset += size;
                    }
                }
                return ret;
            Fail:
                ret.Truncate(curField);
                return ret; ;
            }

            #region private
            TRACE_EVENT_INFO* eventInfo;
            EVENT_PROPERTY_INFO* propertyInfos;
            byte* buffer;                           // points at the eventInfo, but in increments of bytes 
            #endregion // private
        }

        [DllImport("tdh.dll"), SuppressUnmanagedCodeSecurityAttribute]
        internal static extern int TdhGetEventInformation(
            TraceEventNativeMethods.EVENT_RECORD* pEvent,
            uint TdhContextCount,
            void* pTdhContext,
            byte* pBuffer,
            int* pBufferSize);

#if !PUBLIC_ONLY || PERFVIEW
        [DllImport("tdh.dll", CharSet = CharSet.Unicode)]
        internal static extern unsafe int TdhGetAllEventsInformation(
            TraceEventNativeMethods.EVENT_RECORD* pEvent,
            IntPtr mustBeZero,
            out int index,
            out int count,
            byte* pBuffer,
            ref int pBufferSize);
#endif

        [DllImport("tdh.dll", CharSet = CharSet.Unicode), SuppressUnmanagedCodeSecurityAttribute]
        internal static extern int TdhGetEventMapInformation(
            TraceEventNativeMethods.EVENT_RECORD* pEvent,
            string pMapName,
            EVENT_MAP_INFO* info,
            ref int infoSize
        );

        [Flags]
        internal enum MAP_FLAGS
        {
            EVENTMAP_INFO_FLAG_MANIFEST_VALUEMAP = 1,
            EVENTMAP_INFO_FLAG_MANIFEST_BITMAP = 2,
            EVENTMAP_INFO_FLAG_MANIFEST_PATTERNMAP = 4,
            EVENTMAP_INFO_FLAG_WBEM_VALUEMAP = 8,
            EVENTMAP_INFO_FLAG_WBEM_BITMAP = 16,
            EVENTMAP_INFO_FLAG_WBEM_FLAG = 32,
            EVENTMAP_INFO_FLAG_WBEM_NO_MAP = 64
        };

        [StructLayout(LayoutKind.Sequential)]
        internal struct EVENT_MAP_ENTRY
        {
            public int NameOffset;
            public int Value;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct EVENT_MAP_INFO
        {
            public int NameOffset;
            public MAP_FLAGS Flag;
            public int EntryCount;
            public int ValueType;                  // I don't expect patterns, I expect this to be 0 (which means normal enums).  
            public EVENT_MAP_ENTRY MapEntryArray;  // Actually an array, this is the first element.  
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct TRACE_EVENT_INFO
        {
            public Guid ProviderGuid;
            public Guid EventGuid;
            public EventDescriptor EventDescriptor;
            public int DecodingSource;
            public int ProviderNameOffset;
            public int LevelNameOffset;
            public int ChannelNameOffset;
            public int KeywordsNameOffset;
            public int TaskNameOffset;
            public int OpcodeNameOffset;
            public int EventMessageOffset;
            public int ProviderMessageOffset;
            public int BinaryXmlOffset;
            public int BinaryXmlSize;
            public int ActivityIDNameOffset;
            public int RelatedActivityIDNameOffset;
            public int PropertyCount;
            public int TopLevelPropertyCount;
            public int Flags;
            public EVENT_PROPERTY_INFO EventPropertyInfoArray;  // Actually an array, this is the first element.  
        }

        internal struct EVENT_PROPERTY_INFO
        {
            public PROPERTY_FLAGS Flags;
            public int NameOffset;

            // These are valid if Flags & Struct not set. 
            public TdhInputType InType;
            public ushort OutType;             // Really TdhOutputType
            public int MapNameOffset;

            // These are valid if Flags & Struct is set.  
            public int StructStartIndex
            {
                get
                {
                    System.Diagnostics.Debug.Assert((Flags & PROPERTY_FLAGS.Struct) != 0);
                    return (ushort)InType;
                }
            }
            public int NumOfStructMembers
            {
                get
                {
                    System.Diagnostics.Debug.Assert((Flags & PROPERTY_FLAGS.Struct) != 0);
                    return (ushort)OutType;
                }
            }

            // Normally Count is 1 (thus every field in an array, it is just that most array have fixed size of 1)
            public ushort CountOrCountIndex;                // Flags & ParamFixedLength determines if it count, otherwise countIndex 
            // Normally Length is the size of InType (thus is fixed), but can be variable for blobs.
            public ushort LengthOrLengthIndex;              // Flags & ParamLength determines if it lengthIndex otherwise it is the length InType
            public int Reserved;
        }

        [Flags]
        internal enum PROPERTY_FLAGS
        {
            None = 0,
            Struct = 0x1,
            ParamLength = 0x2,
            ParamCount = 0x4,
            WbemXmlFragment = 0x8,
            ParamFixedLength = 0x10
        }

        internal enum TdhInputType : ushort
        {
            Null,
            UnicodeString,
            AnsiString,
            Int8,
            UInt8,
            Int16,
            UInt16,
            Int32,
            UInt32,
            Int64,
            UInt64,
            Float,
            Double,
            Boolean,
            Binary,
            GUID,
            Pointer,
            FILETIME,
            SYSTEMTIME,
            SID,
            HexInt32,
            HexInt64,                   // End of winmeta intypes 21
            CountedUtf16String = 22,    // new for TraceLogging
            CountedMbcsString = 23,
            Struct = 24,

            CountedString = 300,        // Start of TDH intypes for WBEM
            CountedAnsiString,
            ReversedCountedString,
            ReversedCountedAnsiString,
            NonNullTerminatedString,
            NonNullTerminatedAnsiString,
            UnicodeChar,
            AnsiChar,
            SizeT,
            HexDump,
            WbemSID
        };
        #endregion
    }

    /// <summary>
    /// ExternalTraceEventParser is an abstract class that acts as a parser for any 'External' resolution
    /// This include the TDH (RegisteredTraceEventParser) as well as the WPPTraceEventParser.   
    /// </summary>
    public abstract unsafe class ExternalTraceEventParser : TraceEventParser
    {
        /// <summary>
        /// Create a new ExternalTraceEventParser and attach it to the given TraceEventSource
        /// </summary>
        protected unsafe ExternalTraceEventParser(TraceEventSource source)
            : base(source)
        {
            m_state = (ExternalTraceEventParserState)StateObject;
            if (m_state == null)
            {
                StateObject = m_state = new ExternalTraceEventParserState();
                m_state.m_templates = new Dictionary<TraceEvent, DynamicTraceEventData>(new ExternalTraceEventParserState.TraceEventComparer());

#if !DOTNET_V35
                var symbolSource = new SymbolTraceEventParser(source);
                symbolSource.MetaDataEventInfo += delegate(EmptyTraceData data)
                {
                    DynamicTraceEventData template = (new RegisteredTraceEventParser.TdhEventParser((byte*)data.userData)).ParseEventMetaData();

                    // Uncomment this if you want to see the template in the debugger at this point
                    // template.source = data.source;
                    // template.eventRecord = data.eventRecord;
                    // template.userData = data.userData;  
                    m_state.m_templates[template] = template;
                };
#endif

                this.source.RegisterUnhandledEvent(delegate(TraceEvent unknown)
                {
                    // See if we already have this definition 
                    DynamicTraceEventData parsedTemplate = null;

                    if (!m_state.m_templates.TryGetValue(unknown, out parsedTemplate))
                    {
                        parsedTemplate = TryLookup(unknown);
                        if (parsedTemplate == null)
                            m_state.m_templates.Add(unknown.Clone(), null);         // add an entry to remember that we tried and failed.  
                    }
                    if (parsedTemplate == null)
                        return false;

                    // registeredWithTraceEventSource is a fail safe.   Basically if you added yourself to the table
                    // (In OnNewEventDefinition) then you should not come back as unknown, however because of dual events
                    // and just general fragility we don't want to rely on that.  So we keep a bit and insure that we
                    // only add the event definition once.  
                    if (!parsedTemplate.registeredWithTraceEventSource)
                    {
                        parsedTemplate.registeredWithTraceEventSource = true;
                        return OnNewEventDefintion(parsedTemplate, false) == EventFilterResponse.AcceptEvent;
                    }
                    return false;
                });
            }
        }

        /// <summary>
        /// Override.  
        /// </summary>
        public override bool IsStatic { get { return false; } }

        #region private
        /// <summary>
        /// Override
        /// </summary>
        protected override string GetProviderName()
        {
            // We handle more than one provider, so the convention is to return null. 
            return null;
        }

        /// <summary>
        /// override
        /// </summary>
        protected internal override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            // Normally state is setup in the constructor, but call can be invoked before the constructor has finished, 
            if (m_state == null)
                m_state = (ExternalTraceEventParserState)StateObject;

            if (m_state != null)
            {
                foreach (var template in m_state.m_templates.Values)
                {
                    if (template == null)
                        continue;
                    if (eventsToObserve == null || eventsToObserve(template.ProviderName, template.EventName) == EventFilterResponse.AcceptEvent)
                        callback(template);
                }
            }
        }

        /// <summary>
        /// Register 'template' so that if there are any subscriptions to template they get registered with the source.    
        /// </summary>
        internal override EventFilterResponse OnNewEventDefintion(DynamicTraceEventData template, bool mayHaveExistedBefore)
        {
            m_state.m_templates[template] = template;
            return base.OnNewEventDefintion(template, mayHaveExistedBefore);
        }

        internal abstract DynamicTraceEventData TryLookup(TraceEvent unknownEvent);

        ExternalTraceEventParserState m_state;
        #endregion
    }

    #region internal classes
    /// <summary>
    /// TDHDynamicTraceEventParserState represents the state of a  TDHDynamicTraceEventParser that needs to be
    /// serialized to a log file.  It does NOT include information about what events are chosen but DOES contain
    /// any other necessary information that came from the ETL data file or the OS TDH APIs.  
    /// </summary>
    internal class ExternalTraceEventParserState : IFastSerializable
    {
        public ExternalTraceEventParserState() { }

        // This is set of all dynamic event templates that this parser has TRIED to resolve.  If resolution 
        // failed then the value is null, otherwise it is the same as the key.   
        internal Dictionary<TraceEvent, DynamicTraceEventData> m_templates;

        /// <summary>
        /// This defines what it means to be the same event.   For manifest events it means provider and event ID
        /// for classic, it means that taskGuid and opcode match.  
        /// </summary>
        internal class TraceEventComparer : IEqualityComparer<TraceEvent>
        {
            public bool Equals(TraceEvent x, TraceEvent y)
            {
                Debug.Assert(!(x.lookupAsWPP && x.lookupAsClassic));
                if (x.lookupAsClassic != y.lookupAsClassic)
                    return false;
                if (x.lookupAsWPP != y.lookupAsWPP)
                    return false;

                if (x.lookupAsClassic)
                {
                    Debug.Assert(x.taskGuid != Guid.Empty && y.taskGuid != Guid.Empty);
                    return (x.taskGuid == y.taskGuid) && (x.Opcode == y.Opcode);
                }
                else if (x.lookupAsWPP)
                {
                    Debug.Assert(x.taskGuid != Guid.Empty && y.taskGuid != Guid.Empty);
                    return (x.taskGuid == y.taskGuid) && (x.ID == y.ID);
                }
                else
                {
                    Debug.Assert(x.ProviderGuid != Guid.Empty && y.ProviderGuid != Guid.Empty);
                    return (x.ProviderGuid == y.ProviderGuid) && (x.ID == y.ID);
                }
            }
            public int GetHashCode(TraceEvent obj)
            {
                if (obj.lookupAsClassic)
                    return obj.taskGuid.GetHashCode() + (int)obj.Opcode;
                else if (obj.lookupAsWPP)
                    return obj.taskGuid.GetHashCode() + (int)obj.ID;
                else
                    return obj.ProviderGuid.GetHashCode() + (int)obj.ID;
            }
        }

        #region IFastSerializable Members
        /// <summary>
        /// Implements IFastSerializable interface
        /// </summary>
        public virtual void ToStream(Serializer serializer)
        {
            // Calcluate the count.  
            var count = 0;
            foreach (var template in m_templates.Values)
            {
                if (template != null)
                    count++;
            }

            serializer.Write(count);
            foreach (var template in m_templates.Values)
            {
                if (template != null)
                {
#if DEBUG
                    --count;
#endif
                    serializer.Write(template);
                }
            }
            Debug.Assert(count == 0);
        }

        /// <summary>
        /// Implements IFastSerializable interface
        /// </summary>
        public virtual void FromStream(Deserializer deserializer)
        {
            int count;
            deserializer.Read(out count);
            m_templates = new Dictionary<TraceEvent, DynamicTraceEventData>(new TraceEventComparer());
            for (int i = 0; i < count; i++)
            {
                DynamicTraceEventData template;
                deserializer.Read(out template);
                m_templates.Add(template, template);
            }
        }
        #endregion
    }

    #endregion
}
