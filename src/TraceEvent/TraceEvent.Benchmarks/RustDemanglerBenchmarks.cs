using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using Microsoft.Diagnostics.Symbols;

namespace TraceEventBenchmarks
{
    [MemoryDiagnoser]
    [EtwProfiler]
    public class RustDemanglerBenchmarks
    {
        private RustDemangler _demangler;
        private string[] _allSymbols;

        [GlobalSetup]
        public void Setup()
        {
            _demangler = new RustDemangler();
            _allSymbols = BenchmarkInput.RustSymbols;
        }

        [Benchmark]
        public string DemangleShort() => _demangler.Demangle(BenchmarkInput.RustShort);

        [Benchmark]
        public string DemangleMedium() => _demangler.Demangle(BenchmarkInput.RustMedium);

        [Benchmark]
        public string DemangleLong() => _demangler.Demangle(BenchmarkInput.RustLong);

        [Benchmark]
        public int DemangleAll()
        {
            int count = 0;
            var demangler = _demangler;
            foreach (string symbol in _allSymbols)
            {
                if (demangler.Demangle(symbol) != null)
                    count++;
            }
            return count;
        }
    }
}
