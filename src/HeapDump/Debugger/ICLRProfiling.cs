using System;
using System.Runtime.InteropServices;

[ComImport, Guid("B349ABE3-B56F-4689-BFCD-76BF39D888EA"), InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
public interface ICLRProfiling
{
    int AttachProfiler(
        /* [in] */ int dwProfileeProcessID,
        /* [in] */ int dwMillisecondsMax,
        /* [in] */ ref Guid pClsidProfiler,
        /* [in] */ string wszProfilerPath,
        /* [size_is][in] */ IntPtr pvClientData,
        /* [in] */ int cbClientData);
}
