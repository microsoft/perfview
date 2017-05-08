using System;
using System.Diagnostics;

namespace PerfView64
{
    public class App
    {
        [STAThread]
        [DebuggerNonUserCode]
        public static int Main(string[] args)
        {
            return PerfView.App.Main(args);
        }
    }
}
