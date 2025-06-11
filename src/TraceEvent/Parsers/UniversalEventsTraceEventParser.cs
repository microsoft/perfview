using System;
using System.Diagnostics;
using System.Text;
using Address = System.UInt64;

#pragma warning disable 1591        // disable warnings on XML comments not being present

namespace Microsoft.Diagnostics.Tracing.Parsers
{
    using Microsoft.Diagnostics.Tracing.Parsers.Universal.Events;

    public sealed class UniversalEventsTraceEventParser: PredefinedDynamicTraceEventParser
    {
        public static string ProviderName = "Universal.Events";
        public static Guid ProviderGuid = new Guid("bc5e5d63-9799-5873-33d9-fba8316cef71");
        public enum Keywords : long
        {
            None = 0x0,
        };

        public UniversalEventsTraceEventParser(TraceEventSource source) : base(source) 
        {
            // Register templates for the universal events
            RegisterTemplate(new CpuSampleEvent());
            RegisterTemplate(new CswitchSampleEvent());
        }

        public event Action<CpuSampleEvent> cpu
        {
            add
            {
                AddCallbackForEvent<CpuSampleEvent>("cpu", value);
            }
            remove
            {
                RemoveCallback<CpuSampleEvent>(value);
            }
        }

        public event Action<CswitchSampleEvent> cswitch
        {
            add
            {
                AddCallbackForEvent<CswitchSampleEvent>("cswitch", value);
            }
            remove
            {
                RemoveCallback<CswitchSampleEvent>(value);
            }
        }

        #region private
        protected override string GetProviderName() { return ProviderName; }

        #endregion
    }
}

namespace Microsoft.Diagnostics.Tracing.Parsers.Universal.Events
{
    public sealed class CpuSampleEvent : PredefinedDynamicEvent
    {
        public CpuSampleEvent()
            : base("cpu", UniversalEventsTraceEventParser.ProviderGuid, UniversalEventsTraceEventParser.ProviderName)
        {
        }

        public Address Value { get { return GetVarUIntAt(0); } }

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

        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttrib(sb, "Value", Value);
            sb.Append("/>");
            return sb;
        }

        public static ulong GetKeywords() { return 0; }
        public static string GetProviderName() { return UniversalEventsTraceEventParser.ProviderName; }
        public static Guid GetProviderGuid() { return UniversalEventsTraceEventParser.ProviderGuid; }
    }

    public sealed class CswitchSampleEvent : PredefinedDynamicEvent
    {
        public CswitchSampleEvent()
            : base("cswitch", UniversalEventsTraceEventParser.ProviderGuid, UniversalEventsTraceEventParser.ProviderName)
        {
        }

        public Address Value { get { return GetVarUIntAt(0); } }

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

        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttrib(sb, "Value", Value);
            sb.Append("/>");
            return sb;
        }

        public static ulong GetKeywords() { return 0; }
        public static string GetProviderName() { return UniversalEventsTraceEventParser.ProviderName; }
        public static Guid GetProviderGuid() { return UniversalEventsTraceEventParser.ProviderGuid; }
    }

    // Keep the original SampleTraceData for backward compatibility
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
