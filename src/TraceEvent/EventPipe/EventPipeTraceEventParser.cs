using FastSerialization;
using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    public sealed class EventPipeTraceEventParser : ExternalTraceEventParser
    {
        public EventPipeTraceEventParser(TraceEventSource source, bool dontRegister = false)
            : base(source, dontRegister)
        {
        }

        #region Override ExternalTraceEventParser
        internal override DynamicTraceEventData TryLookup(TraceEvent unknownEvent)
        {
            if (unknownEvent.IsClassicProvider)
            {
                return null;
            }

            EventPipeEventSource eventPipeSource = source as EventPipeEventSource;
            DynamicTraceEventData template = null;
            if(eventPipeSource != null)
            {
                eventPipeSource.TryGetTemplateFromMetadata(unknownEvent, out template);
            }
            
            return template;
        }
        #endregion
    }
}
