using Microsoft.Diagnostics.Utilities;
using System;
using System.Runtime.CompilerServices;

namespace System.Runtime.CompilerServices
{
    // File-local so friend assemblies can use their runtime's attribute without a type conflict.
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    file sealed class ModuleInitializerAttribute : Attribute
    {
    }
}

namespace Microsoft.Diagnostics.Tracing
{
    /// <summary>
    /// Enforces the minimum Windows version when the CLR initializes the TraceEvent module,
    /// including for library consumers that do not use PerfView's startup checks.
    /// Unsupported Windows hosts are rejected with PlatformNotSupportedException.
    /// Non-Windows hosts bypass the check without calling Windows APIs.
    /// </summary>
    internal static class PlatformInitializer
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            OperatingSystemVersion.EnsureSupported();
        }
    }
}
