using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TraceEventTests.FilterQueryExpressions
{
    public sealed class FilterQueryExpressionTestTraceEvent : TraceEvent
    {
        private readonly List<Tuple<string, string>> _propertyNamesToValues;

        public FilterQueryExpressionTestTraceEvent(List<Tuple<string, string>> propertyNamesToValues, string eventName = "")
            : base(0, 0xFFFF, eventName, Guid.Empty, 0, "Fake", Guid.Empty, "Fake")
        {
            _propertyNamesToValues = propertyNamesToValues;
            PayloadNames = propertyNamesToValues.Select(p => p.Item1).ToArray();
            taskName = eventName;
        }

        public FilterQueryExpressionTestTraceEvent(string propertyName, string value, string eventName = "")
            : base(0, 0xFFFF, eventName, Guid.Empty, 0, "Fake", Guid.Empty, "Fake")
        {
            _propertyNamesToValues = new List<Tuple<string, string>>
            {
                {  Tuple.Create(propertyName, value) }
            };
            PayloadNames = _propertyNamesToValues.Select(p => p.Item1).ToArray();
            taskName = eventName;
        }

        public FilterQueryExpressionTestTraceEvent(int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName) 
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName) {}

        public override string[] PayloadNames { get; } 

        // Not used.
        protected internal override System.Delegate Target { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public override object PayloadValue(int index)
        {
            if (index <= -1 || index >= _propertyNamesToValues.Count)
            {
                throw new ArgumentException($"Invalid index: {index}");
            }

            else
            {
                return _propertyNamesToValues[index].Item2;
            }
        }
    }
}
