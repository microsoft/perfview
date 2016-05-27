using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Diagnostics.Tracing.Parsers
{
    public class LinuxKernelTraceEventParser : TraceEventParser
    {
        public static Guid ProviderGuid = new Guid("{c21d6c83-2462-45e1-bead-dffd90e6e223}");
        public static Guid LinuxProcessTaskGuid = new Guid("{ee3ae722-9c82-4262-9616-d0864ff0aa51}");
        public static string ProviderName = "Linux Kernel";
        private static TraceEvent[] s_templates;
        
        public LinuxKernelTraceEventParser(TraceEventSource source) : base(source)
        {
        }

        protected override string GetProviderName()
        {
            return ProviderName;
        }

        protected internal override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            if (s_templates == null)
            {
                TraceEvent[] templates = new TraceEvent[1];
                templates[0] = new SchedulerProcessTraceData(null, 0, 0, "Process", LinuxProcessTaskGuid, 1, "Start", ProviderGuid, ProviderName);

                s_templates = templates;
            }
            
            foreach (var template in s_templates)
            {
                if (template == null)
                    continue;
                if (eventsToObserve == null || eventsToObserve(template.ProviderName, template.EventName) == EventFilterResponse.AcceptEvent)
                    callback(template);
            }
        }


        public event Action<SchedulerProcessTraceData> ProcessStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SchedulerProcessTraceData(value, 0, 1, "Process", LinuxProcessTaskGuid, 1, "Start", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 1, LinuxProcessTaskGuid);
            }
        }
    }

    public abstract class CtfTraceData : TraceEvent
    {
        internal Ctf.CtfEventHeader CtfHeader { get; set; }
        internal Ctf.CtfEvent CtfEvent { get; set; }
        internal object[] Values { get; set; }

        public CtfTraceData(int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
        }
    }

    public class SchedulerProcessTraceData : CtfTraceData
    {
        private Action<SchedulerProcessTraceData> _action;
        private static string[] _payloadNames;

        public SchedulerProcessTraceData(Action<SchedulerProcessTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            _action = action;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (_payloadNames == null)
                    _payloadNames = CtfEvent.Fields.Fields.Select(f => f.Name).ToArray();

                return _payloadNames;
            }
        }

        protected internal override Delegate Target
        {
            get
            {
                return _action;
            }

            set
            {
                _action = (Action<SchedulerProcessTraceData>)value;
            }
        }

        public string ImageFileName
        {
            get
            {
                int i = 0;
                for (; i < PayloadNames.Length; i++)
                    if (PayloadNames[i] == "_filename")
                        break;

                return (string)Values[i];
            }
        }

        public override object PayloadValue(int index)
        {
            return Values[index];
        }

        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttrib(sb, "ProcessID", ProcessID);
            XmlAttrib(sb, "ImageFileName", ImageFileName);
            sb.Append("/>");
            return sb;
        }

        protected internal override void Dispatch()
        {
            _action(this);
        }
    }
}
