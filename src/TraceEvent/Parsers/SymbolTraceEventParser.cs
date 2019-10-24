//     Copyright (c) Microsoft Corporation.  All rights reserved.
using Microsoft.Diagnostics.Tracing.Parsers.Symbol;
using System;
using System.Diagnostics;
using System.Text;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Tracing.Parsers
{
    /// <summary>
    /// Kernel traces have information about images that are loaded, however they don't have enough information
    /// in the events themselves to unambigously look up PDBs without looking at the data inside the images.
    /// This means that symbols can't be resolved unless you are on the same machine on which you gathered the data.
    /// 
    /// XPERF solves this problem by adding new 'synthetic' events that it creates by looking at the trace and then
    /// opening each DLL mentioned and extracting the information needed to look PDBS up on a symbol server (this 
    /// includes the PE file's TimeDateStamp as well as a PDB Guid, and 'pdbAge' that can be found in the DLLs header.
    /// 
    /// These new events are added when XPERF runs the 'merge' command (or -d flag is passed).  It is also exposed 
    /// through the KernelTraceControl.dll!CreateMergedTraceFile API.   
    /// 
    /// SymbolTraceEventParser is a parser for extra events.   
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("traceparsergen", "1.0")]
    public sealed class SymbolTraceEventParser : TraceEventParser
    {
        public static readonly string ProviderName = "KernelTraceControl";
        public static readonly Guid ProviderGuid = new Guid(0x28ad2447, 0x105b, 0x4fe2, 0x95, 0x99, 0xe5, 0x9b, 0x2a, 0xa9, 0xa6, 0x34);

        public SymbolTraceEventParser(TraceEventSource source)
            : base(source)
        {
        }

        /// <summary>
        ///  The DbgIDRSDS event is added by XPERF for every Image load.  It contains the 'PDB signature' for the DLL, 
        ///  which is enough to unambiguously look the image's PDB up on a symbol server.  
        /// </summary>
        public event Action<DbgIDRSDSTraceData> ImageIDDbgID_RSDS
        {
            add
            {
                source.RegisterEventTemplate(new DbgIDRSDSTraceData(value, 0xFFFF, 0, "ImageID", ImageIDTaskGuid, DBGID_LOG_TYPE_RSDS, "DbgID_RSDS", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, DBGID_LOG_TYPE_RSDS, ImageIDTaskGuid);
            }
        }
        /// <summary>
        /// Every DLL has a Timestamp in the PE file itself that indicates when it is built.  This event dumps this timestamp.
        /// This timestamp is used to be as the 'signature' of the image and is used as a key to find the symbols, however 
        /// this has mostly be superseded by the DbgID/RSDS event. 
        /// </summary>
        public event Action<ImageIDTraceData> ImageID
        {
            add
            {
                source.RegisterEventTemplate(new ImageIDTraceData(value, 0xFFFF, 0, "ImageID", ImageIDTaskGuid, DBGID_LOG_TYPE_IMAGEID, "Info", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, DBGID_LOG_TYPE_IMAGEID, ImageIDTaskGuid);
            }

        }
        /// <summary>
        /// The FileVersion event contains information from the file version resource that most DLLs have that indicated
        /// detailed information about the exact version of the DLL.  (What is in the File->Properties->Version property
        /// page)
        /// </summary>
        public event Action<FileVersionTraceData> ImageIDFileVersion
        {
            add
            {
                source.RegisterEventTemplate(new FileVersionTraceData(value, 0xFFFF, 0, "ImageID", ImageIDTaskGuid, DBGID_LOG_TYPE_FILEVERSION, "FileVersion", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, DBGID_LOG_TYPE_FILEVERSION, ImageIDTaskGuid);
            }

        }
        /// <summary>
        /// I don't really care about this one, but I need a definition in order to exclude it because it
        /// has the same timestamp as a imageLoad event, and two events with the same timestamp confuse the 
        /// association between a stack and the event for the stack.  
        /// </summary>
        public event Action<EmptyTraceData> ImageIDNone
        {
            add
            {
                source.RegisterEventTemplate(new EmptyTraceData(value, 0xFFFF, 0, "ImageID", ImageIDTaskGuid, DBGID_LOG_TYPE_NONE, "None", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, DBGID_LOG_TYPE_NONE, ImageIDTaskGuid);
            }
        }

        public event Action<DbgIDILRSDSTraceData> ImageIDDbgID_ILRSDS
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DbgIDILRSDSTraceData(value, 0xFFFF, 0, "ImageID", ImageIDTaskGuid, DBGID_LOG_TYPE_ILRSDS, "DbgID_ILRSDS", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, DBGID_LOG_TYPE_ILRSDS, ImageIDTaskGuid);
            }
        }
        public event Action<DbgPPDBTraceData> ImageIDDbgPPDB
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DbgPPDBTraceData(value, 0xFFFF, 0, "ImageID", ImageIDTaskGuid, DBGID_LOG_TYPE_PPDB, "DbgPPDB", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, DBGID_LOG_TYPE_PPDB, ImageIDTaskGuid);
            }
        }

        public event Action<DbgDetermTraceData> ImageIDDbgDeterm
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DbgDetermTraceData(value, 0xFFFF, 0, "ImageID", ImageIDTaskGuid, DBGID_LOG_TYPE_DETERM, "DbgDeterm", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, DBGID_LOG_TYPE_DETERM, ImageIDTaskGuid);
            }
        }

        // The WinSat events are generated by merging, and are full of useful machine-wide information
        // encoded as a compressed XML blob.   
        public event Action<WinSatXmlTraceData> WinSatWinSPR
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new WinSatXmlTraceData(value, 0xFFFF, 0, "WinSat", WinSatTaskGuid, 33, "WinSPR", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 33, WinSatTaskGuid);
            }
        }
        public event Action<WinSatXmlTraceData> WinSatMetrics
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new WinSatXmlTraceData(value, 0xFFFF, 0, "WinSat", WinSatTaskGuid, 35, "Metrics", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 35, WinSatTaskGuid);
            }
        }
        public event Action<WinSatXmlTraceData> WinSatSystemConfig
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new WinSatXmlTraceData(value, 0xFFFF, 0, "WinSat", WinSatTaskGuid, 37, "SystemConfig", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 37, WinSatTaskGuid);
            }
        }

        /// <summary>
        /// This event has a TRACE_EVENT_INFO as its payload, and allows you to decode an event
        /// </summary>
        public event Action<EmptyTraceData> MetaDataEventInfo
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, 0xFFFF, 0, "MetaData", MetaDataTaskGuid, 32, "EventInfo", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 32, MetaDataTaskGuid);
            }
        }
        /// <summary>
        /// The event describes a Map (bitmap or ValueMap), and has a payload as follows   
        /// 
        ///     GUID            ProviderId;  
        ///     EVENT_MAP_INFO EventMapInfo;  
        /// </summary>
        public event Action<EmptyTraceData> MetaDataEventMapInfo
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, 0xFFFF, 0, "MetaData", MetaDataTaskGuid, 33, "EventMapInfo", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 33, MetaDataTaskGuid);
            }
        }

        #region Private
        protected override string GetProviderName() { return ProviderName; }
        static private volatile TraceEvent[] s_templates;
        protected internal override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            if (s_templates == null)
            {
                s_templates = new TraceEvent[]
                {
                    new DbgIDRSDSTraceData(null, 0xFFFF, 0, "ImageID", ImageIDTaskGuid, DBGID_LOG_TYPE_RSDS, "DbgID_RSDS", ProviderGuid, ProviderName),
                    new ImageIDTraceData(null, 0xFFFF, 0, "ImageID", ImageIDTaskGuid, DBGID_LOG_TYPE_IMAGEID, "Info", ProviderGuid, ProviderName),
                    new FileVersionTraceData(null, 0xFFFF, 0, "ImageID", ImageIDTaskGuid, DBGID_LOG_TYPE_FILEVERSION, "FileVersion", ProviderGuid, ProviderName),
                    new EmptyTraceData(null, 0xFFFF, 0, "ImageID", ImageIDTaskGuid, DBGID_LOG_TYPE_NONE, "None", ProviderGuid, ProviderName),
                    new DbgIDILRSDSTraceData(null, 0xFFFF, 0, "ImageID", ImageIDTaskGuid, DBGID_LOG_TYPE_ILRSDS, "DbgID_ILRSDS", ProviderGuid, ProviderName),
                    new DbgPPDBTraceData(null, 0xFFFF, 0, "ImageID", ImageIDTaskGuid, DBGID_LOG_TYPE_PPDB, "DbgPPDB", ProviderGuid, ProviderName),
                    new DbgDetermTraceData(null, 0xFFFF, 0, "ImageID", ImageIDTaskGuid, DBGID_LOG_TYPE_DETERM, "DbgDeterm", ProviderGuid, ProviderName),
                    new WinSatXmlTraceData(null, 0xFFFF, 0, "WinSat", WinSatTaskGuid, 33, "WinSPR", ProviderGuid, ProviderName),
                    new WinSatXmlTraceData(null, 0xFFFF, 0, "WinSat", WinSatTaskGuid, 35, "Metrics", ProviderGuid, ProviderName),
                    new WinSatXmlTraceData(null, 0xFFFF, 0, "WinSat", WinSatTaskGuid, 37, "SystemConfig", ProviderGuid, ProviderName),
                    new EmptyTraceData(null, 0xFFFF, 0, "MetaData", MetaDataTaskGuid, 32, "EventInfo", ProviderGuid, ProviderName),
                    new EmptyTraceData(null, 0xFFFF, 0, "MetaData", MetaDataTaskGuid, 33, "EventMapInfo", ProviderGuid, ProviderName)
                };
            }
            foreach (var template in s_templates)
                if (eventsToObserve == null || eventsToObserve(template.ProviderName, template.EventName) == EventFilterResponse.AcceptEvent)
                    callback(template);
        }

        // These are the Opcode numbers for various events
        public const int DBGID_LOG_TYPE_IMAGEID = 0x00;
        public const int DBGID_LOG_TYPE_NONE = 0x20;
        public const int DBGID_LOG_TYPE_RSDS = 0x24;
        public const int DBGID_LOG_TYPE_ILRSDS = 0x25;
        public const int DBGID_LOG_TYPE_PPDB = 0x26;
        public const int DBGID_LOG_TYPE_DETERM = 0x28;
        public const int DBGID_LOG_TYPE_FILEVERSION = 0x40;

        // Used to log meta-data about crimson events into the log.  
        internal static readonly Guid ImageIDTaskGuid = new Guid(unchecked((int)0xB3E675D7), 0x2554, 0x4f18, 0x83, 0x0B, 0x27, 0x62, 0x73, 0x25, 0x60, 0xDE);
        internal static readonly Guid WinSatTaskGuid = new Guid(unchecked((int)0xed54dff8), unchecked((short)0xc409), 0x4cf6, 0xbf, 0x83, 0x05, 0xe1, 0xe6, 0x1a, 0x09, 0xc4);
        internal static readonly Guid MetaDataTaskGuid = new Guid(unchecked((int)0xbbccf6c1), 0x6cd1, 0x48c4, 0x80, 0xff, 0x83, 0x94, 0x82, 0xe3, 0x76, 0x71);
        internal static readonly Guid PerfTrackMetaDataTaskGuid = new Guid(unchecked((int)0xbf6ef1cb), unchecked((short)0x89b5), 0x490, 0x80, 0xac, 0xb1, 0x80, 0xcf, 0xbc, 0xff, 0x0f);
        #endregion
    }
}

namespace Microsoft.Diagnostics.Tracing.Parsers.Symbol
{
    public sealed class FileVersionTraceData : TraceEvent
    {
        public int ImageSize { get { return GetInt32At(0); } }
        public int TimeDateStamp { get { return GetInt32At(4); } }
        public DateTime BuildTime { get { return PEFile.PEHeader.TimeDateStampToDate(TimeDateStamp); } }
        public string OrigFileName { get { return GetUnicodeStringAt(8); } }
        public string FileDescription { get { return GetUnicodeStringAt(SkipUnicodeString(8, 1)); } }
        public string FileVersion { get { return GetUnicodeStringAt(SkipUnicodeString(8, 2)); } }
        public string BinFileVersion { get { return GetUnicodeStringAt(SkipUnicodeString(8, 3)); } }
        public string VerLanguage { get { return GetUnicodeStringAt(SkipUnicodeString(8, 4)); } }
        public string ProductName { get { return GetUnicodeStringAt(SkipUnicodeString(8, 5)); } }
        public string CompanyName { get { return GetUnicodeStringAt(SkipUnicodeString(8, 6)); } }
        public string ProductVersion { get { return GetUnicodeStringAt(SkipUnicodeString(8, 7)); } }
        public string FileId { get { return GetUnicodeStringAt(SkipUnicodeString(8, 8)); } }
        public string ProgramId { get { return GetUnicodeStringAt(SkipUnicodeString(8, 9)); } }

        #region Private
        internal FileVersionTraceData(Action<FileVersionTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opCode, string opCodeName, Guid providerGuid, string providerName) :
            base(eventID, task, taskName, taskGuid, opCode, opCodeName, providerGuid, providerName)
        {
            Action = action;
        }

        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override Delegate Target
        {
            get { return Action; }
            set { Action = (Action<FileVersionTraceData>)value; }
        }
        protected internal override void Validate()
        {
            Debug.Assert(EventDataLength == SkipUnicodeString(8, 10));
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "ImageSize", "TimeDateStamp", "BuildTime", "OrigFileName", "FileDescription", "FileVersion",
                        "BinFileVersion", "VerLanguage", "ProductName", "CompanyName", "ProductVersion", "FileId", "ProgramId" };
                }
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ImageSize;
                case 1:
                    return TimeDateStamp;
                case 2:
                    return BuildTime;
                case 3:
                    return OrigFileName;
                case 4:
                    return FileDescription;
                case 5:
                    return FileVersion;
                case 6:
                    return BinFileVersion;
                case 7:
                    return VerLanguage;
                case 8:
                    return ProductName;
                case 9:
                    return CompanyName;
                case 10:
                    return ProductVersion;
                case 11:
                    return FileId;
                case 12:
                    return ProgramId;
                default:
                    Debug.Assert(false, "invalid index");
                    return null;
            }
        }

        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttribHex(sb, "ImageSize", ImageSize);
            XmlAttribHex(sb, "TimeDateStamp", TimeDateStamp);
            XmlAttrib(sb, "OrigFileName", OrigFileName);
            XmlAttrib(sb, "FileDescription", FileDescription);
            XmlAttrib(sb, "FileVersion", FileVersion);
            XmlAttrib(sb, "BinFileVersion", BinFileVersion);
            XmlAttrib(sb, "VerLanguage", VerLanguage);
            XmlAttrib(sb, "ProductName", ProductName);
            XmlAttrib(sb, "CompanyName", CompanyName);
            XmlAttrib(sb, "ProductVersion", ProductVersion);
            XmlAttrib(sb, "FileId", FileId);
            XmlAttrib(sb, "ProgramId", ProgramId);
            sb.Append("/>");
            return sb;
        }
        private Action<FileVersionTraceData> Action;
        #endregion
    }
    public sealed class DbgIDRSDSTraceData : TraceEvent
    {
        public Address ImageBase { get { return GetAddressAt(0); } }
        // public int ProcessID { get { return GetInt32At(HostOffset(4, 1)); } }    // This seems to be redundant with the ProcessID in the event header
        public Guid GuidSig { get { return GetGuidAt(HostOffset(8, 1)); } }
        public int Age { get { return GetInt32At(HostOffset(24, 1)); } }
        public string PdbFileName { get { return GetUTF8StringAt(HostOffset(28, 1)); } }

        #region Private
        internal DbgIDRSDSTraceData(Action<DbgIDRSDSTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opCode, string opCodeName, Guid providerGuid, string providerName) :
            base(eventID, task, taskName, taskGuid, opCode, opCodeName, providerGuid, providerName)
        {
            Action = action;
        }

        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override Delegate Target
        {
            get { return Action; }
            set { Action = (Action<DbgIDRSDSTraceData>)value; }
        }
        protected internal override void Validate()
        {
            Debug.Assert(EventDataLength == SkipUTF8String(HostOffset(28, 1)));
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "ImageBase", "GuidSig", "Age", "PDBFileName" };
                }
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ImageBase;
                case 1:
                    return GuidSig;
                case 2:
                    return Age;
                case 3:
                    return PdbFileName;
                default:
                    Debug.Assert(false, "invalid index");
                    return null;
            }
        }

        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttribHex(sb, "ImageBase", ImageBase);
            XmlAttrib(sb, "GuidSig", GuidSig);
            XmlAttrib(sb, "Age", Age);
            XmlAttrib(sb, "PdbFileName", PdbFileName);
            sb.Append("/>");
            return sb;
        }
        private Action<DbgIDRSDSTraceData> Action;
        #endregion
    }
    public sealed class ImageIDTraceData : TraceEvent
    {
        public Address ImageBase { get { return GetAddressAt(0); } }
        public long ImageSize { get { return GetIntPtrAt(HostOffset(4, 1)); } }
        // Seems to always be 0
        // public int ProcessID { get { return GetInt32At(HostOffset(8, 2)); } }
        public int TimeDateStamp { get { return GetInt32At(HostOffset(12, 2)); } }

        public DateTime BuildTime { get { return PEFile.PEHeader.TimeDateStampToDate(TimeDateStamp); } }

        public string OriginalFileName { get { return GetUnicodeStringAt(HostOffset(16, 2)); } }

        #region Private
        internal ImageIDTraceData(Action<ImageIDTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opCode, string opCodeName, Guid providerGuid, string providerName) :
            base(eventID, task, taskName, taskGuid, opCode, opCodeName, providerGuid, providerName)
        {
            Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override Delegate Target
        {
            get { return Action; }
            set { Action = (Action<ImageIDTraceData>)value; }
        }
        protected internal override void Validate()
        {
            Debug.Assert(EventDataLength == SkipUnicodeString(HostOffset(16, 2)));
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "ImageBase", "ImageSize", "ProcessID", "TimeDateStamp", "BuildTime", "OriginalFileName" };
                }
                return payloadNames;

            }
        }
        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ImageBase;
                case 1:
                    return ImageSize;
                case 2:
                    return 0;
                case 3:
                    return TimeDateStamp;
                case 4:
                    return BuildTime;
                case 5:
                    return OriginalFileName;
                default:
                    Debug.Assert(false, "bad index value");
                    return null;
            }
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttribHex(sb, "ImageBase", ImageBase);
            XmlAttribHex(sb, "ImageSize", ImageSize);
            XmlAttribHex(sb, "TimeDateStamp", TimeDateStamp);
            XmlAttrib(sb, "BuildTime", BuildTime);
            XmlAttrib(sb, "OriginalFileName", OriginalFileName);
            sb.Append("/>");
            return sb;
        }

        private event Action<ImageIDTraceData> Action;
        #endregion
    }

    public sealed class DbgIDILRSDSTraceData : TraceEvent
    {
        public Address ImageBase { get { return GetAddressAt(0); } }

        // This seems to be redundant with the ProcessID in the event header
        //public int ProcessID { get { return GetInt32At(HostOffset(4, 1)); } }
        public Guid GuidSig { get { return GetGuidAt(HostOffset(8, 1)); } }
        public int Age { get { return GetInt32At(HostOffset(24, 1)); } }
        public string PdbFileName { get { return GetUTF8StringAt(HostOffset(28, 1)); } }

        #region Private
        internal DbgIDILRSDSTraceData(Action<DbgIDILRSDSTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opCode, string opCodeName, Guid providerGuid, string providerName) :
            base(eventID, task, taskName, taskGuid, opCode, opCodeName, providerGuid, providerName)
        {
            Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override Delegate Target
        {
            get { return Action; }
            set { Action = (Action<DbgIDILRSDSTraceData>)value; }
        }

        protected internal override void Validate()
        {
            Debug.Assert(EventDataLength == SkipUTF8String(HostOffset(32, 1)));
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "ImageBase", "GuidSig", "Age", "PDBFileName" };
                }
                return payloadNames;

            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ImageBase;
                case 1:
                    return GuidSig;
                case 2:
                    return Age;
                case 3:
                    return PdbFileName;
                default:
                    Debug.Assert(false, "invalid index");
                    return null;
            }
        }

        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttribHex(sb, "ImageBase", ImageBase);
            XmlAttrib(sb, "GuidSig", GuidSig);
            XmlAttrib(sb, "Age", Age);
            XmlAttrib(sb, "PdbFileName", PdbFileName);
            sb.Append("/>");
            return sb;
        }

        private event Action<DbgIDILRSDSTraceData> Action;

        #endregion
    }

    public sealed class DbgPPDBTraceData : TraceEvent
    {
        public Address ImageBase { get { return GetAddressAt(0); } }

        // This seems to be redundant with the ProcessID in the event header
        //public int ProcessID { get { return GetInt32At(HostOffset(4, 1)); } }

        public int TimeDateStamp { get { return GetInt32At(HostOffset(8, 1)); } }

        public int MajorVersion { get { return GetByteAt(HostOffset(12, 1)); } }

        public int MinorVersion { get { return GetByteAt(HostOffset(14, 1)); } }

        #region Private
        internal DbgPPDBTraceData(Action<DbgPPDBTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opCode, string opCodeName, Guid providerGuid, string providerName) :
    base(eventID, task, taskName, taskGuid, opCode, opCodeName, providerGuid, providerName)
        {
            Action = action;
        }

        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override Delegate Target
        {
            get { return Action; }
            set { Action = (Action<DbgPPDBTraceData>)value; }
        }

        protected internal override void Validate()
        {
            Debug.Assert(EventDataLength == HostOffset(16, 1));
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "ImageBase", "TimeDateStamp", "MajorVersion", "MinorVersion" };
                }
                return payloadNames;

            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ImageBase;
                case 1:
                    return TimeDateStamp;
                case 2:
                    return MajorVersion;
                case 3:
                    return MinorVersion;
                default:
                    Debug.Assert(false, "bad index value");
                    return null;
            }
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttribHex(sb, "ImageBase", ImageBase);
            XmlAttribHex(sb, "TimeDateStamp", TimeDateStamp);
            XmlAttrib(sb, "MajorVersion", MajorVersion);
            XmlAttrib(sb, "MinorVersion", MinorVersion);
            sb.Append("/>");
            return sb;
        }

        private event Action<DbgPPDBTraceData> Action;
        #endregion
    }

    public sealed class DbgDetermTraceData : TraceEvent
    {
        public Address ImageBase { get { return GetAddressAt(0); } }

        // This seems to be redundant with the ProcessID in the event header
        //public int ProcessID { get { return GetInt32At(HostOffset(4, 1)); } }

        #region Private
        internal DbgDetermTraceData(Action<DbgDetermTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opCode, string opCodeName, Guid providerGuid, string providerName) :
            base(eventID, task, taskName, taskGuid, opCode, opCodeName, providerGuid, providerName)
        {
            Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override Delegate Target
        {
            get { return Action; }
            set { Action = (Action<DbgDetermTraceData>)value; }
        }

        protected internal override void Validate()
        {
            Debug.Assert(EventDataLength == HostOffset(8, 1));
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "ImageBase" };
                }
                return payloadNames;

            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ImageBase;
                default:
                    Debug.Assert(false, "bad index value");
                    return null;
            }
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttribHex(sb, "ImageBase", ImageBase);
            sb.Append("/>");
            return sb;
        }

        private event Action<DbgDetermTraceData> Action;

        #endregion
    }

    public sealed class WinSatXmlTraceData : TraceEvent
    {
        /// <summary>
        /// The value of the one string payload property.  
        /// </summary>
        public string Xml
        {
            get
            {
                if (m_xml == null)
                {
                    m_xml = GetXml();
                }

                return m_xml;
            }
        }
        /// <summary>
        /// Construct a TraceEvent template which has one string payload field with the given metadata and action
        /// </summary>
        public WinSatXmlTraceData(Action<WinSatXmlTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            Action = action;
        }
        #region Private
        /// <summary>
        /// implementation of TraceEvent Interface. 
        /// </summary>
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.Append(">");
            sb.AppendLine(Xml);
            sb.AppendLine("</Event>");
            return sb;
        }
        /// <summary>
        /// implementation of TraceEvent Interface. 
        /// </summary>
        public override string[] PayloadNames
        {
            get
            {
                // We dont put the XML in the fields because it is too big (it does go in the ToXml).  
                if (payloadNames == null)
                {
                    payloadNames = new string[] { };
                }

                return payloadNames;
            }
        }
        /// <summary>
        /// implementation of TraceEvent Interface. 
        /// </summary>
        public override object PayloadValue(int index)
        {
            Debug.Assert(index < 1);
            switch (index)
            {
                default:
                    return null;
            }
        }

        private event Action<WinSatXmlTraceData> Action;
        /// <summary>
        /// implementation of TraceEvent Interface. 
        /// </summary>
        protected internal override void Dispatch()
        {
            Action(this);
        }
        /// <summary>
        /// override
        /// </summary>
        protected internal override Delegate Target
        {
            get { return Action; }
            set { Action = (Action<WinSatXmlTraceData>)value; }
        }

        private unsafe string GetXml()
        {
            int uncompressedSize = GetInt32At(4);
            if (0x10000 <= uncompressedSize)
            {
                return "";
            }

            byte[] uncompressedData = new byte[uncompressedSize];
            fixed (byte* uncompressedPtr = uncompressedData)
            {
                byte* compressedData = ((byte*)DataStart) + 8; // Skip header (State + UncompressedLength)
                int compressedSize = EventDataLength - 8;       // Compressed size is total size minus header. 

                int resultSize = 0;
                int hr = TraceEventNativeMethods.RtlDecompressBuffer(
                    TraceEventNativeMethods.COMPRESSION_FORMAT_LZNT1 | TraceEventNativeMethods.COMPRESSION_ENGINE_MAXIMUM,
                    uncompressedPtr,
                    uncompressedSize,
                    compressedData,
                    compressedSize,
                    out resultSize);

                if (hr == 0 && resultSize == uncompressedSize)
                {
                    var indent = 0;
                    // PrettyPrint the XML
                    char* charPtr = (Char*)uncompressedPtr;
                    StringBuilder sb = new StringBuilder();
                    char* charEnd = &charPtr[uncompressedSize / 2];
                    bool noChildren = true;
                    while (charPtr < charEnd)
                    {
                        char c = *charPtr;
                        if (c == 0)
                        {
                            break;      // we will assume null termination
                        }

                        if (c == '<')
                        {
                            var c1 = charPtr[1];
                            bool newLine = false;
                            if (c1 == '/')
                            {
                                newLine = !noChildren;
                                noChildren = false;
                            }
                            else if (Char.IsLetter(c1))
                            {
                                noChildren = true;
                                newLine = true;
                                indent++;
                            }
                            if (newLine)
                            {
                                sb.AppendLine();
                                for (int i = 0; i < indent; i++)
                                {
                                    sb.Append(' ');
                                }
                            }
                            if (c1 == '/')
                            {
                                --indent;
                            }
                        }
                        sb.Append(c);
                        charPtr++;
                    }
                    return sb.ToString();
                }
            }
            return "";
        }

        private string m_xml;
        #endregion
    }
}
