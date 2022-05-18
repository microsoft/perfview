﻿using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.TraceUtilities.FilterQueryExpression
{
    public static class FilterQueryUtilities
    {
        internal static readonly Dictionary<string, Func<TraceEvent, string>> _specialPayload = new Dictionary<string, Func<TraceEvent, string>>
            (StringComparer.OrdinalIgnoreCase)
        {
            { "ThreadID",              (TraceEvent e) => e.ThreadID.ToString()              },
            { "ProcessID",             (TraceEvent e) => e.ProcessID.ToString()             },
            { "ProcessName",           (TraceEvent e) => e.ProcessName.ToString()           },
            { "ProcessorNumber",       (TraceEvent e) => e.ProcessorNumber.ToString()       },
            { "TimeStampRelativeMSec", (TraceEvent e) => e.TimeStampRelativeMSec.ToString() },
        };

        internal static string ExtractPayloadByName(TraceEvent @event, string payloadName)
        {
            if (_specialPayload.TryGetValue(payloadName, out var func))
            {
                return func(@event);
            }

            return @event.PayloadByName(payloadName)?.ToString(); 
        }
    }
}