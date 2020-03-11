//<autogenerated/>
using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text;
using Microsoft.Diagnostics.Tracing;
using Address = System.UInt64;

#pragma warning disable 1591        // disable warnings on XML comments not being present

// This code was automatically generated by the TraceParserGen tool, which converts
// an ETW event manifest into strongly typed C# classes.
namespace Microsoft.Diagnostics.Tracing.Parsers
{
    using Microsoft.Diagnostics.Tracing.Parsers.LinuxKernel;

    [System.CodeDom.Compiler.GeneratedCode("traceparsergen", "2.0")]
    public sealed class LinuxKernelEventParser : TraceEventParser 
    {
        public static string ProviderName = "Linux-Kernel";
        public static Guid ProviderGuid = new Guid(unchecked((int) 0x4da4eb17), unchecked((short) 0xcd7d), unchecked((short) 0x4b13), 0xb8, 0x07, 0x22, 0x90, 0x8d, 0x29, 0x96, 0xb5);
        internal static readonly Guid ProcessTaskGuid = new Guid(unchecked((int)0xa3041133), unchecked((short)0xbf9e), unchecked((short)0x4861), 0xa3, 0x1d, 0x60, 0xa2, 0xd6, 0x65, 0xfe, 0x6e);
        public enum Keywords : long
        {
            Process = 0x1,
        };

        public LinuxKernelEventParser(TraceEventSource source) : base(source) {}

        public event Action<ProcessStartTraceData> ProcessStart
        {
            add
            {
                source.RegisterEventTemplate(new ProcessStartTraceData(value, 1, 1, "Process", ProcessTaskGuid, 1, "Start", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 1, ProcessTaskGuid);
            }
        }
        public event Action<ProcessStopTraceData> ProcessStop
        {
            add
            {
                source.RegisterEventTemplate(new ProcessStopTraceData(value, 2, 1, "Process", ProcessTaskGuid, 2, "Stop", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 2, ProcessTaskGuid);
            }
        }

        #region private
        protected override string GetProviderName() { return ProviderName; }

        static private volatile TraceEvent[] s_templates;

/*        static private ProcessStartTraceData ProcessStartTemplate(Action<ProcessStartTraceData> action)
        {                  // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
            return new ProcessStartTraceData(action, 1, 1, "Process", Guid.Empty, 1, "Start", ProviderGuid, ProviderName);
        }
        static private ProcessStopTraceData ProcessStopTemplate(Action<ProcessStopTraceData> action)
        {                  // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
            return new ProcessStopTraceData(action, 2, 1, "Process", Guid.Empty, 2, "Stop", ProviderGuid, ProviderName);
        }*/

        protected internal override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            if (s_templates == null)
            {
                var templates = new TraceEvent[2];
                templates[0] = new ProcessStartTraceData(null, 1, 1, "Process", ProcessTaskGuid, 1, "Start", ProviderGuid, ProviderName);
                templates[1] = new ProcessStopTraceData(null, 2, 1, "Process", ProcessTaskGuid, 2, "Stop", ProviderGuid, ProviderName);
                s_templates = templates;
            }
            foreach (var template in s_templates)
                if (eventsToObserve == null || eventsToObserve(template.ProviderName, template.EventName) == EventFilterResponse.AcceptEvent)
                    callback(template);
        }

        #endregion
    }
}

namespace Microsoft.Diagnostics.Tracing.Parsers.LinuxKernel
{
    public sealed class ProcessStartTraceData : TraceEvent
    {
        #region Private
        internal ProcessStartTraceData(Action<ProcessStartTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            //Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(4)));
            //Debug.Assert(!(Version > 0 && EventDataLength < SkipUnicodeString(4)));
        }
        protected internal override Delegate Target
        {
            get { return Action; }
            set { Action = (Action<ProcessStartTraceData>) value; }
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             XmlAttrib(sb, "ProcessID", ProcessID);
             XmlAttrib(sb, "ProcessName", ProcessName);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ProcessID", "ProcessName"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ProcessID;
                case 1:
                    return ProcessName;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        public static ulong GetKeywords() { return 1; }
        public static string GetProviderName() { return "Linux-Kernel"; }
        public static Guid GetProviderGuid() { return new Guid("4da4eb17-cd7d-4b13-b807-22908d2996b5"); }
        private event Action<ProcessStartTraceData> Action;
        #endregion
    }
    public sealed class ProcessStopTraceData : TraceEvent
    {
/*      public int ProcessID { get { return GetInt32At(0); } }
        public string ProcessName { get { return GetUnicodeStringAt(4); } }*/

        #region Private
        internal ProcessStopTraceData(Action<ProcessStopTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            //Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(4)));
            //Debug.Assert(!(Version > 0 && EventDataLength < SkipUnicodeString(4)));
        }
        protected internal override Delegate Target
        {
            get { return Action; }
            set { Action = (Action<ProcessStopTraceData>) value; }
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             XmlAttrib(sb, "ProcessID", ProcessID);
             XmlAttrib(sb, "ProcessName", ProcessName);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ProcessID", "ProcessName"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ProcessID;
                case 1:
                    return ProcessName;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        public static ulong GetKeywords() { return 1; }
        public static string GetProviderName() { return "Linux-Kernel"; }
        public static Guid GetProviderGuid() { return new Guid("4da4eb17-cd7d-4b13-b807-22908d2996b5"); }
        private event Action<ProcessStopTraceData> Action;
        #endregion
    }
}