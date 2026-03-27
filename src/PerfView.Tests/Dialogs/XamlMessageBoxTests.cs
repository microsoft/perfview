using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PerfView.Dialogs;
using Xunit;

#pragma warning disable VSTHRD001 // Use JoinableTaskFactory — we're explicitly testing WPF Dispatcher threading
#pragma warning disable VSTHRD110 // Observe awaitable — fire-and-forget Task.Run is intentional in these tests

namespace PerfViewTests.Dialogs
{
    /// <summary>
    /// Regression tests for <see cref="XamlMessageBox"/> threading behavior.
    /// See https://github.com/microsoft/perfview/issues/2300
    /// </summary>
    public class XamlMessageBoxTests
    {
        /// <summary>
        /// Verifies that <see cref="XamlMessageBox.Show(string, string, MessageBoxButton)"/> auto-dispatches
        /// to the UI thread when called from a background thread, rather than throwing
        /// "The calling thread must be STA, because many UI components require this."
        /// Also verifies that calling from the UI thread directly still works (no-op dispatch).
        /// This is the core regression test for issue #2300.
        /// </summary>
        [Fact]
        public void Show_AutoDispatchesToUIThreadFromBackgroundThread()
        {
            Exception exception = null;
            MessageBoxResult uiResult = MessageBoxResult.None;
            MessageBoxResult bgResult = MessageBoxResult.None;

            var staThread = new Thread(() =>
            {
                try
                {
                    var app = Application.Current ?? new Application();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    RegisterMinimalThemeResources(app);

                    // Catch unhandled dispatcher exceptions so they don't silently hang.
                    app.DispatcherUnhandledException += (s, args) =>
                    {
                        exception = args.Exception;
                        args.Handled = true;
                        Dispatcher.CurrentDispatcher.InvokeShutdown();
                    };

                    // Auto-close any XamlMessageBox dialogs as soon as they load.
                    RegisterAutoCloseHandler();

                    // Safety timeout: force shutdown if the test hangs.
                    var safetyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
                    safetyTimer.Tick += (s, e) =>
                    {
                        safetyTimer.Stop();
                        if (exception == null)
                        {
                            exception = new TimeoutException("Safety timer fired — dialog was not auto-closed");
                        }
                        Dispatcher.CurrentDispatcher.InvokeShutdown();
                    };
                    safetyTimer.Start();

                    // Use Dispatcher.CurrentDispatcher (the STA thread's dispatcher being
                    // pumped by Dispatcher.Run) rather than app.Dispatcher, because if
                    // Application.Current was reused from a prior test, app.Dispatcher may
                    // belong to a different thread.
                    var currentDispatcher = Dispatcher.CurrentDispatcher;
                    currentDispatcher.BeginInvoke((Action)(() =>
                    {
                        try
                        {
                            // Part 1: Call directly from the UI thread (dispatch is a no-op).
                            uiResult = XamlMessageBox.Show("Test message", "XamlMBTest_UI", MessageBoxButton.OK);

                            // Part 2: Call from a background thread — before the fix for issue #2300,
                            // this would throw InvalidOperationException ("The calling thread must be STA")
                            // because XamlMessageBox creates a WPF Window requiring the UI thread.
                            Task.Run(() =>
                            {
                                try
                                {
                                    bgResult = XamlMessageBox.Show("Test message", "XamlMBTest_BG", MessageBoxButton.YesNo);
                                }
                                catch (Exception ex)
                                {
                                    exception = ex;
                                }
                                finally
                                {
                                    currentDispatcher.BeginInvoke(
                                        (Action)(() => currentDispatcher.InvokeShutdown()));
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            exception = ex;
                            Dispatcher.CurrentDispatcher.InvokeShutdown();
                        }
                    }));

                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            Assert.True(staThread.Join(TimeSpan.FromSeconds(10)), "Test timed out — dialog may not have been auto-closed");

            Assert.Null(exception);
            // Both dialogs were auto-closed without clicking a button, so Result is None.
            Assert.Equal(MessageBoxResult.None, uiResult);
            Assert.Equal(MessageBoxResult.None, bgResult);
        }

        private static bool s_autoCloseHandlerRegistered;

        /// <summary>
        /// Registers a class-level handler that auto-closes any <see cref="Window"/> with
        /// a test caption as soon as it finishes loading. The handler fires inside
        /// <see cref="Window.ShowDialog"/>'s nested message loop.
        /// </summary>
        private static void RegisterAutoCloseHandler()
        {
            if (s_autoCloseHandlerRegistered)
            {
                return;
            }

            s_autoCloseHandlerRegistered = true;
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler((sender, args) =>
                {
                    Window w = sender as Window;
                    if (w != null && w.Title != null && w.Title.StartsWith("XamlMBTest_"))
                    {
                        w.Dispatcher.BeginInvoke((Action)(() => w.Close()));
                    }
                }));
        }

        /// <summary>
        /// Registers the minimal resources needed by <c>MessageBoxWindow.xaml</c> so it
        /// can be created without loading the full PerfView theme.
        /// </summary>
        private static void RegisterMinimalThemeResources(Application app)
        {
            app.Resources["CustomToolWindowStyle"] = new Style(typeof(Window));
            app.Resources["ControlDarkerBackground"] = new SolidColorBrush(Colors.LightGray);
            app.Resources["ControlDefaultBorderBrush"] = new SolidColorBrush(Colors.Gray);
        }
    }
}
