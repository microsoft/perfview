using System;
using System.Linq;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public static class Extensions
    {
        public static StackSource CPUStacks(this TraceLog eventLog, TraceProcess process = null, Predicate<TraceEvent> predicate = null)
        {
            TraceEvents events;
            if (process == null)
            {
                events = eventLog.Events.Filter((x) => ((predicate == null) || predicate(x)) && x is SampledProfileTraceData && x.ProcessID != 0);
            }
            else
            {
                events = process.EventsInProcess.Filter((x) => ((predicate == null) || predicate(x)) && x is SampledProfileTraceData);
            }

            var traceStackSource = new TraceEventStackSource(events);

            // We clone the samples so that we don't have to go back to the ETL file from here on.  
            return CopyStackSource.Clone(traceStackSource);
        }

        public static bool ManagedProcess(this TraceProcess process)
        {
            return process.LoadedModules.Any(module =>
                    module is TraceManagedModule ||
                    module.Name.Equals("clr", StringComparison.OrdinalIgnoreCase) ||
                    module.Name.Equals("coreclr", StringComparison.OrdinalIgnoreCase) ||
                    module.Name.Equals("mscorwks", StringComparison.OrdinalIgnoreCase) ||
                    module.Name.Equals("System.Private.CoreLib", StringComparison.OrdinalIgnoreCase));
        }
    }
}
