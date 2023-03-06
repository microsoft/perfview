using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Tracing.Compatibility
{
    internal static class Extentions
    {
        public static IntPtr GetHandle(this Process process)
        {
            return process.Handle;
        }

        public static IDictionary<string, string> GetEnvironment(this ProcessStartInfo startInfo) => startInfo.Environment;
    }
}