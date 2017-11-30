using System;

namespace Microsoft.Diagnostics.Tracing.Compatibility
{
#if NETSTANDARD1_6

    // Designed from Core CLR 2.0.0
    public class ApplicationException : Exception
    {
        internal const int COR_E_APPLICATION = unchecked((int)0x80131600);

        public ApplicationException()
            : base("Error in the application.")
        {
            HResult = COR_E_APPLICATION;
        }

        public ApplicationException(String message)
            : base(message)
        {
            HResult = COR_E_APPLICATION;
        }

        public ApplicationException(String message, Exception innerException)
            : base(message, innerException)
        {
            HResult = COR_E_APPLICATION;
        }
    }    
#endif
}