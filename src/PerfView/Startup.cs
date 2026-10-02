using Microsoft.Diagnostics.Utilities;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PerfView
{
    internal static class Startup
    {
#if PERFVIEW_COLLECT
        [MTAThread]
#else
        [STAThread]
#endif
        public static int Main(string[] args)
        {
            return Run(() => OperatingSystemVersion.IsSupported, ReportUnsupported, () => RunApplication(args));
        }

        /// <summary>
        /// Checks the Windows requirement before entering TraceEvent-dependent application startup.
        /// Delegates let tests exercise rejection and error reporting without launching the application or showing UI.
        /// </summary>
        internal static int Run(Func<bool> isSupported, Action<string> reportUnsupported, Func<int> runApplication)
        {
            bool supported;
            try
            {
                supported = isSupported();
            }
            catch (Win32Exception ex)
            {
                // A failed native version query is not evidence of support. Report it and stop startup.
                reportUnsupported(ex.Message);
                return 1;
            }

            if (!supported)
            {
                // Pop-up a dialog box or report the error to stderr.
                // Return a failure status without entering normal startup or its console/key-wait handling.
                reportUnsupported("This operating system is not supported. " + OperatingSystemVersion.Requirement);
                return 1;
            }

            // Only supported hosts reach the deferred application handoff; preserve its exit status.
            return runApplication();
        }

        #region private
        // Keep TraceEvent-dependent types out of the unsupported-OS startup path.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int RunApplication(string[] args)
        {
            return App.Main(args);
        }

        private static void ReportUnsupported(string message)
        {
#if PERFVIEW_COLLECT
            Console.Error.WriteLine("PerfViewCollect: " + message);
#else
            // XamlMessageBox requires WPF theme resources that are not initialized at this startup boundary.
            // Use the native dialog to report the error before GUI initialization or TraceEvent-dependent startup.
            MessageBoxW(IntPtr.Zero, message, "PerfView", 0x10);
#endif
        }

#if !PERFVIEW_COLLECT
        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);
#endif
        #endregion
    }
}
