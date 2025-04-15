using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Symbols
{
    internal interface ISymbolLookup
    {
        string FindNameForRva(uint rva);

    }
}
