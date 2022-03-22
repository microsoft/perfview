using System;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    internal class AnalyzerExecutionScope : IDisposable
    {
        [ThreadStatic]
        private static AnalyzerExecutionScope _current;

        internal static AnalyzerExecutionScope Current
        {
            get { return _current; }
        }

        internal AnalyzerExecutionScope(Analyzer executingAnalyzer)
        {
            if (_current != null)
            {
                throw new InvalidOperationException();
            }

            ExecutingAnalyzer = executingAnalyzer;
            _current = this;
        }

        void IDisposable.Dispose()
        {
            _current = null;
        }

        internal Analyzer ExecutingAnalyzer { get; private set; }
    }
}
