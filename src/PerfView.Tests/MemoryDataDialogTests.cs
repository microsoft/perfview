using PerfView;
using PerfView.Dialogs;
using PerfViewTests.Utilities;
using REghZyFramework.Themes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Xunit;
using Xunit.Abstractions;

namespace PerfViewTests
{
    public class MemoryDataDialogTests : PerfViewTestBase
    {
        public MemoryDataDialogTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [WpfFact]
        public Task DacVerificationUsesSessionStateForLiveAndDumpDialogsAsync()
        {
            return RunUITestAsync(
                () => Task.FromResult(GuiApp.MainWindow),
                async window =>
                {
                    await JoinableTaskFactory.SwitchToMainThreadAsync();

                    Application app = Application.Current ?? new Application();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    var theme = new LightTheme();
                    theme.InitializeComponent();
                    app.Resources.MergedDictionaries.Add(theme);
                    try
                    {
                        var args = App.CommandLineArgs;
                        string persistedValue = App.UserConfigData["VerifyDacOnWindows"];
                        Assert.True(args.VerifyDacOnWindows);
                        foreach (string processDump in new[] { null, "test.dmp" })
                        {
                            args.ProcessDumpFile = processDump;
                            var dialog = new MemoryDataDialog(args, window, null);
                            try
                            {
                                dialog.Show();
                                Assert.True(dialog.VerifyDacOnWindowsCheckBox.IsVisible);
                                Assert.Equal(args.VerifyDacOnWindows, dialog.VerifyDacOnWindowsCheckBox.IsChecked);
                                bool newValue = !args.VerifyDacOnWindows;
                                dialog.VerifyDacOnWindowsCheckBox.IsChecked = newValue;
                                dialog.VerifyDacOnWindowsCheckBox.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                                Assert.Equal(newValue, args.VerifyDacOnWindows);
                            }
                            finally
                            {
                                dialog.Close();
                            }

                            var reopened = new MemoryDataDialog(args, window, null);
                            try
                            {
                                Assert.Equal(args.VerifyDacOnWindows, reopened.VerifyDacOnWindowsCheckBox.IsChecked);
                            }
                            finally
                            {
                                reopened.Close();
                            }
                        }

                        Assert.True(new CommandLineArgs().VerifyDacOnWindows);
                        Assert.Equal(persistedValue, App.UserConfigData["VerifyDacOnWindows"]);
                    }
                    finally
                    {
                        app.Resources.MergedDictionaries.Remove(theme);
                    }
                },
                window => Task.CompletedTask);
        }
    }
}
