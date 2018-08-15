using System;

namespace Microsoft.Diagnostics.HeapDump
{
    public class HeapDumpException : ApplicationException
    {
        public HeapDumpException(string message, HR hr) :
            base(message)
        {
            HResult = (int)hr;
        }
    }
}
