namespace Microsoft.Diagnostics.HeapDump
{
    public enum HR
    {
        // Warning: Consumers of Microsoft.Diagnostics.HeapDump.dll rely on these being constant
        UnknownError = unchecked((int)(((ulong)(0x3) << 31) | ((ulong)(0x122) << 16) | ((ulong)(0x0)))),
        NoHeapFound = UnknownError + 1,
        CouldNotFindProcessId = UnknownError + 2,
        Opening32BitDumpIn64BitProcess = UnknownError + 3,
        Opening64BitDumpIn32BitProcess = UnknownError + 4,
        NoDotNetRuntimeFound = UnknownError + 5,
        CouldNotAccessDac = UnknownError + 6,
    }
}
