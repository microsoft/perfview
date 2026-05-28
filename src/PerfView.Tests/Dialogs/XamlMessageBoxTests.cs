using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using PerfView.Dialogs;
using Xunit;

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
#pragma warning disable VSTHRD200 // Keep the original regression test name stable.
        [WpfFact]
        public async Task Show_AutoDispatchesToUIThreadFromBackgroundThread()
#pragma warning restore VSTHRD200
        {
            Application app = Application.Current ?? new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            RegisterMinimalThemeResources(app);

            // Auto-close any XamlMessageBox dialogs as soon as they load.
            RegisterAutoCloseHandler();

            // Part 1: Call directly from the UI thread (dispatch is a no-op).
            MessageBoxResult uiResult = XamlMessageBox.Show("Test message", "XamlMBTest_UI", MessageBoxButton.OK);

            // Part 2: Call from a background thread — before the fix for issue #2300,
            // this would throw InvalidOperationException ("The calling thread must be STA")
            // because XamlMessageBox creates a WPF Window requiring the UI thread.
            Task<MessageBoxResult> backgroundShowTask = Task.Run(() =>
                XamlMessageBox.Show("Test message", "XamlMBTest_BG", MessageBoxButton.YesNo));

            Task completedTask = await Task.WhenAny(backgroundShowTask, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.True(
                ReferenceEquals(backgroundShowTask, completedTask),
                "Timed out waiting for XamlMessageBox.Show to dispatch to the WPF test thread.");

            MessageBoxResult bgResult = await backgroundShowTask;

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
#pragma warning disable VSTHRD001, VSTHRD110 // Loaded is already on the WPF thread; defer Close until loading completes.
                        w.Dispatcher.BeginInvoke((Action)(() => w.Close()));
#pragma warning restore VSTHRD001, VSTHRD110
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
