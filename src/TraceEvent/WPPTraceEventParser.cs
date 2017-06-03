//     Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using FastSerialization;
using Microsoft.Diagnostics.Utilities;
using System.IO;
using System.Diagnostics.Tracing;

namespace Microsoft.Diagnostics.Tracing.Parsers
{
    /// <summary>
    /// This parser knows how to decode Windows Software Trace Preprocessor (WPP) events.  In order to decode
    /// the events it needs access to the TMF files that describe the events (these are created from the PDB at 
    /// build time). 
    /// <br/>
    /// You will generally use this for the 'FormattedMessage' property of the event.  
    /// </summary>
    public sealed class WppTraceEventParser : ExternalTraceEventParser
    {
        /// <summary>
        /// Construct a new WPPTraceEventParser that is attached to 'source'.   Once you do this the source
        /// will understand WPP events. In particular you can subscribe to the  Wpp.All event to get the
        /// stream of WPP events in the source. For WppTraceEventParser to function, it needs the TMF
        /// files for the events it will decode. You should pass the directory to find these TMF files 
        /// in 'TMFDirectory'.  Each file should have the form of a GUID.tmf.   
        /// </summary>
        /// <param name="source"></param>
        /// <param name="TMFDirectory"></param>
        public WppTraceEventParser(TraceEventSource source, string TMFDirectory)
            : base(source)
        {
            m_TMFDirectory = TMFDirectory;
        }

        #region private

        unsafe internal override DynamicTraceEventData TryLookup(TraceEvent unknownEvent)
        {
            // WPP is always classic 
            if (unknownEvent.IsClassicProvider)
            {
                var taskGuid = unknownEvent.taskGuid;
                var tmfPath = GetTmfPathForTaskGuid(taskGuid);
                if (tmfPath != null)
                {
                    var templates = CreateTemplatesForTMFFile(taskGuid, tmfPath);

                    // Register all the templates in the file, and if we found the specific one we are looking for return that one. 
                    DynamicTraceEventData ret = null;
                    foreach (var template in templates)
                    {
                        if (template.eventID == unknownEvent.eventID)
                            ret = template;
                        else
                            OnNewEventDefintion(template, false);
                    }
                    // If we fail, remove the file so we don't ever try to this Task's events again.  
                    m_tmfDataFilePathsByFileNameBase[taskGuid.ToString()] = null;
                    return ret;
                }
            }
            return null;
        }

        private string GetTmfPathForTaskGuid(Guid taskGuid)
        {
            if (m_tmfDataFilePathsByFileNameBase == null)
            {
                m_tmfDataFilePathsByFileNameBase = new Dictionary<string, string>(64);
                foreach (var path in DirectoryUtilities.GetFiles(m_TMFDirectory))
                {
                    var fileNameBase = Path.GetFileNameWithoutExtension(path);
                    m_tmfDataFilePathsByFileNameBase[fileNameBase] = path;
                }
            }

            string ret;
            m_tmfDataFilePathsByFileNameBase.TryGetValue(taskGuid.ToString(), out ret);
            return ret;
        }

        private struct TypeAndFormat
        {
            public TypeAndFormat(Type Type, IDictionary<long, string> Map) { this.Type = Type; this.Map = Map; }
            public Type Type;
            public IDictionary<long, string> Map;
        }

        private List<DynamicTraceEventData> CreateTemplatesForTMFFile(Guid taskGuid, string tmfPath)
        {
            List<DynamicTraceEventData> templates = new List<DynamicTraceEventData>();
            List<TypeAndFormat> parameterTypes = new List<TypeAndFormat>();

            using (StreamReader tmfData = File.OpenText(tmfPath))
            {
                string taskName = null;
                string providerName = null;
                Guid providerGuid = Guid.Empty;
                Match m;
                for (;;)
                {
                    var line = tmfData.ReadLine();
                    if (line == null)
                        break;

                    if (providerGuid == Guid.Empty)
                    {
                        m = Regex.Match(line, @"PDB: .*?(\w+)\.pdb\s*$", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            // We use the name of the mof file (which is the same as the PDB file) as the provider name.
                            if (string.IsNullOrEmpty(providerName))
                                providerName = m.Groups[1].Value;

                            string mofFilePath;
                            if (m_tmfDataFilePathsByFileNameBase.TryGetValue(providerName, out mofFilePath))
                            {
                                if (mofFilePath.EndsWith(".mof", StringComparison.OrdinalIgnoreCase))
                                {
                                    using (var mofFile = File.OpenText(mofFilePath))
                                    {
                                        for (;;)
                                        {
                                            var mofLine = mofFile.ReadLine();
                                            if (mofLine == null)
                                                break;
                                            m = Regex.Match(mofLine, @"guid\(.{(.*)}.\)", RegexOptions.IgnoreCase);
                                            if (m.Success)
                                            {
                                                try { providerGuid = new Guid(m.Groups[1].Value); }
                                                catch (Exception) { }
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (taskName == null)
                    {
                        // 7113b9e1-a0cc-d313-1eab-57efe9d7e56c build.server // SRC=TTSEngineCom.cpp MJ= MN=
                        m = Regex.Match(line, @"^\w+-\w+-\w+-\w+-\w+\s+(\S+)");
                        if (m.Success)
                            taskName = m.Groups[1].Value;
                    }
                    else
                    {
                        // #typev  ttstracing_cpp78 13 "%0%10!s! Error happens in Initializing %11!s!!" //   LEVEL=TRACE_LEVEL_ERROR FLAGS=TTS_Trace_Engine_Initialization FUNC=CTTSTracingHelper::LogComponentInitialization
                        m = Regex.Match(line, "^#typev\\s+(\\S*?)(\\d+)\\s+(\\d+)\\s+\"(.*)\"(.*)");
                        if (m.Success)
                        {
                            var fileName = m.Groups[1].Value;
                            var lineNum = int.Parse(m.Groups[2].Value);
                            var eventId = int.Parse(m.Groups[3].Value);
                            var formatStr = m.Groups[4].Value;

                            // Substitute in %!NAME! for their values as defined in the tail  
                            if (formatStr.Contains("%!"))
                            {
                                var tail = m.Groups[5].Value;
                                for (;;)
                                {
                                    var m1 = Regex.Match(formatStr, @"%!(\w+)!");
                                    if (!m1.Success)
                                        break;
                                    var varName = m1.Groups[1].Value;
                                    var varValue = "";
                                    m1 = Regex.Match(tail, varName + @"=(.*)");
                                    if (m1.Success)
                                    {
                                        varValue = m1.Groups[1].Value;
                                        varValue = Regex.Replace(varValue, @" \w+=.*", "");     // Remove things that look like the next key-value    
                                    }
                                    formatStr = formatStr.Replace("%!" + varName + "!", varValue);
                                }
                            }

                            var eventProviderName = taskName;
                            if (providerName != null)
                                eventProviderName = providerName + "/" + eventProviderName;

                            var template = new DynamicTraceEventData(null, eventId, 0, fileName + "/" + m.Groups[2].Value, taskGuid, 0, "", providerGuid, eventProviderName);
                            template.lookupAsWPP = true;                // Use WPP lookup conventions. 

                            parameterTypes.Clear();

                            for (;;)
                            {
                                line = tmfData.ReadLine();
                                if (line == null)
                                    break;
                                if (line.Trim() == "}")
                                    break;
                                // szPOSHeader, ItemString -- 10
                                m = Regex.Match(line, @"^.*, Item(\S+) -- (\d+)$");
                                if (m.Success)
                                {
                                    var typeStr = m.Groups[1].Value;
                                    Type type = null;
                                    IDictionary<long, string> map = null;
                                    if (typeStr == "String")
                                        type = typeof(StringBuilder);       // We use StringBuild to represent a ANSI string 
                                    else if (typeStr == "WString")
                                        type = typeof(string);
                                    else if (typeStr == "Long" || typeStr == "Ulong")
                                        type = typeof(int);
                                    else if (typeStr == "HRESULT" || typeStr == "NTSTATUS")
                                    {
                                        type = typeof(int);
                                        // By making map non-null we indicate that this is a enum, but we don't add any enum
                                        // mappings, which makes it print as Hex.  Thus we are just saying 'print as hex'  
                                        map = new SortedDictionary<long, string>();
                                    }
                                    else if (typeStr == "Double")
                                        type = typeof(double);
                                    else if (typeStr == "Ptr")
                                        type = typeof(IntPtr);
                                    else if (typeStr.StartsWith("Enum("))       // TODO more support for enums 
                                        type = typeof(int);
                                    else if (typeStr == "ULongLong" || typeStr.StartsWith("LongLong"))
                                        type = typeof(long);
                                    else if (typeStr == "ListLong(false,true)")
                                        type = typeof(bool);
                                    else if (typeStr.StartsWith("ListLong("))
                                        type = typeof(int);
                                    else if (typeStr == "Guid")
                                        type = typeof(Guid);

                                    if (type != null)
                                    {
                                        parameterTypes.Add(new TypeAndFormat(type, map));
                                    }
                                }
                            }
                            template.payloadNames = new string[parameterTypes.Count];
                            template.payloadFetches = new DynamicTraceEventData.PayloadFetch[parameterTypes.Count];
                            ushort offset = 0;
                            for (int i = 0; i < parameterTypes.Count; i++)
                            {
                                template.payloadNames[i] = "Arg" + (i + 1).ToString();
                                template.payloadFetches[i].Offset = offset;
                                var type = parameterTypes[i].Type;
                                template.payloadFetches[i].Map = parameterTypes[i].Map;
                                ushort size = 0;
                                if (type == typeof(StringBuilder))  // This mean ANSI_STRING (I just need a distinct type)
                                {
                                    type = typeof(string);
                                    size |= DynamicTraceEventData.IS_ANSI;
                                }
                                size |= DynamicTraceEventData.SizeOfType(type);
                                template.payloadFetches[i].Size = size;
                                template.payloadFetches[i].Type = type;

                                if (size >= DynamicTraceEventData.SPECIAL_SIZES || offset == ushort.MaxValue)
                                    offset = ushort.MaxValue;           // Indicate that the offset must be computed at run time.
                                else
                                    offset += size;
                            }

                            formatStr = formatStr.Replace("%0", "");    // TODO What is this?  Why is it here?  
                            formatStr = Regex.Replace(formatStr, @"%(\d+)!(\w?)\w*!", delegate (Match match)
                            {
                                var argNum = int.Parse(match.Groups[1].Value) - 10;     // 0 first arg ...

                                // If it has a !x qualifer after it change th map so it will be decoded as hex.  
                                if (match.Groups[2].Value == "x" && 0 <= argNum && argNum < template.payloadFetches.Length &&
                                    template.payloadFetches[argNum].Map == null)
                                    template.payloadFetches[argNum].Map = new SortedDictionary<long, string>();

                                return "%" + (argNum + 1).ToString();
                            });
                            template.MessageFormat = formatStr;

                            templates.Add(template);
                        }
                    }
                }
            }
            return templates;
        }

        string m_TMFDirectory;
        Dictionary<string, string> m_tmfDataFilePathsByFileNameBase;
        #endregion
    }

}
