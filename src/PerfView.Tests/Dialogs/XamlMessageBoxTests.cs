using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using PerfView.Dialogs;
using Xunit;

namespace PerfViewTests.Dialogs
{
    /// <summary>
    /// Regression tests for <see cref="XamlMessageBox"/> threading and modal ownership.
    /// </summary>
    public class XamlMessageBoxTests
    {
        /// <summary>
        /// Verifies that <see cref="XamlMessageBox.Show(string, string, MessageBoxButton)"/> auto-dispatches
        /// to the UI thread when called from a background thread, rather than throwing
        /// "The calling thread must be STA, because many UI components require this."
        /// Also verifies that calling from the UI thread directly still works (no-op dispatch).
        /// An owner which has never been shown must also be ignored rather than assigned.
        /// This is the core regression test for issue #2300.
        /// Also verifies owner selection keeps modal dialogs reachable after switching
        /// applications (issue #2506).
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
            var dialogs = RegisterAutoCloseHandler();

            // Part 1: Call directly from the UI thread (dispatch is a no-op).
            MessageBoxResult uiResult = XamlMessageBox.Show("Test message", "XamlMBTest_UI", MessageBoxButton.OK);

            // Part 2: Use an owner that has not been shown.
            Window unshownOwner = new Window();
            MessageBoxResult unshownOwnerResult = XamlMessageBox.Show(
                unshownOwner,
                "Test message",
                "XamlMBTest_UnshownOwner",
                MessageBoxButton.OK);

            // Part 3: Call from a background thread — before the fix for issue #2300,
            // this would throw InvalidOperationException ("The calling thread must be STA")
            // because XamlMessageBox creates a WPF Window requiring the UI thread.
            Task<MessageBoxResult> backgroundShowTask = Task.Run(() =>
                XamlMessageBox.Show("Test message", "XamlMBTest_BG", MessageBoxButton.YesNo));

            Task completedTask = await Task.WhenAny(backgroundShowTask, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.True(
                ReferenceEquals(backgroundShowTask, completedTask),
                "Timed out waiting for XamlMessageBox.Show to dispatch to the WPF test thread.");

            MessageBoxResult bgResult = await backgroundShowTask;

            // All dialogs were auto-closed without clicking a button, so Result is None.
            Assert.Equal(MessageBoxResult.None, uiResult);
            Assert.Equal(MessageBoxResult.None, unshownOwnerResult);
            Assert.Equal(MessageBoxResult.None, bgResult);
            Assert.False(unshownOwner.IsLoaded);

            VerifyDialogOwnership(app, dialogs);
        }

        private static void VerifyDialogOwnership(
            Application app,
            List<(Window Owner, bool ShowInTaskbar, WindowStartupLocation StartupLocation)> dialogs)
        {
            Window originalMainWindow = app.MainWindow;
            Window mainWindow = new Window { ShowInTaskbar = false, ShowActivated = false };
            Window secondaryWindow = new Window { ShowInTaskbar = false };
            try
            {
                app.MainWindow = mainWindow;

                // With no loaded owner, the dialog must be independently reachable.
                XamlMessageBox.Show("Test message", "XamlMBTest_NoOwner");
                Assert.Null(dialogs[dialogs.Count - 1].Owner);
                Assert.True(dialogs[dialogs.Count - 1].ShowInTaskbar);
                Assert.Equal(WindowStartupLocation.CenterScreen, dialogs[dialogs.Count - 1].StartupLocation);

                mainWindow.Show();
                Assert.False(mainWindow.IsActive);

                // Symbol resolution can prompt after the user has switched applications.
                XamlMessageBox.Show("Test message", "XamlMBTest_MainOwner");
                Assert.Same(mainWindow, dialogs[dialogs.Count - 1].Owner);
                Assert.False(dialogs[dialogs.Count - 1].ShowInTaskbar);

                secondaryWindow.Show();
                secondaryWindow.Activate();

                // Windows may deny foreground activation to the test host.
                Window expectedOwner = secondaryWindow.IsActive ? secondaryWindow : mainWindow;
                XamlMessageBox.Show("Test message", "XamlMBTest_ActiveOwner");
                Assert.Same(expectedOwner, dialogs[dialogs.Count - 1].Owner);

                expectedOwner = secondaryWindow.IsActive ? secondaryWindow : mainWindow;
                XamlMessageBox.Show(new Window(), "Test message", "XamlMBTest_UnshownOwnerFallback");
                Assert.Same(expectedOwner, dialogs[dialogs.Count - 1].Owner);

                XamlMessageBox.Show(mainWindow, "Test message", "XamlMBTest_ExplicitOwner");
                Assert.Same(mainWindow, dialogs[dialogs.Count - 1].Owner);
            }
            finally
            {
                secondaryWindow.Close();
                mainWindow.Close();
                app.MainWindow = originalMainWindow;
            }
        }

        /// <summary>
        /// Registers a class-level handler that auto-closes any <see cref="Window"/> with
        /// a test caption as soon as it finishes loading. The handler fires inside
        /// <see cref="Window.ShowDialog"/>'s nested message loop.
        /// </summary>
        private static List<(Window Owner, bool ShowInTaskbar, WindowStartupLocation StartupLocation)> RegisterAutoCloseHandler()
        {
            var dialogs = new List<(Window Owner, bool ShowInTaskbar, WindowStartupLocation StartupLocation)>();
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler((sender, args) =>
                {
                    Window w = sender as Window;
                    if (w != null && w.Title != null && w.Title.StartsWith("XamlMBTest_"))
                    {
                        dialogs.Add((w.Owner, w.ShowInTaskbar, w.WindowStartupLocation));
#pragma warning disable VSTHRD001, VSTHRD110 // Loaded is already on the WPF thread; defer Close until loading completes.
                        w.Dispatcher.BeginInvoke((Action)(() => w.Close()));
#pragma warning restore VSTHRD001, VSTHRD110
                    }
                }));
            return dialogs;
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
