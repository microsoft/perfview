using System.Runtime.InteropServices;
using Xunit;

namespace TraceEventTests
{
    public class WindowsFactAttribute : FactAttribute
    {
        public WindowsFactAttribute()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Skip = "Test requires Windows platform";
            }
        }
    }
}
