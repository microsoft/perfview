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
            ((ITraceParserServices)source).RegisterEventTemplate(GCDynamicTemplate(Dispatch, GCDynamicEvent.RawDynamicTemplate));
            ((ITraceParserServices)source).RegisterEventTemplate(GCDynamicTemplate(Dispatch, GCDynamicEvent.HeapCountTuningTemplate));
            ((ITraceParserServices)source).RegisterEventTemplate(GCDynamicTemplate(Dispatch, GCDynamicEvent.CommittedUsageTemplate));
            ((ITraceParserServices)source).RegisterEventTemplate(GCDynamicTemplate(Dispatch, GCDynamicEvent.HeapCountSampleTemplate));
        }

        protected override string GetProviderName()
        {
            return ProviderName;
        }

        protected internal override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            if (s_templates == null)
            {
                var templates = new TraceEvent[4];

                // This template ensures that all GC dynamic events are parsed properly.
                templates[0] = GCDynamicTemplate(null, GCDynamicEvent.RawDynamicTemplate);

                // A template must be registered for each dynamic event type.  This ensures that after the event is converted
                // to its final form and saved in a TraceLog, that it can still be properly parsed and dispatched.
                templates[1] = GCDynamicTemplate(null, GCDynamicEvent.HeapCountTuningTemplate);
                templates[2] = GCDynamicTemplate(null, GCDynamicEvent.CommittedUsageTemplate);
                templates[3] = GCDynamicTemplate(null, GCDynamicEvent.HeapCountSampleTemplate);

                s_templates = templates;
            }

            foreach (var template in s_templates)
                if (eventsToObserve == null || eventsToObserve(template.ProviderName, template.EventName) == EventFilterResponse.AcceptEvent)
                    callback(template);
        }

        /// <summary>
        /// Do not use.  This is here to avoid asserts that detect undeclared event templates.
        /// </summary>
        public event Action<GCDynamicTraceEvent> GCDynamicTraceEvent
        {
            add
            {
                throw new NotSupportedException();
            }
            remove
            {
                throw new NotSupportedException();
            }
        }

        private event Action<HeapCountTuningTraceEvent> _gcHeapCountTuning;
        public event Action<HeapCountTuningTraceEvent> GCHeapCountTuning
        {
            add
            {
                _gcHeapCountTuning += value;
            }
            remove
            {
                _gcHeapCountTuning -= value;
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

        private event Action<HeapCountSampleTraceEvent> _gcHeapCountSample;
        public event Action<HeapCountSampleTraceEvent> GCHeapCountSample
        {
            add
            {
                _gcHeapCountSample += value;
            }
            remove
            {
                _gcHeapCountSample -= value;
            }
        }

        /// <summary>
        /// Responsible for dispatching the event after we determine its type
        /// and parse it.
        /// </summary>
        private void Dispatch(GCDynamicTraceEvent data)
        {
            if (_gcHeapCountTuning != null &&
                data.eventID == GCDynamicEvent.HeapCountTuningTemplate.ID)
            {
                _gcHeapCountTuning(data.EventPayload as HeapCountTuningTraceEvent);
            }

            else if (_gcCommittedUsage != null && 
                data.eventID == GCDynamicEvent.CommittedUsageTemplate.ID)
            {
                _gcCommittedUsage(data.EventPayload as CommittedUsageTraceEvent);
            }

            else if (_gcHeapCountSample != null && 
                data.eventID == GCDynamicEvent.HeapCountSampleTemplate.ID)
            {
                _gcHeapCountSample(data.EventPayload as HeapCountSampleTraceEvent);
            }
        }

        private static GCDynamicTraceEvent GCDynamicTemplate(Action<GCDynamicTraceEvent> action, GCDynamic.GCDynamicEvent eventTemplate)
        {
            Debug.Assert(eventTemplate != null);

            // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
            return new GCDynamicTraceEvent(action, (int) eventTemplate.ID, 1, eventTemplate.TaskName, GCTaskGuid, 41, eventTemplate.OpcodeName, ProviderGuid, ProviderName);
        }
    }
}

namespace Microsoft.Diagnostics.Tracing.Parsers.GCDynamic
{
    public sealed class GCDynamicTraceEvent : TraceEvent
    {
        internal GCDynamicTraceEvent(Action<GCDynamicTraceEvent> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            Action = action;
            NeedsFixup = true;
        }

        /// <summary>
        /// These are the raw payload fields of the underlying event.
        /// </summary>
        internal string Name { get { return GetUnicodeStringAt(0); } }
        internal Int32 DataSize { get { return GetInt32At(SkipUnicodeString(0)); } }
        internal byte[] Data { get { return GetByteArrayAt(offset: SkipUnicodeString(0) + 4, DataSize); } }
        internal int ClrInstanceID { get { return GetInt16At(SkipUnicodeString(0) + 4 + DataSize); } }

        /// <summary>
        /// This gets run before each event is dispatched.  It is responsible for detecting the event type
        /// and selecting the correct event template (derived from GCDynamicEvent).
        /// </summary>
        internal override void FixupData()
        {
            // Delete any per-event user data because we may mutate the event identity.
            EventTypeUserData = null;

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
            set { Action = (Action<GCDynamicTraceEvent>)value; }
        }

        private readonly HeapCountTuningTraceEvent _heapCountTuningTemplate = new HeapCountTuningTraceEvent();
        private readonly CommittedUsageTraceEvent _committedUsageTemplate = new CommittedUsageTraceEvent();
        private readonly HeapCountSampleTraceEvent _heapCountSampleTemplate = new HeapCountSampleTraceEvent();
        private readonly RawDynamicTraceData _rawTemplate = new RawDynamicTraceData();

        /// <summary>
        /// Contains the fully parsed payload of the dynamic event.
        /// </summary>
        public GCDynamicEvent EventPayload
        {
            get
            {
                if (eventID == GCDynamicEvent.HeapCountTuningTemplate.ID)
                {
                    return _heapCountTuningTemplate.Bind(this);
                }

                else if (eventID == GCDynamicEvent.CommittedUsageTemplate.ID)
                {
                    return _committedUsageTemplate.Bind(this);
                }

                else if (eventID == GCDynamicEvent.HeapCountSampleTemplate.ID)
                {
                    return _heapCountSampleTemplate.Bind(this);
                }

                return _rawTemplate.Bind(this);
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

        private event Action<GCDynamicTraceEvent> Action;

        private void SelectEventMetadata()
        {
            GCDynamicEvent eventTemplate = GCDynamicEvent.RawDynamicTemplate;

            if (Name.Equals(GCDynamicEvent.HeapCountTuningTemplate.OpcodeName, StringComparison.InvariantCultureIgnoreCase))
            {
                eventTemplate = GCDynamicEvent.HeapCountTuningTemplate;
            }

            else if (Name.Equals(GCDynamicEvent.CommittedUsageTemplate.OpcodeName, StringComparison.InvariantCultureIgnoreCase))
            {
                eventTemplate = GCDynamicEvent.CommittedUsageTemplate;
            }

            else if (Name.Equals(GCDynamicEvent.HeapCountSampleTemplate.OpcodeName, StringComparison.InvariantCultureIgnoreCase))
            {
                eventTemplate = GCDynamicEvent.HeapCountSampleTemplate;
            }

            SetMetadataFromTemplate(eventTemplate);
        }

        private unsafe void SetMetadataFromTemplate(GCDynamicEvent eventTemplate)
        {
            eventRecord->EventHeader.Id = (ushort)eventTemplate.ID;
            eventID = eventTemplate.ID;
            taskName = eventTemplate.TaskName;
            opcodeName = eventTemplate.OpcodeName;
            eventName = eventTemplate.EventName;
        }
    }

    /// <summary>
    /// Template base class for a specific type of dynamic event.
    /// </summary>
    public abstract class GCDynamicEvent
    {
        /// <summary>
        /// The list of specific event templates.
        /// </summary>
        internal static readonly RawDynamicTraceData RawDynamicTemplate = new RawDynamicTraceData();
        internal static readonly HeapCountTuningTraceEvent HeapCountTuningTemplate = new HeapCountTuningTraceEvent();
        internal static readonly CommittedUsageTraceEvent CommittedUsageTemplate = new CommittedUsageTraceEvent();
        internal static readonly HeapCountSampleTraceEvent HeapCountSampleTemplate = new HeapCountSampleTraceEvent();

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
        internal GCDynamicTraceEvent UnderlyingEvent { get; private set; }

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
        internal GCDynamicEvent Bind(GCDynamicTraceEvent underlyingEvent)
        {
            UnderlyingEvent = underlyingEvent;
            return this;
        }
    }

    internal sealed class RawDynamicTraceData : GCDynamicEvent
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

    public sealed class HeapCountTuningTraceEvent : GCDynamicEvent
    {
        public short Version { get { return BitConverter.ToInt16(DataField, 0); } }
        public short NewHeapCount { get { return BitConverter.ToInt16(DataField, 2); } }
        public long GCIndex { get { return BitConverter.ToInt64(DataField, 4); } }
        public float MedianThroughputCostPercent { get { return BitConverter.ToSingle(DataField, 12); } }
        public float SmoothedMedianThroughputCostPercent { get { return BitConverter.ToSingle(DataField, 16); } }
        public float ThroughputCostPercentReductionPerStepUp { get { return BitConverter.ToSingle(DataField, 20); } }
        public float ThroughputCostPercentIncreasePerStepDown { get { return BitConverter.ToSingle(DataField, 24); } }
        public float SpaceCostPercentIncreasePerStepUp { get { return BitConverter.ToSingle(DataField, 28); } }
        public float SpaceCostPercentDecreasePerStepDown { get { return BitConverter.ToSingle(DataField, 32); } }

        internal override TraceEventID ID => TraceEventID.Illegal - 10;
        internal override string TaskName => "GC";
        internal override string OpcodeName => "HeapCountTuning";
        internal override string EventName => "GC/HeapCountTuning";

        private string[] _payloadNames;

        internal override string[] PayloadNames
        {
            get
            {
                if (_payloadNames == null)
                {
                    _payloadNames = new string[] { "Version", "NewHeapCount", "GCIndex", "MedianThroughputCostPercent", "SmoothedMedianThroughputCostPercent", "ThroughputCostPercentReductionPerStepUp", "ThroughputCostPercentIncreasePerStepDown", "SpaceCostPercentIncreasePerStepUp", "SpaceCostPercentDecreasePerStepDown" };
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
                    return NewHeapCount;
                case 2:
                    return GCIndex;
                case 3:
                    return Math.Round((decimal)MedianThroughputCostPercent, 3);
                case 4:
                    return Math.Round((decimal)SmoothedMedianThroughputCostPercent, 3);
                case 5:
                    return Math.Round((decimal)ThroughputCostPercentReductionPerStepUp, 3);
                case 6:
                    return Math.Round((decimal)ThroughputCostPercentIncreasePerStepDown, 3);
                case 7:
                    return Math.Round((decimal)SpaceCostPercentIncreasePerStepUp, 3);
                case 8:
                    return Math.Round((decimal)SpaceCostPercentDecreasePerStepDown, 3);
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
                yield return new KeyValuePair<string, object>("NewHeapCount", NewHeapCount);
                yield return new KeyValuePair<string, object>("GCIndex", GCIndex);
                yield return new KeyValuePair<string, object>("MedianThroughputCostPercent", Math.Round((decimal)MedianThroughputCostPercent, 3));
                yield return new KeyValuePair<string, object>("SmoothedMedianThroughputCostPercent", Math.Round((decimal)SmoothedMedianThroughputCostPercent, 3));
                yield return new KeyValuePair<string, object>("ThroughputCostPercentReductionPerStepUp", Math.Round((decimal)ThroughputCostPercentReductionPerStepUp, 3));
                yield return new KeyValuePair<string, object>("ThroughputCostPercentIncreasePerStepDown", Math.Round((decimal)ThroughputCostPercentIncreasePerStepDown, 3));
                yield return new KeyValuePair<string, object>("SpaceCostPercentIncreasePerStepUp", Math.Round((decimal)SpaceCostPercentIncreasePerStepUp, 3));
                yield return new KeyValuePair<string, object>("SpaceCostPercentDecreasePerStepDown", Math.Round((decimal)SpaceCostPercentDecreasePerStepDown, 3));
            }
        }
    }

    public sealed class HeapCountTuning
    {
        public short Version { get; internal set; }
        public short NewHeapCount { get; internal set; }
        public long GCIndex { get; internal set; }
        public float MedianThroughputCostPercent { get; internal set; }
        public float SmoothedMedianThroughputCostPercent { get; internal set; }
        public float ThroughputCostPercentReductionPerStepUp { get; internal set; }
        public float ThroughputCostPercentIncreasePerStepDown { get; internal set; }
        public float SpaceCostPercentIncreasePerStepUp { get; internal set; }
        public float SpaceCostPercentDecreasePerStepDown { get; internal set; }
    }

    public sealed class CommittedUsageTraceEvent : GCDynamicEvent
    {
        public short Version { get { return BitConverter.ToInt16(DataField, 0); } }
        public long TotalCommittedInUse { get { return BitConverter.ToInt64(DataField, 2); } }
        public long TotalCommittedInGlobalDecommit { get { return BitConverter.ToInt64(DataField, 10); } }
        public long TotalCommittedInFree { get { return BitConverter.ToInt64(DataField, 18); } }
        public long TotalCommittedInGlobalFree { get { return BitConverter.ToInt64(DataField, 26); } }
        public long TotalBookkeepingCommitted { get { return BitConverter.ToInt64(DataField, 34); } }

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

    public sealed class HeapCountSampleTraceEvent : GCDynamicEvent
    {
        public short Version { get { return BitConverter.ToInt16(DataField, 0); }}
        public long GCIndex { get { return BitConverter.ToInt64(DataField, 2); }}
        public long ElapsedTimeBetweenGCs { get { return BitConverter.ToInt64(DataField, 10); }}
        public long GCPauseTime { get { return BitConverter.ToInt64(DataField, 18); }}
        public long MslWaitTime { get { return BitConverter.ToInt64(DataField, 26); }}

        internal override TraceEventID ID => TraceEventID.Illegal - 12;
        internal override string TaskName => "GC";
        internal override string OpcodeName => "HeapCountSample";
        internal override string EventName => "GC/HeapCountSample";

        private string[] _payloadNames;

        internal override string[] PayloadNames
        {
            get
            {
                if (_payloadNames == null)
                {
                    _payloadNames = new string[] { "Version", "GCIndex", "ElapsedTimeBetweenGCs", "GCPauseTime", "MslWaitTime" };
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
                    return GCIndex;
                case 2:
                    return ElapsedTimeBetweenGCs;
                case 3:
                    return GCPauseTime;
                case 4:
                    return MslWaitTime;
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
                yield return new KeyValuePair<string, object>("GCIndex", GCIndex);
                yield return new KeyValuePair<string, object>("ElapsedTimeBetweenGCs", ElapsedTimeBetweenGCs);
                yield return new KeyValuePair<string, object>("GCPauseTime", GCPauseTime);
                yield return new KeyValuePair<string, object>("MslWaitTime", MslWaitTime);
            }
        }
    }

    public sealed class HeapCountSample
    {
        public short Version { get; internal set; }
        public long GCIndex { get; internal set; }
        public double ElapsedTimeBetweenGCsMSec { get; internal set; }
        public double GCPauseTimeMSec { get; internal set; }
        public double MslWaitTimeMSec { get; internal set; }
    }
}