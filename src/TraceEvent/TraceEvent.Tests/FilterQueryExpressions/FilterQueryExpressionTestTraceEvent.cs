using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;

namespace TraceEventTests.FilterQueryExpressions
{
    public sealed class FilterQueryExpressionTestTraceEvent : TraceEvent
    {
        public string Value { get; set; }

        public FilterQueryExpressionTestTraceEvent(string propertyName, string value, string eventName = "")
            : base(0, 0xFFFF, eventName, Guid.Empty, 0, "Fake", Guid.Empty, "Fake")
        {
            PayloadNames = new[] { propertyName };
            Value = value;
            taskName = eventName;
        }

        public FilterQueryExpressionTestTraceEvent(int eventID, int task, string taskName, System.Guid taskGuid, int opcode, string opcodeName, System.Guid providerGuid, string providerName) 
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName) {}

        public override string[] PayloadNames { get; } 

        // Not used.
        protected internal override System.Delegate Target { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public override object PayloadValue(int index)
        {
            if (index == 0)
            {
                return Value;
            }

            else
            {
                throw new System.ArgumentException(nameof(index));
            }
        }
    }
}
