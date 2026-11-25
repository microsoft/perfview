using System;

using BenchmarkDotNet.Running;

namespace TraceEventBenchmarks
{
    public class Program
    {
        private const string PeakMemorySwitch = "--async-profiler-peak-memory";

        public static int Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], PeakMemorySwitch, StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length != 2 ||
                    !Enum.TryParse(args[1], ignoreCase: true, out AsyncProfilerWorkloadProfile profile) ||
                    !Enum.IsDefined(typeof(AsyncProfilerWorkloadProfile), profile))
                {
                    Console.Error.WriteLine($"Usage: {PeakMemorySwitch} <Realistic|High|Extreme>");
                    return 1;
                }

                AsyncProfilerConversionPeakMemory.Measure(profile);
                return 0;
            }

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
            return 0;
        }
    }
}
