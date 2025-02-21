using System;
using System.Diagnostics;
using System.Text;
using Address = System.UInt64;

#pragma warning disable 1591        // disable warnings on XML comments not being present

namespace Microsoft.Diagnostics.Tracing.Parsers
{
    using Microsoft.Diagnostics.Tracing.Parsers.Universal.Events;

    public sealed class UniversalSystemTraceEventParser : TraceEventParser
    {
        public static string ProviderName = "Universal.System";
        public static Guid ProviderGuid = new Guid("8c107b6c-79f8-5231-4de6-2a0e20a3f562");
        public enum Keywords : long
        {
            None = 0x0,
        };

        public UniversalSystemTraceEventParser(TraceEventSource source) : base(source) { }

        public event Action<ProcessCreateTraceData> ExistingProcess
        {
            add
            {
                source.RegisterEventTemplate(new ProcessCreateTraceData(value, 0, (int)TraceEventTask.Default, "ExistingProcess", Guid.Empty, 0, "Default", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 0, Guid.Empty);
            }
        }

        public event Action<ProcessCreateTraceData> ProcessCreate
        {
            add
            {
                source.RegisterEventTemplate(new ProcessCreateTraceData(value, 1, (int)TraceEventTask.Default, "ProcessCreate", Guid.Empty, 0, "Default", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 1, Guid.Empty);
            }
        }

        public event Action<ProcessExitTraceData> ProcessExit
        {
            add
            {
                source.RegisterEventTemplate(new ProcessExitTraceData(value, 2, (int)TraceEventTask.Default, "ProcessExit", Guid.Empty, 0, "Default", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 2, Guid.Empty);
            }
        }

        public event Action<ProcessMappingTraceData> ProcessMapping
        {
            add
            {
                source.RegisterEventTemplate(new ProcessMappingTraceData(value, 3, (int)TraceEventTask.Default, "ProcessMapping", Guid.Empty, 0, "Default", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 3, Guid.Empty);
            }
        }

        public event Action<ProcessSymbolTraceData> ProcessSymbol
        {
            add
            {
                source.RegisterEventTemplate(new ProcessSymbolTraceData(value, 4, (int)TraceEventTask.Default, "ProcessSymbol", Guid.Empty, 0, "Default", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 4, Guid.Empty);
            }
        }

        #region private
        protected override string GetProviderName() { return ProviderName; }

        static private volatile TraceEvent[] s_templates;

        protected internal override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            if (s_templates == null)
            {
                var templates = new TraceEvent[5];
                templates[0] = new ProcessCreateTraceData(null, 0, (int)TraceEventTask.Default, "ExistingProcess", Guid.Empty, 0, "Default", ProviderGuid, ProviderName);
                templates[1] = new ProcessCreateTraceData(null, 1, (int)TraceEventTask.Default, "ProcessCreate", Guid.Empty, 0, "Default", ProviderGuid, ProviderName);
                templates[2] = new ProcessExitTraceData(null, 2, (int)TraceEventTask.Default, "ProcessExit", Guid.Empty, 0, "Default", ProviderGuid, ProviderName);
                templates[3] = new ProcessMappingTraceData(null, 3, (int)TraceEventTask.Default, "ProcessMapping", Guid.Empty, 0, "Default", ProviderGuid, ProviderName);
                templates[4] = new ProcessSymbolTraceData(null, 4, (int)TraceEventTask.Default, "ProcessSymbol", Guid.Empty, 0, "Default", ProviderGuid, ProviderName);
                s_templates = templates;
            }
            foreach (var template in s_templates)
                if (eventsToObserve == null || eventsToObserve(template.ProviderName, template.EventName) == EventFilterResponse.AcceptEvent)
                    callback(template);
        }

        #endregion
    }
}

namespace Microsoft.Diagnostics.Tracing.Parsers.Universal.Events
{
    public sealed class ProcessCreateTraceData : TraceEvent
    {
        public int Id { get { return GetInt32At(0); } }
        public int NamespaceId { get { return GetInt32At(4); } }

        public string Name { get { return GetUnicodeStringAt(8); } }

        public string NamespaceName { get { return GetUnicodeStringAt(SkipUnicodeString(8)); } }

        #region Private
        internal ProcessCreateTraceData(Action<ProcessCreateTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
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
            set { Action = (Action<ProcessCreateTraceData>)value; }
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttrib(sb, "Id", Id);
            XmlAttrib(sb, "NamespaceId", NamespaceId);
            XmlAttrib(sb, "Name", Name);
            XmlAttrib(sb, "NamespaceName", NamespaceName);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Id", "NamespaceId", "Name", "NamespaceName" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Id;
                case 1:
                    return NamespaceId;
                case 2:
                    return Name;
                case 3:
                    return NamespaceName;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        public static ulong GetKeywords() { return 0; }
        public static string GetProviderName() { return UniversalEventsTraceEventParser.ProviderName; }
        public static Guid GetProviderGuid() { return UniversalEventsTraceEventParser.ProviderGuid; }
        private event Action<ProcessCreateTraceData> Action;
        #endregion
    }

    public sealed class ProcessExitTraceData : TraceEvent
    {
        public int ProcessId { get { return GetInt32At(0); } }

        #region Private
        internal ProcessExitTraceData(Action<ProcessExitTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
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
            set { Action = (Action<ProcessExitTraceData>)value; }
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttrib(sb, "ProcessId", ProcessId);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ProcessId" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ProcessId;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        public static ulong GetKeywords() { return 0; }
        public static string GetProviderName() { return UniversalEventsTraceEventParser.ProviderName; }
        public static Guid GetProviderGuid() { return UniversalEventsTraceEventParser.ProviderGuid; }
        private event Action<ProcessExitTraceData> Action;
        #endregion
    }

    public sealed class ProcessMappingTraceData : TraceEvent
    {
        public int Id { get { return GetInt32At(0); } }

        public int ProcessId { get { return GetInt32At(4); } }

        public Address StartAddress { get { return (Address)GetInt64At(8); } }

        public Address EndAddress { get { return (Address)GetInt64At(16); } }

        public Address FileOffset { get { return (Address)GetInt64At(24); } }

        public string FileName { get { return GetUnicodeStringAt(32); } }

        public string SymbolIndex { get { return GetUnicodeStringAt(SkipUnicodeString(32)); } }

        #region Private
        internal ProcessMappingTraceData(Action<ProcessMappingTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
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
            set { Action = (Action<ProcessMappingTraceData>)value; }
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttrib(sb, "Id", Id);
            XmlAttrib(sb, "ProcessId", ProcessId);
            XmlAttrib(sb, "StartAddress", StartAddress);
            XmlAttrib(sb, "EndAddress", EndAddress);
            XmlAttrib(sb, "FileOffset", FileOffset);
            XmlAttrib(sb, "FileName", FileName);
            XmlAttrib(sb, "SymbolIndex", SymbolIndex);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Id", "ProcessId", "StartAddress", "EndAddress", "FileOffset", "FileName", "SymbolIndex" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Id;
                case 1:
                    return ProcessId;
                case 2:
                    return StartAddress;
                case 3:
                    return EndAddress;
                case 4:
                    return FileOffset;
                case 5:
                    return FileName;
                case 6:
                    return SymbolIndex;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        public static ulong GetKeywords() { return 0; }
        public static string GetProviderName() { return UniversalEventsTraceEventParser.ProviderName; }
        public static Guid GetProviderGuid() { return UniversalEventsTraceEventParser.ProviderGuid; }
        private event Action<ProcessMappingTraceData> Action;
        #endregion
    }

    public sealed class ProcessSymbolTraceData : TraceEvent
    {
        public int Id { get { return GetInt32At(0); } }

        public int MappingId { get { return GetInt32At(4); } }

        public Address StartAddress { get { return (Address)GetInt64At(8); } }

        public Address EndAddress { get { return (Address)GetInt64At(16); } }

        public string Name { get { return GetUnicodeStringAt(24); } }

        #region Private
        internal ProcessSymbolTraceData(Action<ProcessSymbolTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
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
            set { Action = (Action<ProcessSymbolTraceData>)value; }
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttrib(sb, "Id", Id);
            XmlAttrib(sb, "MappingId", MappingId);
            XmlAttrib(sb, "StartAddress", StartAddress);
            XmlAttrib(sb, "EndAddress", EndAddress);
            XmlAttrib(sb, "Name", Name);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Id", "MappingId", "StartAddress", "EndAddress", "Name" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Id;
                case 1:
                    return MappingId;
                case 2:
                    return StartAddress;
                case 3:
                    return EndAddress;
                case 4:
                    return Name;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        public static ulong GetKeywords() { return 0; }
        public static string GetProviderName() { return UniversalEventsTraceEventParser.ProviderName; }
        public static Guid GetProviderGuid() { return UniversalEventsTraceEventParser.ProviderGuid; }
        private event Action<ProcessSymbolTraceData> Action;
        #endregion
    }
}
