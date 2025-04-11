using System;
using System.Diagnostics;
using System.Text;
using Address = System.UInt64;

#pragma warning disable 1591        // disable warnings on XML comments not being present

namespace Microsoft.Diagnostics.Tracing.Parsers
{
    using Microsoft.Diagnostics.Tracing.Parsers.Universal.Events;

    public sealed class UniversalEventsTraceEventParser: TraceEventParser
    {
        public static string ProviderName = "Universal.Events";
        public static Guid ProviderGuid = new Guid("bc5e5d63-9799-5873-33d9-fba8316cef71");
        public enum Keywords : long
        {
            None = 0x0,
        };

        public UniversalEventsTraceEventParser(TraceEventSource source) : base(source) { }

        public event Action<SampleTraceData> cpu
        {
            add
            {
                source.RegisterEventTemplate(new SampleTraceData(value, 1, (int)TraceEventTask.Default, "cpu", Guid.Empty, 0, "Default", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 1, Guid.Empty);
            }
        }

        public event Action<SampleTraceData> cswitch
        {
            add
            {
                source.RegisterEventTemplate(new SampleTraceData(value, 2, (int)TraceEventTask.Default, "cswitch", Guid.Empty, 0, "Default", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 2, Guid.Empty);
            }
        }

        #region private
        protected override string GetProviderName() { return ProviderName; }

        static private volatile TraceEvent[] s_templates;

        protected internal override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            if (s_templates == null)
            {
                var templates = new TraceEvent[2];
                templates[0] = new SampleTraceData(null, 1, (int)TraceEventTask.Default, "cpu", Guid.Empty, 0, "Default", ProviderGuid, ProviderName);
                templates[1] = new SampleTraceData(null, 2, (int)TraceEventTask.Default, "cswitch", Guid.Empty, 0, "Default", ProviderGuid, ProviderName);
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
    public sealed class SampleTraceData : TraceEvent
    {
        public Address Value { get { return GetVarUIntAt(0); } }

        #region Private
        internal SampleTraceData(Action<SampleTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
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
            set { Action = (Action<SampleTraceData>)value; }
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttrib(sb, "Value", Value);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Value" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Value;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        public static ulong GetKeywords() { return 0; }
        public static string GetProviderName() { return UniversalEventsTraceEventParser.ProviderName; }
        public static Guid GetProviderGuid() { return UniversalEventsTraceEventParser.ProviderGuid; }
        private event Action<SampleTraceData> Action;
        #endregion
    }
}
