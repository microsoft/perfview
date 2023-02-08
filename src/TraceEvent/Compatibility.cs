using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Tracing.Compatibility
{
    internal static class Extentions
    {
#if NET462
        public static StringDictionary GetEnvironment(this ProcessStartInfo startInfo) => startInfo.EnvironmentVariables;
#else
        public static IDictionary<string, string> GetEnvironment(this ProcessStartInfo startInfo) => startInfo.Environment;
#endif
    }
}