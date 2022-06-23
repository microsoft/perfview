using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.Diagnostics.Tracing.TraceUtilities.FilterQueryExpression
{
    public static class FilterQueryUtilities
    {
        public static readonly Regex FilterQueryExpressionRegex = new Regex(@"\[[^\]]*\]");
        public static readonly char[] SpaceSeparator = new[] { ' ' }; 

        public static string TryExtractFilterQueryExpression(string expression, out FilterQueryExpressionTree tree)
        {
            tree = null;

            // If the string is empty or the filter query expression isn't specified in [...].
            if (string.IsNullOrEmpty(expression) || !expression.Contains("[") || !expression.Contains("]"))
            {
                return expression;
            }

            var matched = FilterQueryExpressionRegex.Match(expression);
            expression  = FilterQueryExpressionRegex.Replace(expression, string.Empty);

            if (matched.Success)
            {
                var cleanedExpressionString = matched.Value.Replace("[", string.Empty).Replace("]", string.Empty);
                tree = new FilterQueryExpressionTree(cleanedExpressionString);
            }

            return expression;
        }

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