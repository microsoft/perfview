using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Text;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    [EventSource(Name = "Microsoft-Diagnostics-Tracing-AutomatedAnalysis")]
    internal sealed class AutomatedAnalysisEventSource : EventSource
    {
        internal static AutomatedAnalysisEventSource Log = new AutomatedAnalysisEventSource();

        [Event(1, Level = EventLevel.Error)]
        public void Error(string message)
        {
            WriteEvent(1, message);
        }

        [Event(2, Level = EventLevel.Verbose)]
        public void Verbose(string message)
        {
            WriteEvent(2, message);
        }
    }

    /// <summary>
    /// TextWriter implementation that allows TraceEvent constructions to log to EventSource along with other AutomatedAnalysis logging.
    /// </summary>
    public sealed class AutomatedAnalysisTextWriter : TextWriter
    {
        [ThreadStatic]
        private static StringBuilder _builder;

        public static AutomatedAnalysisTextWriter Instance = new AutomatedAnalysisTextWriter();

        private AutomatedAnalysisTextWriter()
        {
        }

        public override Encoding Encoding => Encoding.Unicode;

        public override void Write(char value)
        {
            if (_builder == null)
            {
                _builder = new StringBuilder();
            }

            // If the input value is a newline, flush the buffer.
            if (value == '\n' && _builder.Length > 0)
            {
                AutomatedAnalysisEventSource.Log.Verbose(_builder.ToString());
                _builder.Clear();
                return;
            }

            if (value != '\r')
            {
                _builder.Append(value);
            }
        }
    }
}
