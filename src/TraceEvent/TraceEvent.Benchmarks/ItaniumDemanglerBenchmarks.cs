using BenchmarkDotNet.Attributes;
using Microsoft.Diagnostics.Symbols;

namespace TraceEventBenchmarks
{
    [MemoryDiagnoser]
    public class ItaniumDemanglerBenchmarks
    {
        private ItaniumDemangler _demangler;
        private string[] _allSymbols;

        [GlobalSetup]
        public void Setup()
        {
            _demangler = new ItaniumDemangler();
            _allSymbols = BenchmarkInput.CppSymbols;
        }

        [Benchmark]
        public string DemangleShort() => _demangler.Demangle(BenchmarkInput.CppShort);

        [Benchmark]
        public string DemangleMedium() => _demangler.Demangle(BenchmarkInput.CppMedium);

        [Benchmark]
        public string DemangleLong() => _demangler.Demangle(BenchmarkInput.CppLong);

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
