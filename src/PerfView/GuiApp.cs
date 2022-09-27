using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using Utilities;

namespace PerfView
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class GuiApp : Application
    {
        /// <summary>
        /// The one and only main GUI window of the application.
        /// </summary>
        public static new MainWindow MainWindow;

        public GuiApp(bool installUnhandledExceptionHandlers = true)
        {
            Startup += delegate (object sender, StartupEventArgs e) { ApplicationStarted(); };

            InitializeComponent();

            if (installUnhandledExceptionHandlers)
            {
                // Setup unhanded exception handlers
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                DispatcherUnhandledException += OnGuiUnhandledException;
            }
        }

        /// <summary>
        /// Called when the application is started.
        /// </summary>
        private void ApplicationStarted()
        {
            if (!Enum.TryParse(App.UserConfigData["Theme"], out Theme theme))
            {
                theme = Theme.Light;
            }

            // initialize theme before creating any window
            ThemeViewModel.InitTheme(theme);
            MainWindow = new MainWindow();
            MainWindow.ThemeViewModel.SetTheme(theme);

            var logFile = File.CreateText(App.LogFileName);
            StatusBar.AttachWriterToLogStream(logFile);
            App.CommandProcessor.LogFile = MainWindow.StatusBar.LogWriter;

            // Work around for Non-English/US locale (e.g. French) where among other things the decimal point is a comma.
            // WPF never uses the CurrentCulture when it formats numbers (it always uses US)
            // This sets the default to the current culture.
            // see http://serialseb.blogspot.com/2007/04/wpf-tips-1-have-all-your-dates-times.html
            FrameworkElement.LanguageProperty.OverrideMetadata(
              typeof(FrameworkElement),
              new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

            if (App.CommandLineArgs.HelpRequested)
            {
                MainWindow.Show();
                MainWindow.DoCommandLineHelp(null, null);
                return;
            }

            MainWindow.StatusBar.LogWriter.WriteLine("Started with command line: {0}", Environment.CommandLine);
            MainWindow.StatusBar.LogWriter.WriteLine("PerfView Version: {0}  BuildDate: {1}", AppLog.VersionNumber, AppLog.BuildDate);
            MainWindow.StatusBar.LogWriter.WriteLine("PerfView Start Time {0}", DateTime.Now);

            if (App.NeedsEulaConfirmation(App.CommandLineArgs))
            {
                var eula = new PerfView.Dialogs.EULADialog(MainWindow);
                bool? accepted = eula.ShowDialog();
                if (!(accepted ?? false))
                {
                    Environment.Exit(-10);
                }

                App.AcceptEula();       // Remember that we have accepted the EULA for next time.
            }

            MainWindow.Loaded += delegate (object sender, RoutedEventArgs ev)
            {
                string[] providers = App.CommandLineArgs.Providers;

                if (App.CommandLineArgs.CommandLineFailure != null)
                {
                    var message = App.CommandLineArgs.CommandLineFailure.Message;
                    if (message.Contains("\n"))
                    {
                        MainWindow.StatusBar.LogError("Command Line Error, see log file for details.");
                        MainWindow.StatusBar.Log(message);
                    }
                    else
                    {
                        MainWindow.StatusBar.LogError("Command Line Error: " + message);
                    }

                    return;
                }

                if (App.CommandLineArgs.DoCommand == null)
                {
                    App.CommandLineArgs.DoCommand = App.CommandProcessor.View;
                }

                string commandName = "View";
                Action continuation = delegate
                {
                    if (App.CommandLineArgs.DataFile != null)
                    {
                        MainWindow.OpenPath(App.CommandLineArgs.DataFile);
                    }
                };
                if (App.CommandLineArgs.DoCommand != App.CommandProcessor.View)
                {
                    commandName = App.CommandLineArgs.DoCommand.Method.Name;
                    continuation = null;
                }

                // Run commands in the PerfViewExtensions\PerfViewStartup file.
                PerfViewExtensibility.Extensions.RunUserStartupCommands(MainWindow.StatusBar);
                MainWindow.OpenPreviouslyOpened();
                MainWindow.ExecuteCommand(commandName, App.CommandLineArgs.DoCommand, null, continuation);
            };
            MainWindow.Show();
        }

        /// <summary>
        /// Called when exception happens in a GUI routine
        /// </summary>
        private void OnGuiUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            bool userLevel;
            string message = ExceptionMessage.GetUserMessage(e.Exception, out userLevel);
            if (userLevel)
            {
                // TODO FIX NOW would really like to find the window with focus, and not always use the main window...
                MainWindow.Focus();
                MainWindow.StatusBar.LogError(message);
                e.Handled = true;
            }
            else
            {
                var feedbackSent = AppLog.SendFeedback("Unhandled Exception in GUI\r\n" + e.Exception.ToString(), true);
                var dialog = new PerfView.Dialogs.UnhandledExceptionDialog(MainWindow, e.Exception, feedbackSent);
                var ret = dialog.ShowDialog();
                // If it returns, it means that the user has opted to continue.
                e.Handled = true;
            }
        }

        /// <summary>
        /// Fallback if we happen to take an exception in a non-gui routine (shouldn't happen!)
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // TODO discriminate between the GUI and Non_GUI case.
            var feedbackSent = AppLog.SendFeedback("Unhandled Exception\r\n" + e.ExceptionObject.ToString(), true);
            MainWindow.Dispatcher.BeginInvoke((Action)delegate ()
            {
                var dialog = new PerfView.Dialogs.UnhandledExceptionDialog(MainWindow, e.ExceptionObject, feedbackSent);
                var ret = dialog.ShowDialog();
            });
        }
    }
}

