using System;
using System.Diagnostics;
using System.Text;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    public sealed class SampleProfilerTraceEventParser : TraceEventParser
    {
        // NOTE: It's not a real EventSource provider
        public static string ProviderName = "Microsoft-DotNETCore-SampleProfiler";
        // {3c530d44-97ae-513a-1e6d-783e8f8e03a9}
        public static Guid ProviderGuid = new Guid(unchecked((int)0x3c530d44), unchecked((short)0x97ae), unchecked((short)0x513a), 0x1e, 0x6d, 0x78, 0x3e, 0x8f, 0x8e, 0x03, 0xa9);

        public SampleProfilerTraceEventParser(TraceEventSource source) : base(source) { }

        public event Action<ClrThreadSampleTraceData> ThreadSample
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                RegisterTemplate(new ClrThreadSampleTraceData(value, 0, 0, "Thread", Guid.Empty, 10, "Sample", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 0, ProviderGuid);
            }
        }

        // This is obsolete in the V3 version of the EventPipe format (released in .NET Core V2.1)  Can remove in 2019.  
        public event Action<ClrThreadStackWalkTraceData> ThreadStackWalk
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                RegisterTemplate(new ClrThreadStackWalkTraceData(value, 1, 0, "Thread", Guid.Empty, 11, "StackWalk", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 1, ProviderGuid);
            }
        }

        #region private
        protected override string GetProviderName() { return ProviderName; }
        private static volatile TraceEvent[] s_templates;
        protected internal override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            if (s_templates == null)
            {
                var templates = new TraceEvent[2];
                templates[0] = new ClrThreadSampleTraceData(null, 0, 0, "Thread", Guid.Empty, 1, "Sample", ProviderGuid, ProviderName);
                templates[1] = new ClrThreadStackWalkTraceData(null, 1, 0, "Thread", Guid.Empty, 2, "StackWalk", ProviderGuid, ProviderName);

                s_templates = templates;
            }
            foreach (var template in s_templates)
            {
                if (eventsToObserve == null || eventsToObserve(template.ProviderName, template.EventName) == EventFilterResponse.AcceptEvent)
                {
                    callback(template);
                }
            }
        }

        private void RegisterTemplate(TraceEvent template)
        {
            Debug.Assert(template.ProviderGuid == SampleProfilerTraceEventParser.ProviderGuid);
            source.RegisterEventTemplate(template);
        }
        #endregion
    }

    public enum ClrThreadSampleType
    {
        Error = 0,
        External = 1,
        Managed = 2
    }

    public sealed class ClrThreadSampleTraceData : TraceEvent
    {
        public ClrThreadSampleType Type
        {
            get
            {
                return (ClrThreadSampleType)GetInt32At(0);
            }
        }

        #region Private
        internal ClrThreadSampleTraceData(Action<ClrThreadSampleTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
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
            set { Action = (Action<ClrThreadSampleTraceData>)value; }
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(EventDataLength < 4));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttrib(sb, "Type", Type);
            sb.AppendLine(">");
            sb.Append("</Event>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "Type" };
                }

                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Type;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ClrThreadSampleTraceData> Action;
        #endregion
    }

    public sealed class ClrThreadStackWalkTraceData : TraceEvent
    {
        public int FrameCount
        {
            get
            {
                System.Diagnostics.Debug.Assert(EventDataLength % PointerSize == 0);
                return EventDataLength / PointerSize;
            }
        }

        /// <summary>
        /// Fetches the instruction pointer of a eventToStack frame 0 is the deepest frame, and the maximum should
        /// be a thread offset routine (if you get a complete eventToStack).  
        /// </summary>
        /// <param name="index">The index of the frame to fetch.  0 is the CPU EIP, 1 is the Caller of that
        /// routine ...</param>
        /// <returns>The instruction pointer of the specified frame.</returns>
        public Address InstructionPointer(int index)
        {
            return GetAddressAt(index * PointerSize);
        }

        /// <summary>
        /// Access to the instruction pointers as a unsafe memory blob
        /// </summary>
        internal unsafe void* InstructionPointers
        {
            get
            {
                return ((byte*)DataStart);
            }
        }

        #region Private
        internal ClrThreadStackWalkTraceData(Action<ClrThreadStackWalkTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
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
            set { Action = (Action<ClrThreadStackWalkTraceData>)value; }
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(EventDataLength < 4));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttrib(sb, "FrameCount", FrameCount);
            sb.AppendLine(">");
            for (int i = 0; i < FrameCount; i++)
            {
                sb.Append("  ");
                sb.Append("0x").Append(((ulong)InstructionPointer(i)).ToString("x"));
            }
            sb.AppendLine();
            sb.Append("</Event>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "FrameCount" };
                }

                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return FrameCount;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ClrThreadStackWalkTraceData> Action;
        #endregion
    }
}
