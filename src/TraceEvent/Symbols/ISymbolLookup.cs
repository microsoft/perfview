namespace Microsoft.Diagnostics.Symbols
{
    /// <summary>
    /// Minimal requirements for symbol files used by TraceCodeAddresses.LookupSymbolsForModule.
    /// </summary>
    internal interface ISymbolLookup
    {
        string FindNameForRva(uint rva, ref uint symbolStart);
    }
}
