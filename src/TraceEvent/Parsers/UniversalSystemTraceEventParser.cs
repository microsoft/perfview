using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        public event Action<EmptyTraceData> ProcessExit
        {
            add
            {
                source.RegisterEventTemplate(new EmptyTraceData(value, 2, (int)TraceEventTask.Default, "ProcessExit", Guid.Empty, 0, "Default", ProviderGuid, ProviderName));
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

        public event Action<ProcessMappingMetadataTraceData> ProcessMappingMetadata
        {
            add
            {
                source.RegisterEventTemplate(new ProcessMappingMetadataTraceData(value, 5, (int)TraceEventTask.Default, "ProcessMappingMetadata", Guid.Empty, 0, "Default", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 5, Guid.Empty);
            }
        }

        #region private
        protected override string GetProviderName() { return ProviderName; }

        static private volatile TraceEvent[] s_templates;

        protected internal override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            if (s_templates == null)
            {
                var templates = new TraceEvent[6];
                templates[0] = new ProcessCreateTraceData(null, 0, (int)TraceEventTask.Default, "ExistingProcess", Guid.Empty, 0, "Default", ProviderGuid, ProviderName);
                templates[1] = new ProcessCreateTraceData(null, 1, (int)TraceEventTask.Default, "ProcessCreate", Guid.Empty, 0, "Default", ProviderGuid, ProviderName);
                templates[2] = new EmptyTraceData(null, 2, (int)TraceEventTask.Default, "ProcessExit", Guid.Empty, 0, "Default", ProviderGuid, ProviderName);
                templates[3] = new ProcessMappingTraceData(null, 3, (int)TraceEventTask.Default, "ProcessMapping", Guid.Empty, 0, "Default", ProviderGuid, ProviderName);
                templates[4] = new ProcessSymbolTraceData(null, 4, (int)TraceEventTask.Default, "ProcessSymbol", Guid.Empty, 0, "Default", ProviderGuid, ProviderName);
                templates[5] = new ProcessMappingMetadataTraceData(null, 5, (int)TraceEventTask.Default, "ProcessMappingMetadata", Guid.Empty, 0, "Default", ProviderGuid, ProviderName);
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
        public ulong NamespaceId { get { return GetVarUIntAt(0); } }

        public string Name { get { return GetShortUTF8StringAt(SkipVarInt(0)); } }

        public string NamespaceName { get { return GetShortUTF8StringAt(SkipShortUTF8String(SkipVarInt(0))); } }

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
                    payloadNames = new string[] { "NamespaceId", "Name", "NamespaceName" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return NamespaceId;
                case 1:
                    return Name;
                case 2:
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

    public sealed class ProcessMappingTraceData : TraceEvent
    {
        public ulong Id { get { return GetVarUIntAt(0); } }

        public Address StartAddress { get { return GetVarUIntAt(SkipVarInt(0)); } }

        public Address EndAddress { get { return GetVarUIntAt(SkipVarInt(SkipVarInt(0))); } }

        public Address FileOffset { get { return GetVarUIntAt(SkipVarInt(SkipVarInt(SkipVarInt(0)))); } }

        public string FileName { get { return GetShortUTF8StringAt(SkipVarInt(SkipVarInt(SkipVarInt(SkipVarInt(0))))); } }

        public ulong MetadataId { get { return GetVarUIntAt(SkipShortUTF8String(SkipVarInt(SkipVarInt(SkipVarInt(SkipVarInt(0)))))); } }

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
            XmlAttrib(sb, "StartAddress", StartAddress);
            XmlAttrib(sb, "EndAddress", EndAddress);
            XmlAttrib(sb, "FileOffset", FileOffset);
            XmlAttrib(sb, "FileName", FileName);
            XmlAttrib(sb, "MetadataId", MetadataId);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Id", "StartAddress", "EndAddress", "FileOffset", "FileName", "MetadataId" };
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
                    return StartAddress;
                case 2:
                    return EndAddress;
                case 3:
                    return FileOffset;
                case 4:
                    return FileName;
                case 5:
                    return MetadataId;
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
        public ulong Id { get { return GetVarUIntAt(0); } }

        public ulong MappingId { get { return GetVarUIntAt(SkipVarInt(0)); } }

        public Address StartAddress { get { return GetVarUIntAt(SkipVarInt(SkipVarInt(0))); } }

        public Address EndAddress { get { return GetVarUIntAt(SkipVarInt(SkipVarInt(SkipVarInt(0)))); } }

        public string Name { get { return GetShortUTF8StringAt(SkipVarInt(SkipVarInt(SkipVarInt(SkipVarInt(0))))); } }

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

    public sealed class ProcessMappingMetadataTraceData : TraceEvent
    {
        public ulong Id { get { return GetVarUIntAt(0); } }

        public string SymbolMetadata {get { return GetShortUTF8StringAt(SkipVarInt(0)); } }

        internal ProcessMappingSymbolMetadata ParsedSymbolMetadata { get { return ProcessMappingSymbolMetadataParser.TryParse(SymbolMetadata); } }

        public string VersionMetadata {get {return GetShortUTF8StringAt(SkipShortUTF8String(SkipVarInt(0))); } }

        #region Private
        internal ProcessMappingMetadataTraceData(Action<ProcessMappingMetadataTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
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
            set { Action = (Action<ProcessMappingMetadataTraceData>)value; }
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttrib(sb, "Id", Id);
            XmlAttrib(sb, "SymbolMetadata", SymbolMetadata);
            XmlAttrib(sb, "VersionMetadata", VersionMetadata);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Id", "SymbolMetadata", "VersionMetadata" };
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
                    return SymbolMetadata;
                case 2:
                    return VersionMetadata;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        public static ulong GetKeywords() { return 0; }
        public static string GetProviderName() { return UniversalEventsTraceEventParser.ProviderName; }
        public static Guid GetProviderGuid() { return UniversalEventsTraceEventParser.ProviderGuid; }
        private event Action<ProcessMappingMetadataTraceData> Action;
        #endregion
    }

    /// <summary>
    /// Parser for ProcessMappingSymbolMetadata events.  The payload arrives in JSON format.
    /// </summary>
    internal static class ProcessMappingSymbolMetadataParser
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter(),
                new GuidConverter(),
                new ProcessMappingSymbolMetadataConverter()
            }
        };

        internal static ProcessMappingSymbolMetadata TryParse(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<ProcessMappingSymbolMetadata>(json, Options);
            }
            catch
            {
                return null;
            }
        }
    }

    [JsonConverter(typeof(ProcessMappingSymbolMetadataConverter))]
    internal abstract class ProcessMappingSymbolMetadata
    {
        [JsonPropertyName("type")]
        public ProcessMappingSymbolMetadataFileType Type { get; set; }
    }

    internal sealed class PEProcessMappingSymbolMetadata : ProcessMappingSymbolMetadata
    {
        [JsonPropertyName("name")]
        public string PdbName { get; set; }
        [JsonPropertyName("date_time")]
        public long DateTime { get; set; }
        [JsonPropertyName("age")]
        public int PdbAge { get; set; }
        [JsonPropertyName("signature")]
        public Guid PdbSignature { get; set; }
        [JsonPropertyName("perfmap_signature")]
        public Guid PerfmapSignature { get; set; }
        [JsonPropertyName("perfmap_version")]
        public int PerfmapVersion { get; set; }
        [JsonPropertyName("perfmap_name")]
        public string PerfmapName { get; set; }
        [JsonPropertyName("text_offset")]
        public ulong TextOffset { get; set; }
    }

    internal sealed class ELFProcessMappingSymbolMetadata : ProcessMappingSymbolMetadata
    {
        [JsonPropertyName("debug_link")]
        public string DebugLink { get; set; }
        [JsonPropertyName("build_id")]
        public string BuildId { get; set; }
    }

    internal enum ProcessMappingSymbolMetadataFileType
    {
        ELF,
        PE
    }

    internal class GuidConverter : JsonConverter<Guid>
    {
        public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Guid.TryParse(reader.GetString(), out Guid value);
            return value;
        }

        public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    internal class ProcessMappingSymbolMetadataConverter : JsonConverter<ProcessMappingSymbolMetadata>
    {
        public override ProcessMappingSymbolMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using (JsonDocument document = JsonDocument.ParseValue(ref reader))
            {
                JsonElement root = document.RootElement;
                string type = root.GetProperty("type").GetString();

                if (string.Equals("PE", type, StringComparison.OrdinalIgnoreCase))
                {
                    return JsonSerializer.Deserialize<PEProcessMappingSymbolMetadata>(root.GetRawText(), options);
                }
                else if (string.Equals("ELF", type, StringComparison.OrdinalIgnoreCase))
                {
                    return JsonSerializer.Deserialize<ELFProcessMappingSymbolMetadata>(root.GetRawText(), options);
                }
                else
                {
                    throw new JsonException($"Unknown type: {type}");
                }
            }
        }

        public override void Write(Utf8JsonWriter writer, ProcessMappingSymbolMetadata value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
