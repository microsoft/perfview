using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Tracing.Compatibility
{
#if NETSTANDARD1_6

    // Design based on Core CLR 2.0.0
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

    internal static class Extentions
    {
        public static IntPtr GetHandle(this Process process)
        {
#if NETSTANDARD1_6
            return process.SafeHandle.DangerousGetHandle();
#else
            return process.Handle;
#endif
        }

#if NET462
        public static StringDictionary GetEnvironment(this ProcessStartInfo startInfo) => startInfo.EnvironmentVariables;
#else
        public static IDictionary<string, string> GetEnvironment(this ProcessStartInfo startInfo) => startInfo.Environment;
#endif
    }
}