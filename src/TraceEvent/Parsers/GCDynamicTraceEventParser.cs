using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Microsoft.Diagnostics.Tracing.Parsers.GCDynamic;

namespace Microsoft.Diagnostics.Tracing.Parsers
{
    /// <summary>
    /// This parser is responsible for extracting the appropriate Dynamic Events and using their payloads and metadata to convert them into first-class events, the implications of which are that they can show up in the Events View.
    /// The implementation involves declaration of the dynamic events (look at CommittedUsageTraceEvent as an example), registration of the Metadata that'll match on the Microsoft-Windows-DotNETRuntime/GarbageCollection/GCDynamicEvent
    /// event based on the Name. Once the event is matched to the appropriate DynamicEvent implementation (if implemented), the appropriate event handler is invoked to dispatch the typed payload to any subscribers.
    /// The differentiating factor between dynamic events and traditional events is, therefore, this additional layer of extracting the payload after the metadata matches.
    /// To interface with these events, the user has to subscribe to one of the typed Dynamic Events such as the `CommittedUsageTraceEvent`. These can be publicly accessed via the ``Clr.GCDynamicEvent`` property.
    /// Additional fields such as the Payloads, Trace Event ID and other details have to be filled for the DynamicEvent to show up in the Events View.
    /// More Details:
    /// 1. There are two paths that can be invoked: one that's used when parsing events directly from the raw trace (e.g., etl or nettrace) and another when dealing directly with TraceLog after the etlx has been composed.
    /// 2. The scheme of the event ids of these involve making use of some decremented value starting from TraceEventID.Illegal - 10.
    /// </summary>
    public sealed class GCDynamicTraceEventParser : TraceEventParser
    {
        private static readonly string ProviderName = "Microsoft-Windows-DotNETRuntime";
        internal static readonly Guid ProviderGuid = new Guid(unchecked((int)0xe13c0d23), unchecked((short)0xccbc), unchecked((short)0x4e12), 0x93, 0x1b, 0xd9, 0xcc, 0x2e, 0xee, 0x27, 0xe4);
        private static readonly Guid GCTaskGuid = new Guid(unchecked((int)0x044973cd), unchecked((short)0x251f), unchecked((short)0x4dff), 0xa3, 0xe9, 0x9d, 0x63, 0x07, 0x28, 0x6b, 0x05);

        private static volatile TraceEvent[] s_templates;

        public GCDynamicTraceEventParser(TraceEventSource source) : base(source)
        {
            // These registrations are required for raw (non-TraceLog sources).
            // They ensure that Dispatch is called so that the specific event handlers are called for each event.
            ((ITraceParserServices)source).RegisterEventTemplate(GCDynamicTemplate(Dispatch, GCDynamicEventBase.GCDynamicTemplate));
            ((ITraceParserServices)source).RegisterEventTemplate(GCDynamicTemplate(Dispatch, GCDynamicEventBase.CommittedUsageTemplate));
        }

        protected override string GetProviderName()
        {
            return ProviderName;
        }

        protected internal override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            if (s_templates == null)
            {
                var templates = new TraceEvent[2];

                // This template ensures that all GC dynamic events are parsed properly.
                templates[0] = GCDynamicTemplate(null, GCDynamicEventBase.GCDynamicTemplate);

                // A template must be registered for each dynamic event type.  This ensures that after the event is converted
                // to its final form and saved in a TraceLog, that it can still be properly parsed and dispatched.
                templates[1] = GCDynamicTemplate(null, GCDynamicEventBase.CommittedUsageTemplate);

                s_templates = templates;
            }

            foreach (var template in s_templates)
                if (eventsToObserve == null || eventsToObserve(template.ProviderName, template.EventName) == EventFilterResponse.AcceptEvent)
                    callback(template);
        }

        private event Action<GCDynamicTraceEvent> _gcDynamicTraceEvent;
        public event Action<GCDynamicTraceEvent> GCDynamicTraceEvent
        {
            add
            {
                _gcDynamicTraceEvent += value;
            }
            remove
            {
                _gcDynamicTraceEvent -= value;
            }
        }

        private event Action<CommittedUsageTraceEvent> _gcCommittedUsage;
        public event Action<CommittedUsageTraceEvent> GCCommittedUsage
        {
            add
            {
                _gcCommittedUsage += value;
            }
            remove
            {
                _gcCommittedUsage -= value;
            }
        }

        /// <summary>
        /// Responsible for dispatching the event after we determine its type
        /// and parse it.
        /// </summary>
        private void Dispatch(GCDynamicTraceEventImpl data)
        {
            if (_gcCommittedUsage != null &&
                data.eventID == GCDynamicEventBase.CommittedUsageTemplate.ID)
            {
                _gcCommittedUsage(data.EventPayload as CommittedUsageTraceEvent);
            }

            else if (_gcDynamicTraceEvent != null &&
                data.EventPayload is GCDynamicTraceEvent)
            {
                _gcDynamicTraceEvent(data.EventPayload as GCDynamicTraceEvent);
            }
        }

        private static GCDynamicTraceEventImpl GCDynamicTemplate(Action<GCDynamicTraceEventImpl> action, GCDynamic.GCDynamicEventBase eventTemplate)
        {
            Debug.Assert(eventTemplate != null);

            // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
            return new GCDynamicTraceEventImpl(action, (int)eventTemplate.ID, 1, eventTemplate.TaskName, GCTaskGuid, 41, eventTemplate.OpcodeName, ProviderGuid, ProviderName);
        }
    }
}

namespace Microsoft.Diagnostics.Tracing.Parsers.GCDynamic
{
    internal sealed class GCDynamicTraceEventImpl : TraceEvent
    {
        internal GCDynamicTraceEventImpl(Action<GCDynamicTraceEventImpl> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            Action = action;
            NeedsFixup = true;
        }

        /// <summary>
        /// These are the raw payload fields of the underlying event.  TraceLog replay
        /// reuses templates and does not call <see cref="FixupData"/>, so replayed events
        /// decode the current layout on access.  Invalid layouts return type-appropriate
        /// safe defaults.
        /// </summary>
        internal string Name { get { return GetPayloadLayout()?.Name ?? string.Empty; } }
        internal Int32 DataSize { get { return GetPayloadLayout()?.DataSize ?? 0; } }
        internal byte[] Data
        {
            get
            {
                PayloadLayout? payload = GetPayloadLayout();
                return payload.HasValue ? GetByteArrayAt(offset: payload.Value.DataOffset, payload.Value.DataSize) : Array.Empty<byte>();
            }
        }
        internal int ClrInstanceID
        {
            get
            {
                PayloadLayout? payload = GetPayloadLayout();
                return payload.HasValue ? GetInt16At(payload.Value.ClrInstanceIDOffset) : 0;
            }
        }

        /// <summary>
        /// This gets run before each event is dispatched.  It is responsible for detecting the event type
        /// and selecting the correct event template (derived from GCDynamicEvent).
        ///
        /// The single <see cref="GCDynamicTraceEventImpl"/> instance is reused for every
        /// GCDynamic event the source produces, so the per-event payload validation
        /// must run here (not once at construction) -- it refreshes <see cref="_payload"/>
        /// for the raw event that's about to be converted and prevents an
        /// attacker-controlled DataSize from selecting an incompatible template.
        /// Payload accessors validate independently because TraceLog replay skips this
        /// method after the converted event type has been persisted.
        /// </summary>
        internal override void FixupData()
        {
            // Delete any per-event user data because we may mutate the event identity.
            EventTypeUserData = null;

            // Validate the payload layout for THIS event (instance is shared across all
            // GCDynamic events, so we must re-validate each time, not just the first).
            _payload = ReadPayloadLayout();

            // Set the event identity.
            SelectEventMetadata();
        }

        protected internal override void Dispatch()
        {
            Action(this);
        }

        protected internal override Delegate Target
        {
            get { return Action; }
            set { Action = (Action<GCDynamicTraceEventImpl>)value; }
        }

        private readonly CommittedUsageTraceEvent _committedUsageTemplate = new CommittedUsageTraceEvent();
        private readonly GCDynamicTraceEvent _gcDynamicTemplate = new GCDynamicTraceEvent();

        /// <summary>
        /// Contains the fully parsed payload of the dynamic event.
        /// </summary>
        public GCDynamicEventBase EventPayload
        {
            get
            {
                if (eventID == GCDynamicEventBase.CommittedUsageTemplate.ID)
                {
                    return _committedUsageTemplate.Bind(this);
                }

                return _gcDynamicTemplate.Bind(this);
            }
        }

        public override string[] PayloadNames
        {
            get
            {
                return EventPayload.PayloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            return EventPayload.PayloadValue(index);
        }

        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            foreach (KeyValuePair<string, object> pair in EventPayload.PayloadValues)
            {
                XmlAttrib(sb, pair.Key, pair.Value);
            }

            sb.Append("</Event>");
            return sb;
        }

        private event Action<GCDynamicTraceEventImpl> Action;

        // Per-event scratch state populated while converting a raw event to its final
        // event type. TraceLog replay templates never populate this field, so their
        // payload accessors decode each currently bound event instead.
        private PayloadLayout? _payload;

        private PayloadLayout? GetPayloadLayout()
        {
            return _payload ?? ReadPayloadLayout();
        }

        /// <summary>
        /// Bounds-checks the current event's payload against <see cref="TraceEvent.EventDataLength"/>
        /// and returns the validated layout (including the eagerly read Name string)
        /// on success, or <c>null</c> on failure.  Safe to call on attacker-controlled
        /// payloads: every read goes through bounds-aware primitives
        /// (<see cref="TraceEvent.GetUnicodeStringAt"/>, <see cref="TraceEvent.GetInt32At"/>)
        /// and the byte array is not materialised -- only its range is validated.
        /// </summary>
        private PayloadLayout? ReadPayloadLayout()
        {
            int eventDataLength = EventDataLength;

            // GetUnicodeStringAt requires at least one byte; a malformed event with
            // an empty payload trivially has nowhere to put the GCDynamic fields.
            if (eventDataLength < sizeof(char))
            {
                return null;
            }

            // GetUnicodeStringAt's read is bounded by EventDataLength.  If the buffer
            // contains no Unicode null terminator the returned string saturates at
            // the buffer end and the nameEndOffset computed below exceeds
            // eventDataLength, which the next range check rejects.
            string name = GetUnicodeStringAt(0);

            // Account for the Unicode null terminator after the name string.
            int nameEndOffset = (name.Length + 1) * sizeof(char);
            if (eventDataLength - nameEndOffset < sizeof(int))
            {
                return null;
            }

            int dataSize = GetInt32At(nameEndOffset);
            if (dataSize < 0)
            {
                return null;
            }

            int dataOffset = nameEndOffset + sizeof(int);
            int remainingBytesBeforeClrInstanceID = eventDataLength - dataOffset - sizeof(short);
            if (remainingBytesBeforeClrInstanceID < 0 || dataSize > remainingBytesBeforeClrInstanceID)
            {
                return null;
            }

            return new PayloadLayout(name, dataSize, dataOffset, dataOffset + dataSize);
        }

        private void SelectEventMetadata()
        {
            // Default to the generic template -- its accessors fall back to safe
            // placeholder values when _payload is null.
            GCDynamicEventBase eventTemplate = GCDynamicEventBase.GCDynamicTemplate;

            // Only select the CommittedUsage template when the payload validated AND
            // is large enough to satisfy CommittedUsageTraceEvent's fixed-offset
            // reads (Version through TotalBookkeepingCommitted occupy bytes 0..41).
            // A spoofed event named "CommittedUsage" with a shorter DataSize would
            // otherwise let BitConverter throw ArgumentException during PayloadValue
            // / ToXml on the dispatch hot path.
            if (_payload.HasValue
                && _payload.Value.Name.Equals(GCDynamicEventBase.CommittedUsageTemplate.OpcodeName, StringComparison.InvariantCultureIgnoreCase)
                && _payload.Value.DataSize >= CommittedUsageTraceEvent.MinimumDataSize)
            {
                eventTemplate = GCDynamicEventBase.CommittedUsageTemplate;
            }

            SetMetadataFromTemplate(eventTemplate);
        }

        private unsafe void SetMetadataFromTemplate(GCDynamicEventBase eventTemplate)
        {
            eventRecord->EventHeader.Id = (ushort)eventTemplate.ID;
            eventID = eventTemplate.ID;
            taskName = eventTemplate.TaskName;
            opcodeName = eventTemplate.OpcodeName;
            eventName = eventTemplate.EventName;
        }

        private struct PayloadLayout
        {
            public PayloadLayout(string name, int dataSize, int dataOffset, int clrInstanceIDOffset)
            {
                Name = name;
                DataSize = dataSize;
                DataOffset = dataOffset;
                ClrInstanceIDOffset = clrInstanceIDOffset;
            }

            public readonly string Name;
            public readonly int DataSize;
            public readonly int DataOffset;
            public readonly int ClrInstanceIDOffset;
        }
    }

    /// <summary>
    /// Template base class for a specific type of dynamic event.
    /// </summary>
    public abstract class GCDynamicEventBase
    {
        /// <summary>
        /// The list of specific event templates.
        /// </summary>
        internal static readonly GCDynamicTraceEvent GCDynamicTemplate = new GCDynamicTraceEvent();
        internal static readonly CommittedUsageTraceEvent CommittedUsageTemplate = new CommittedUsageTraceEvent();

        /// <summary>
        /// Metadata that must be specified for each specific type of dynamic event.
        /// </summary>
        internal abstract TraceEventID ID { get; }
        internal abstract string TaskName { get; }
        internal abstract string OpcodeName { get; }
        internal abstract string EventName { get; }

        /// <summary>
        /// Properties and methods that must be implemented in order to integrate with the TraceEvent class.
        /// </summary>
        internal abstract string[] PayloadNames { get; }
        internal abstract object PayloadValue(int index);
        internal abstract IEnumerable<KeyValuePair<string, object>> PayloadValues { get; }

        /// <summary>
        /// The underlying TraceEvent object that is bound to the template during dispatch.
        /// It contains a pointer to the actual event payload and is what's used to fetch and parse fields.
        /// </summary>
        internal GCDynamicTraceEventImpl UnderlyingEvent { get; private set; }

        /// <summary>
        /// The Data field from the underlying event.
        /// </summary>
        internal byte[] DataField
        {
            get { return UnderlyingEvent.Data; }
        }

        /// <summary>
        /// Binds this template to an underlying event before it is dispatched.
        /// This is what allows the template to be used to parse the event.
        /// </summary>
        internal GCDynamicEventBase Bind(GCDynamicTraceEventImpl underlyingEvent)
        {
            UnderlyingEvent = underlyingEvent;
            return this;
        }
    }

    public sealed class GCDynamicTraceEvent : GCDynamicEventBase
    {
        internal override TraceEventID ID => (TraceEventID)39;
        internal override string TaskName => "GC";
        internal override string OpcodeName => "DynamicTraceEvent";
        internal override string EventName => "GC/DynamicTraceEvent";

        private string[] _payloadNames;

        internal override string[] PayloadNames
        {
            get
            {
                if (_payloadNames == null)
                {
                    _payloadNames = new string[] { "Name", "DataSize", "Data", "ClrInstanceID" };
                }
                return _payloadNames;
            }
        }

        internal override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return UnderlyingEvent.Name;
                case 1:
                    return UnderlyingEvent.DataSize;
                case 2:
                    return UnderlyingEvent.Data;
                case 3:
                    return UnderlyingEvent.ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        internal override IEnumerable<KeyValuePair<string, object>> PayloadValues
        {
            get
            {
                yield return new KeyValuePair<string, object>("Name", UnderlyingEvent.Name);
                yield return new KeyValuePair<string, object>("DataSize", UnderlyingEvent.DataSize);
                yield return new KeyValuePair<string, object>("Data", string.Join(",", UnderlyingEvent.Data));
                yield return new KeyValuePair<string, object>("ClrInstanceID", UnderlyingEvent.ClrInstanceID);
            }
        }
    }

    public sealed class CommittedUsageTraceEvent : GCDynamicEventBase
    {
        /// <summary>
        /// The minimum number of bytes the Data payload must contain in order for
        /// all CommittedUsage fields (Version through TotalBookkeepingCommitted)
        /// to be readable.  Used by the GCDynamic dispatcher to reject spoofed
        /// "CommittedUsage" events whose DataSize is too small, which would
        /// otherwise cause BitConverter to throw ArgumentException during
        /// PayloadValue / ToXml and abort trace processing.
        /// </summary>
        internal const int MinimumDataSize = 42;

        public short Version { get { return GetInt16(0); } }
        public long TotalCommittedInUse { get { return GetInt64(2); } }
        public long TotalCommittedInGlobalDecommit { get { return GetInt64(10); } }
        public long TotalCommittedInFree { get { return GetInt64(18); } }
        public long TotalCommittedInGlobalFree { get { return GetInt64(26); } }
        public long TotalBookkeepingCommitted { get { return GetInt64(34); } }

        internal override TraceEventID ID => TraceEventID.Illegal - 11;
        internal override string TaskName => "GC";
        internal override string OpcodeName => "CommittedUsage";
        internal override string EventName => "GC/CommittedUsage";

        private string[] _payloadNames;

        internal override string[] PayloadNames
        {
            get
            {
                if (_payloadNames == null)
                {
                    _payloadNames = new string[] { "Version", "TotalCommittedInUse", "TotalCommittedInGlobalDecommit", "TotalCommittedInFree", "TotalCommittedInGlobalFree", "TotalBookkeepingCommitted" };
                }

                return _payloadNames;
            }
        }

        internal override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Version;
                case 1:
                    return TotalCommittedInUse;
                case 2:
                    return TotalCommittedInGlobalDecommit;
                case 3:
                    return TotalCommittedInFree;
                case 4:
                    return TotalCommittedInGlobalFree;
                case 5:
                    return TotalBookkeepingCommitted;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        internal override IEnumerable<KeyValuePair<string, object>> PayloadValues
        {
            get
            {
                yield return new KeyValuePair<string, object>("Version", Version);
                yield return new KeyValuePair<string, object>("TotalCommittedInUse", TotalCommittedInUse);
                yield return new KeyValuePair<string, object>("TotalCommittedInGlobalDecommit", TotalCommittedInGlobalDecommit);
                yield return new KeyValuePair<string, object>("TotalCommittedInFree", TotalCommittedInFree);
                yield return new KeyValuePair<string, object>("TotalCommittedInGlobalFree", TotalCommittedInGlobalFree);
                yield return new KeyValuePair<string, object>("TotalBookkeepingCommitted", TotalBookkeepingCommitted);
            }
        }

        private short GetInt16(int offset)
        {
            byte[] data = DataField;
            return data.Length >= offset + sizeof(short) ? BitConverter.ToInt16(data, offset) : (short)0;
        }

        private long GetInt64(int offset)
        {
            byte[] data = DataField;
            return data.Length >= offset + sizeof(long) ? BitConverter.ToInt64(data, offset) : 0;
        }
    }

    public sealed class CommittedUsage
    {
        public short Version { get; internal set; }
        public long TotalCommittedInUse { get; internal set; }
        public long TotalCommittedInGlobalDecommit { get; internal set; }
        public long TotalCommittedInFree { get; internal set; }
        public long TotalCommittedInGlobalFree { get; internal set; }
        public long TotalBookkeepingCommitted { get; internal set; }
    }

    public sealed class GCDynamicEvent
    {
        public GCDynamicEvent(string name, DateTime timeStamp, byte[] payload)
        {
            Name = name;
            TimeStamp = timeStamp;
            Payload = payload;
        }

        public string Name { get; internal set; }
        public DateTime TimeStamp { get; internal set; }
        public byte[] Payload { get; internal set; }
    }
}
