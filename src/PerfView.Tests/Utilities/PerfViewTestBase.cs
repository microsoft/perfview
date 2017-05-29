using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using PerfView;

namespace PerfViewTests.Utilities
{
    public abstract class PerfViewTestBase : IDisposable
    {
        protected PerfViewTestBase()
        {
            AppLog.s_IsUnderTest = true;
            App.CommandLineArgs = new CommandLineArgs();
            App.CommandProcessor = new CommandProcessor();
        }

        protected JoinableTaskFactory JoinableTaskFactory
        {
            get;
            private set;
        }

        protected async Task RunUITestAsync<T>(
            Func<Task<T>> setupAsync,
            Func<T, Task> testDriverAsync,
            Func<T, Task> cleanupAsync)
        {
            CreateMainWindow();

            var setupTask = JoinableTaskFactory.RunAsync(async () =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                // The main window has to be visible or the Closing event will not be raised on owned windows.
                GuiApp.MainWindow.Show();

                return await setupAsync().ConfigureAwait(false);
            });

            // Launch a background thread to drive interaction
            var testDriverTask = JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await testDriverAsync(await setupTask).ConfigureAwait(false);
                }
                finally
                {
                    await cleanupAsync(await setupTask).ConfigureAwait(false);
                }
            }, JoinableTaskCreationOptions.LongRunning);

            await testDriverTask.Task.ConfigureAwait(false);
        }

        private void CreateMainWindow()
        {
            GuiApp.MainWindow = new MainWindow();
            JoinableTaskFactory = new JoinableTaskFactory(new JoinableTaskContext());
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
