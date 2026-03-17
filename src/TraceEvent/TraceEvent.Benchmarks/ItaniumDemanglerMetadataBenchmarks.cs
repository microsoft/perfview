using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using Microsoft.Diagnostics.Symbols;

namespace TraceEventBenchmarks
{
    [MemoryDiagnoser]
    [EtwProfiler]
    public class ItaniumDemanglerMetadataBenchmarks
    {
        private ItaniumDemangler _demangler;
        private string[] _allSymbols;

        [GlobalSetup]
        public void Setup()
        {
            _demangler = new ItaniumDemangler();
            _allSymbols = BenchmarkInput.CppSymbolsMetadata;
        }

        [Benchmark]
        public string DemangleShort() => _demangler.Demangle(BenchmarkInput.CppMetadataShort);

        [Benchmark]
        public string DemangleMedium() => _demangler.Demangle(BenchmarkInput.CppMetadataMedium);

        [Benchmark]
        public string DemangleLong() => _demangler.Demangle(BenchmarkInput.CppMetadataLong);

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
