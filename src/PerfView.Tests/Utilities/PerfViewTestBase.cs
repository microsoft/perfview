using System;
using Microsoft.VisualStudio.Threading;
using PerfView;

namespace PerfViewTests.Utilities
{
    public abstract class PerfViewTestBase : IDisposable
    {
        protected PerfViewTestBase()
        {
            // Create the main application
            AppLog.s_IsUnderTest = true;
            App.CommandLineArgs = new CommandLineArgs();
            App.CommandProcessor = new CommandProcessor();
            GuiApp.MainWindow = new MainWindow();

            JoinableTaskFactory = new JoinableTaskFactory(new JoinableTaskContext());
        }

        protected JoinableTaskFactory JoinableTaskFactory
        {
            get;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                GuiApp.MainWindow?.Close();
            }
        }
    }
}
