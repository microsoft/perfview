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
            RegisterTemplate(new SampleTraceData("cpu"));
            RegisterTemplate(new SampleTraceData("cswitch"));
        }

        public event Action<SampleTraceData> cpu
        {
            add
            {
                AddCallbackForEvent(
                    "cpu",
                    (TraceEvent data) => value((SampleTraceData)data)
                );
            }
            remove
            {
                throw new NotImplementedException();
            }
        }

        public event Action<SampleTraceData> cswitch
        {
            add
            {
                AddCallbackForEvent(
                    "cswitch",
                    (TraceEvent data) => value((SampleTraceData)data)
                );
            }
            remove
            {
                throw new NotImplementedException();
            }
        }

        #region private
        protected override string GetProviderName() { return ProviderName; }

        #endregion
    }
}

namespace Microsoft.Diagnostics.Tracing.Parsers.Universal.Events
{
    public sealed class SampleTraceData : PredefinedDynamicEvent
    {
        public SampleTraceData(string eventName)
            : base(eventName, UniversalEventsTraceEventParser.ProviderGuid, UniversalEventsTraceEventParser.ProviderName)
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
}
