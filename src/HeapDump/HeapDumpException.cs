using System;

namespace Microsoft.Diagnostics.HeapDump
{
#pragma warning disable RCS1194
    public class HeapDumpException : ApplicationException
#pragma warning restore RCS1194
    {
        public HeapDumpException(string message, HR hr) :
            base(message)
        {
            HResult = (int)hr;
        }
    }
}
