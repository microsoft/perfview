using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace PerfView64
{
    /// <summary>
    /// This application simply loads and calls the main PerfView executable, passing along the command line arguments
    /// as they were received. It works as a 64-bit application by leveraging the ability of AnyCPU binaries to run as
    /// either 32- or 64-bit applications, and while the main PerfView executable has the "prefer 32-bit" option set,
    /// the 64-bit wrapper does not (and thus is launched by the OS as a 64-bit image where available).
    /// </summary>
    public class App
    {
        [STAThread]
        [DebuggerNonUserCode]
        public static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (FileNotFoundException ex) when (ex.Message.StartsWith("Could not load file or assembly 'PerfView"))
            {
                MessageBox.Show("Failed to run application. Is PerfView.exe present?", "PerfView.exe not found", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }

        private static int Run(string[] args) => PerfView.App.Main(args);
    }
}
