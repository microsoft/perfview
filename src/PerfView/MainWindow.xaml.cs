using Controls;
using Microsoft.Diagnostics.Symbols.Authentication;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Utilities;
using PerfView.Dialogs;
using PerfView.GuiUtilities;
using PerfViewExtensibility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Utilities;

namespace PerfView
{
    /// <summary>
    /// The main window of the performance viewer.
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _testing;

        // TODO FIX NOW do a better job keeping track of open windows
        public int NumWindowsNeedingSaving;

        /// <summary>
        /// The view model for the user's choice of authentication providers.
        /// </summary>
        public AuthenticationViewModel AuthenticationViewModel { get; }

        /// <summary>
        /// The view model for the application theme.
        /// </summary>
        public ThemeViewModel ThemeViewModel { get; }

        public MainWindow(bool testing = false)
        {
            _testing = testing;

            ThemeViewModel = new ThemeViewModel(App.UserConfigData);
            InitializeComponent();
            Directory.HistoryLength = 25;
            DataContext = this;

            // Initialize the directory history if available.
            var directoryHistory = App.UserConfigData["DirectoryHistory"];
            if (directoryHistory != null)
            {
                Directory.SetHistory(directoryHistory.Split(';'));
            }

            // And also add the docs directory
            var docsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(docsDir))
            {
                Directory.AddToHistory(docsDir);
            }

            // Make sure the location is sane so it can be displayed.
            var top = App.UserConfigData.GetDouble("MainWindowTop", Top);
            Top = Math.Min(Math.Max(top, 0), System.Windows.SystemParameters.PrimaryScreenHeight - 200);

            var left = App.UserConfigData.GetDouble("MainWindowLeft", Left);
            Left = Math.Min(Math.Max(left, 0), System.Windows.SystemParameters.PrimaryScreenWidth - 200);

            Height = App.UserConfigData.GetDouble("MainWindowHeight", Height);
            Width = App.UserConfigData.GetDouble("MainWindowWidth", Width);

            // Initialize the authentication view model from user config
            AuthenticationViewModel = new AuthenticationViewModel(App.UserConfigData);
            App.SymbolReaderHandlerProvider = GetSymbolReaderHandler;

            Loaded += delegate (object sender1, RoutedEventArgs e2)
            {
                FileFilterTextBox.Focus();
            };

            Closing += delegate (object sender, CancelEventArgs e)
            {
                if (NumWindowsNeedingSaving != 0)
                {
                    var result = XamlMessageBox.Show(
                        this,
                        """
                        You have unsaved notes in some Stack Views.
                        Do you wish to exit anyway?
                        """,
                        "Unsaved Data",
                        MessageBoxButton.OKCancel);

                    if (result == MessageBoxResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }
                }

                if (StatusBar.IsWorking)
                {
                    if (App.CommandProcessor.StopInProgress)
                    {
                        var result = XamlMessageBox.Show(
                            this,
                            """
                            Closing PerfView while the trace is being processed will result in a trace that is unusable if copied off of this machine.
                            Would you still like to close PerfView?
                            """,
                            "Collecting data in progress",
                            MessageBoxButton.YesNo);

                        if (result == MessageBoxResult.No)
                        {
                            e.Cancel = true;
                            return;
                        }
                    }

                    StatusBar.AbortWork();
                }

                if (WindowState != System.Windows.WindowState.Maximized)
                {
                    App.UserConfigData["MainWindowWidth"] = RenderSize.Width.ToString("f0", CultureInfo.InvariantCulture);
                    App.UserConfigData["MainWindowHeight"] = RenderSize.Height.ToString("f0", CultureInfo.InvariantCulture);
                    App.UserConfigData["MainWindowTop"] = Top.ToString("f0", CultureInfo.InvariantCulture);
                    App.UserConfigData["MainWindowLeft"] = Left.ToString("f0", CultureInfo.InvariantCulture);
                }
            };

            // Initialize configuration options.
            InitializeOpenToLastUsedDirectory();
            InitializeDoNotCompressStackFramesOnCopy();
            InitializeTruncateRawEventData();

            InitializeFeedback();
        }
        public PerfViewDirectory CurrentDirectory { get { return m_CurrentDirectory; } }

        public void InitializeOpenToLastUsedDirectory()
        {
            bool currentValue;
            bool.TryParse(App.UserConfigData["OpenToLastUsedDirectory"], out currentValue);
            Option_OpenToLastUsedDirectory.IsChecked = currentValue;
        }

        public void ToggleOpenToLastUsedDirectory(object sender, RoutedEventArgs e)
        {
            bool currentValue;
            bool.TryParse(App.UserConfigData["OpenToLastUsedDirectory"], out currentValue);
            bool newValue = !currentValue;
            App.UserConfigData["OpenToLastUsedDirectory"] = newValue.ToString();
            Option_OpenToLastUsedDirectory.IsChecked = newValue;
        }

        public void OpenPreviouslyOpened()
        {
            string path = ".";
            if (bool.TryParse(App.UserConfigData["OpenToLastUsedDirectory"], out bool openToLastUsedDirectory) && openToLastUsedDirectory)
            {
                path = App.UserConfigData["Directory"] ?? ".";
            }
            OpenPath(path);
        }
        /// <summary>
        /// Initial read-in of the user settings to check if they had previously opted-in for the DoNotCompressStackFramesOnCopy option
        /// </summary>
        public void InitializeDoNotCompressStackFramesOnCopy()
        {
            bool currentValue;
            bool.TryParse(App.UserConfigData["DoNotCompressStackFramesOnCopy"], out currentValue);
            Option_DoNotCompressStackFramesOnCopy.IsChecked = currentValue;
            PerfDataGrid.DoNotCompressStackFrames = currentValue;
        }

        /// <summary>
        /// Toggles the option to compress stack frame lines copied from the stack viewer window.
        /// </summary>
        public void ToggleDoNotCompressStackFramesOnCopy(object sender, RoutedEventArgs e)
        {
            bool currentValue;
            bool.TryParse(App.UserConfigData["DoNotCompressStackFramesOnCopy"], out currentValue);
            bool newValue = !currentValue;
            App.UserConfigData["DoNotCompressStackFramesOnCopy"] = newValue.ToString();
            Option_DoNotCompressStackFramesOnCopy.IsChecked = newValue;
            PerfDataGrid.DoNotCompressStackFrames = newValue;
        }

        public void ToggleExperimentalGCFeatures(object sender, RoutedEventArgs e)
        {
            bool currentValue;
            bool.TryParse(App.UserConfigData["ExperimentalGCFeatures"], out currentValue);
            bool newValue = !currentValue;
            App.UserConfigData["ExperimentalGCFeatures"] = newValue.ToString();
            Option_ExperimentalGCFeatures.IsChecked = newValue;
            Stats.GcStats.EnableExperimentalFeatures = newValue;
        }

        /// <summary>
        /// Initial read-in of the user settings to check if they had previously opted-in for the TruncateRawEventData option
        /// </summary>
        public void InitializeTruncateRawEventData()
        {
            bool willTruncate;
            if (!bool.TryParse(App.UserConfigData["TruncateRawEventData"], out willTruncate))
            {
                // the default behavior of PerfView has always been to truncate the event data
                // so if the TryParse fails for any reason, then opt for the original behavior
                willTruncate = true;
            }
            Option_TruncateRawEventData.IsChecked = willTruncate;
            EventWindow.TruncateRawEventData = willTruncate;
        }

        /// <summary>
        /// Toggles the option to truncate or not the raw event data when an event is dumped from the Events view
        /// </summary>
        public void ToggleTruncateRawEventData(object sender, RoutedEventArgs e)
        {
            bool currentValue;
            bool.TryParse(App.UserConfigData["TruncateRawEventData"], out currentValue);
            bool newValue = !currentValue;
            App.UserConfigData["TruncateRawEventData"] = newValue.ToString();
            Option_TruncateRawEventData.IsChecked = newValue;
            EventWindow.TruncateRawEventData = newValue;
        }

        /// <summary>
        /// Set the left pane to the specified directory.  If it is a file name, then that file name is opened
        /// </summary>
        public void OpenPath(string path, bool force = false)
        {
            // If someone holds down shift, right clicks on a file and selects "Copy as path" it will
            // contain a starting and ending quote (e.g. "e:\trace.etl" as apposed to e:\trace.etl). If
            // they then paste this into the MainWindow's directory HistoryComboBox without removing
            // the quotes and press enter, an exception will be thrown here. Remove these quotes so
            // it doesn't need to be manually done.
            if (path.StartsWith("\"") && path.EndsWith("\"") && path.Length >= 2)
            {
                path = path.Substring(1, path.Length - 2);
            }

            if (System.IO.Directory.Exists(path))
            {
                var fullPath = App.MakeUniversalIfPossible(Path.GetFullPath(path));
                if (force || m_CurrentDirectory == null || fullPath != m_CurrentDirectory.FilePath)
                {
                    Directory.Text = fullPath;
                    if (Directory.AddToHistory(fullPath))
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (string item in Directory.Items)
                        {
                            if (sb.Length != 0)
                            {
                                sb.Append(';');
                            }

                            sb.Append(item);
                        }
                        App.UserConfigData["DirectoryHistory"] = sb.ToString();
                    }

                    App.UserConfigData["Directory"] = fullPath;
                    FileFilterTextBox.Text = "";
                    m_CurrentDirectory = new PerfViewDirectory(fullPath);
                    UpdateFileFilter();

                    string appName = "PerfView";
                    string elevatedSuffix = (TraceEventSession.IsElevated() ?? false) ? " (Administrator)" : "";
                    Title = appName + " " + CurrentDirectory.FilePath + elevatedSuffix;
                }
            }
            else if (System.IO.File.Exists(path))
            {
                Open(path);
            }
            else
            {
                Directory.RemoveFromHistory(Directory.Text);
                if (m_CurrentDirectory != null)
                {
                    Directory.Text = m_CurrentDirectory.FilePath;
                }

                StatusBar.LogError("Directory " + path + " does not exist");
            }
        }
        /// <summary>
        /// Given a file name and format open the file (if format is null we try to infer the format from the file extension)
        /// </summary>
        public void Open(string dataFileName, PerfViewFile format = null, Action doAfter = null)
        {
            // Allow people open a directory name.
            if (System.IO.Directory.Exists(dataFileName))
            {
                StatusBar.Log("Opened directory " + dataFileName);
                OpenPath(dataFileName);
                return;
            }
            var dir = Path.GetDirectoryName(dataFileName);
            if (string.IsNullOrEmpty(dir))
            {
                dir = ".";
            }

            OpenPath(dir);
            PerfViewFile.Get(dataFileName).Open(this, StatusBar, doAfter);
        }
        /// <summary>
        /// Opens a stack source of a given name (null is the default) for a given file.
        /// </summary>
        public void OpenStacks(string dataFileName, PerfViewFile format = null, string stackSourceName = null)
        {
            Open(dataFileName, null, delegate ()
            {
                var data = PerfViewFile.Get(dataFileName);
                if (data.Children != null)
                {
                    if (stackSourceName == null)
                    {
                        stackSourceName = data.DefaultStackSourceName;
                    }

                    var source = data.GetStackSource(stackSourceName);
                    if (source != null)
                    {
                        source.Open(this, StatusBar);
                    }
                }
            });
        }

        /// <summary>
        /// Open the Memory Dump dialog.  If processDumpFile == null then it will prompt for a live process.
        /// It will prime the process dump file from the given string.
        /// </summary>
        public void TakeHeapShapshot(Action continuation)
        {
            // Set the default value for continuation
            if (continuation == null)
            {
                continuation = TryOpenDataFile;
            }

            // You need to be admin if you are not taking the snapshot from a dump.
            if (App.CommandLineArgs.ProcessDumpFile == null)
            {
                App.CommandProcessor.LaunchPerfViewElevatedIfNeeded("GuiHeapSnapshot", App.CommandLineArgs);
            }

            ChangeCurrentDirectoryIfNeeded();
            var memoryDialog = new Dialogs.MemoryDataDialog(App.CommandLineArgs, this, continuation);
            memoryDialog.Show();        // Can't be a true dialog because you can't bring up the log otherwise.
            // TODO FIX NOW.   no longer a dialog, ensure that it is unique?
        }

        public RunCommandDialog CollectWindow { get; set; }
        /// <summary>
        /// This is a helper that performs a command line style action, logging output to the log.
        /// statusMessage is what is displayed while the command is executing.
        ///
        /// If continuation is non-null it is executed after the command completes successfully
        /// If _finally is non-null, it is executed after the command (and continuation), even if the command is unsuccessful.
        /// </summary>
        public void ExecuteCommand(string statusMessage, Action<CommandLineArgs> command, StatusBar worker = null, Action continuation = null, Action finally_ = null)
        {
            App.CommandProcessor.ShowLog = false;

            if (worker == null)
            {
                worker = StatusBar;
            }

            App.CommandProcessor.LogFile = worker.LogWriter;
            worker.StartWork(statusMessage, delegate ()
            {
                command(App.CommandLineArgs);
                worker.EndWork(delegate ()
                {
                    // Refresh directory view
                    RefreshCurrentDirectory();

                    // TODO FIX NOW use continuation instead
                    if (App.CommandProcessor.ShowLog)
                    {
                        worker.OpenLog();
                    }

                    var openNext = GuiApp.MainWindow.m_openNextFileName;
                    GuiApp.MainWindow.m_openNextFileName = null;
                    if (openNext != null)
                    {
                        Open(openNext);
                    }

                    continuation?.Invoke();
                });
            }, finally_);
        }

        /// GUI command callbacks
        // The file menu callbacks
        internal void DoSetSymbolPath(object sender, RoutedEventArgs e)
        {
            var symPathDialog = new SymbolPathDialog(this, App.SymbolPath, "Symbol", delegate (string newPath)
            {
                App.SymbolPath = newPath;
            });
            symPathDialog.Show();
        }

        internal void DoRun(object sender, RoutedEventArgs e)
        {
            ChangeCurrentDirectoryIfNeeded();
            CollectWindow = new RunCommandDialog(App.CommandLineArgs, this, false, TryOpenDataFile);
            CollectWindow.Show();
        }
        internal void DoCollect(object sender, RoutedEventArgs e)
        {
            ChangeCurrentDirectoryIfNeeded();
            CollectWindow = new RunCommandDialog(App.CommandLineArgs, this, true, TryOpenDataFile);
            CollectWindow.Show();
        }

        internal void TryOpenDataFile()
        {
            if (App.CommandLineArgs.DataFile != null)
            {
                var file = PerfViewFile.TryGet(App.CommandLineArgs.DataFile);
                if (file != null)
                {
                    if (file.IsOpened)
                    {
                        file.Close();
                    }

                    OpenPath(App.CommandLineArgs.DataFile);
                }
            }
        }

        /// <summary>
        /// Refreshes the current shown directory
        /// </summary>
        private void RefreshCurrentDirectory()
        {
            OpenPath(Directory.Text, force: true);
        }

        private void DoAbort(object sender, RoutedEventArgs e)
        {
            ExecuteCommand("Aborting any active Data collection", App.CommandProcessor.Abort);
        }
        private void DoMerge(object sender, RoutedEventArgs e)
        {
            // TODO FIX NOW, decide how I want this done.   Do I select or do I use GetDataFielName
            var selectedFile = TreeView.SelectedItem as PerfViewFile;
            if (selectedFile == null)
            {
                throw new ApplicationException("No file selected.");
            }

            // TODO this has a side effect...
            App.CommandLineArgs.DataFile = selectedFile.FilePath;
            App.CommandLineArgs.Zip = false;
            if (!App.CommandLineArgs.DataFile.EndsWith(".etl"))
            {
                throw new ApplicationException("File " + App.CommandLineArgs.DataFile + " not a .ETL file");
            }

            ExecuteCommand("Merging " + Path.GetFullPath(App.CommandLineArgs.DataFile), App.CommandProcessor.Merge);
        }
        private void DoZip(object sender, RoutedEventArgs e)
        {
            var selectedFile = TreeView.SelectedItem as PerfViewFile;
            if (selectedFile == null)
            {
                throw new ApplicationException("No file selected.");
            }

            // TODO this has a side effect...
            App.CommandLineArgs.DataFile = selectedFile.FilePath;
            App.CommandLineArgs.Zip = true;
            if (!App.CommandLineArgs.DataFile.EndsWith(".etl"))
            {
                throw new ApplicationException("File " + App.CommandLineArgs.DataFile + " not a .ETL file");
            }
            // TODO we may be doing an unnecessary merge.
            ExecuteCommand("Merging and Zipping " + Path.GetFullPath(App.CommandLineArgs.DataFile), App.CommandProcessor.Merge);
        }

        private void DoMergeAndZipAll(object sender, RoutedEventArgs e)
        {
            List<Action> actions = new List<Action>();
            foreach (var file in TreeView.Items.OfType<PerfViewFile>().Reverse())
            {
                var filePath = file.FilePath;
                if (!filePath.EndsWith(".etl"))
                {
                    continue;
                }

                var continuation = actions.LastOrDefault();
                actions.Add(() =>
                {
                    // TODO this has a side effect...
                    App.CommandLineArgs.DataFile = filePath;
                    App.CommandLineArgs.Zip = true;

                    ExecuteCommand("Merging and Zipping " + Path.GetFullPath(App.CommandLineArgs.DataFile), App.CommandProcessor.Merge, continuation: continuation);
                });
            }

            actions.LastOrDefault()?.Invoke();
        }

        private void DoUnZip(object sender, RoutedEventArgs e)
        {
            var selectedFile = TreeView.SelectedItem as PerfViewFile;
            if (selectedFile == null)
            {
                throw new ApplicationException("No file selected.");
            }

            // TODO this has a side effect...
            var inputName = selectedFile.FilePath;
            if (!inputName.EndsWith(".etl.zip"))
            {
                throw new ApplicationException("File " + inputName + " not a zipped .ETL file");
            }

            // TODO make a command
            StatusBar.StartWork("Unzipping " + inputName, delegate ()
            {
                CommandProcessor.UnZipIfNecessary(ref inputName, StatusBar.LogWriter, false);
                StatusBar.EndWork(delegate ()
                {
                    // Refresh the directory view
                    RefreshCurrentDirectory();
                });
            });
        }

        private void DoUserCommand(object sender, RoutedEventArgs e)
        {
            if (m_UserDefineCommandDialog == null)
            {
                m_UserDefineCommandDialog = new UserCommandDialog(this, delegate (string commandAndArgs)
                {
                    App.CommandLineArgs.CommandAndArgs = ParseWordsOrQuotedStrings(commandAndArgs).ToArray();
                    bool commandSuccessful = false;

                    ExecuteCommand("User Command " + string.Join(" ", commandAndArgs), App.CommandProcessor.UserCommand, null,
                        delegate { commandSuccessful = true; },
                        delegate { if (!commandSuccessful) { m_UserDefineCommandDialog.RemoveHistory(commandAndArgs); } });
                });
            }
            m_UserDefineCommandDialog.Show();
            m_UserDefineCommandDialog.Focus();
        }

        private void DoExit(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void DoClearTempFiles(object sender, RoutedEventArgs e)
        {
            StatusBar.Log("Cleaning up " + CacheFiles.CacheDir + ".");
            DirectoryUtilities.Clean(CacheFiles.CacheDir);
            System.IO.Directory.CreateDirectory(CacheFiles.CacheDir);
            foreach (var file in System.IO.Directory.EnumerateFiles(CacheFiles.CacheDir))
            {
                StatusBar.Log("Could not delete " + file);
            }
        }
        private void DoClearUserConfig(object sender, RoutedEventArgs e)
        {
            StatusBar.Log("Deleting user config file " + App.UserConfigDataFileName + ".");
            FileUtilities.ForceDelete(App.UserConfigDataFileName);
            App.UserConfigData.Clear();
        }

        private void DoCancel(object sender, ExecutedRoutedEventArgs e)
        {
            StatusBar.AbortWork();
        }
        private void InitializeFeedback()
        {
            System.Threading.Thread.Sleep(100);     // Wait for startup to end, this is lower priority work.

            // FeedbackButton.Visibility = System.Windows.Visibility.Collapsed;
            WikiButton.Visibility = System.Windows.Visibility.Collapsed;

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                // If we have the PdbScope.exe file then we can enable the ImageFile capability
                string pdbScopeFile = Path.Combine(PerfViewExtensibility.Extensions.ExtensionsDirectory, "PdbScope.exe");
                bool pdbScopeExists = File.Exists(pdbScopeFile);

                string ilSizeFile = Path.Combine(PerfViewExtensibility.Extensions.ExtensionsDirectory, "ILSize.dll");
                bool ilSizeExists = File.Exists(ilSizeFile);

                Dispatcher.BeginInvoke((Action)delegate ()
                {
                    if (pdbScopeExists)
                    {
                        ImageSizeMenuItem.Visibility = System.Windows.Visibility.Visible;
                    }
                    else
                    {
                        StatusBar.Log("Warning: PdbScope not found at " + pdbScopeFile);
                        StatusBar.Log("Disabling the Image Size Menu Item.");
                    }
                    if (ilSizeExists)
                    {
                        ILSizeMenuItem.Visibility = System.Windows.Visibility.Visible;
                    }
                    else
                    {
                        StatusBar.Log("Warning: ILSize not found at " + ilSizeFile);
                        StatusBar.Log("Disabling the IL Size Menu Item.");
                    }
                });
            });
        }
        private void DoWikiClick(object sender, RoutedEventArgs e)
        {
            var wikiUrl = "http://devdiv/sites/wikis/perf/Wiki%20Pages/PerfView%20Wiki.aspx";
            StatusBar.Log("Opening " + wikiUrl);
            Command.Run(wikiUrl, new CommandOptions().AddStart().AddTimeout(CommandOptions.Infinite));
        }
        private void DoVideoClick(object sender, RoutedEventArgs e)
        {
            var videoUrl = Path.Combine(Path.GetDirectoryName(SupportFiles.MainAssemblyPath), @"PerfViewVideos\PerfViewVideos.htm");
            if (!File.Exists(videoUrl))
            {
                if (!AllowNavigateToWeb)
                {
                    StatusBar.LogError("Navigating to web disallowed, canceling.");
                    return;
                }
                videoUrl = Path.Combine(SupportFiles.SupportFileDir, "perfViewWebVideos.htm");
            }
            StatusBar.Log("Opening " + videoUrl);
            Command.Run(Command.Quote(videoUrl), new CommandOptions().AddStart().AddTimeout(CommandOptions.Infinite));
        }

        private void DoTakeHeapSnapshot(object sender, RoutedEventArgs e)
        {
            App.CommandLineArgs.ProcessDumpFile = null;
            TakeHeapShapshot(null);
        }
        private void DoDirectorySize(object sender, RoutedEventArgs e)
        {
            App.CommandLineArgs.CommandAndArgs = new string[] { "DirectorySize" };
            App.CommandLineArgs.DoCommand = App.CommandProcessor.UserCommand;
            ExecuteCommand("Computing directory size", App.CommandLineArgs.DoCommand);
        }
        private void DoImageSize(object sender, RoutedEventArgs e)
        {
            App.CommandLineArgs.CommandAndArgs = new string[] { "ImageSize" };
            App.CommandLineArgs.DoCommand = App.CommandProcessor.UserCommand;
            ExecuteCommand("Computing image size", App.CommandLineArgs.DoCommand);
        }
        private void DoILSize(object sender, RoutedEventArgs e)
        {
            App.CommandLineArgs.CommandAndArgs = new string[] { "ILSize.ILSize" };
            App.CommandLineArgs.DoCommand = App.CommandProcessor.UserCommand;
            ExecuteCommand("Computing image size", App.CommandLineArgs.DoCommand);
        }

        private void DoTakeHeapShapshotFromProcessDump(object sender, RoutedEventArgs e)
        {
            App.CommandLineArgs.ProcessDumpFile = "";
            TakeHeapShapshot(null);
        }

        // The Help menu callbacks
        internal void DoCommandLineHelp(object sender, RoutedEventArgs e)
        {
            var editor = new TextEditorWindow(this);
            editor.Width = 1000;
            editor.Height = 600;
            editor.Title = "PerfView Command Line Help";
            editor.TextEditor.AppendText(CommandLineArgs.GetHelpString(120));
            editor.TextEditor.IsReadOnly = true;
            editor.Show();
        }
        internal void DoUserCommandHelp(object sender, RoutedEventArgs e)
        {
            var sw = new StringWriter();
            sw.WriteLine("All User Commands");
            Extensions.GenerateHelp(sw);

            var editor = new TextEditorWindow(this);
            editor.Width = 850;
            editor.Height = 600;
            editor.Title = "PerfView Command Line Help";
            editor.TextEditor.AppendText(sw.ToString());
            editor.TextEditor.IsReadOnly = true;
            editor.Show();
        }

        private void DoAbout(object sender, RoutedEventArgs e)
        {
            string versionString = $"""
                PerfView Version {AppInfo.VersionNumber}
                BuildDate: {AppInfo.BuildDate}
                """;

            XamlMessageBox.Show(versionString, versionString);
        }

        // Gui actions in the TreeView pane
        private void DoMouseDoubleClickInTreeView(object sender, MouseButtonEventArgs e)
        {
            DoOpen(sender, null);
        }
        private void KeyDownInTreeView(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DoOpen(sender, null);
            }
        }
        private void SelectedItemChangedInTreeView(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var asFile = TreeView.SelectedItem as PerfViewFile;
            if (asFile != null)
            {
                StatusBar.Status = "File : " + Path.GetFullPath(asFile.FilePath);
            }
        }
        private void DoTextEnteredInDirectoryTextBox(object sender, RoutedEventArgs e)
        {
            OpenPath(Directory.Text);
        }
        private void DoDrop(object sender, DragEventArgs e)
        {
            var fileNames = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
            // Don't allow multiple drops as it is expensive.
            if (fileNames != null && fileNames.Length > 0)
            {
                Open(fileNames[0]);
            }
        }

        // Context menu in the Treeview pane
        private void CanDoItemHelp(object sender, CanExecuteRoutedEventArgs e)
        {
            var selectedItem = TreeView.SelectedItem as PerfViewTreeItem;
            if (selectedItem != null && selectedItem.HelpAnchor != null)
            {
                e.CanExecute = true;
            }
        }

        private void DoItemHelp(object sender, ExecutedRoutedEventArgs e)
        {
            var selectedItem = TreeView.SelectedItem as PerfViewTreeItem;
            if (selectedItem == null)
            {
                throw new ApplicationException("No item selected.");
            }

            var anchor = selectedItem.HelpAnchor;
            if (anchor == null)
            {
                throw new ApplicationException("Item does not have help.");
            }

            StatusBar.Log("Looking up topic " + anchor + " in Users Guide.");
            DisplayUsersGuide(anchor);
        }

        private void OpenInBrowser(object sender, ExecutedRoutedEventArgs e)
        {
            var selectedReport = TreeView.SelectedItem as PerfViewHtmlReport;
            if (selectedReport == null)
            {
                throw new ApplicationException("No report selected.");
            }

            selectedReport.OpenInExternalBrowser(StatusBar);
        }

        private void CanOpenInBrowser(object sender, CanExecuteRoutedEventArgs e)
        {
            if (TreeView.SelectedItem is PerfViewHtmlReport)
            {
                e.CanExecute = true;
            }
        }

        private void DoRefreshDir(object sender, ExecutedRoutedEventArgs e)
        {
            RefreshCurrentDirectory();
        }
        private void DoOpen(object sender, ExecutedRoutedEventArgs e)
        {
            var selectedItem = TreeView.SelectedItem as PerfViewTreeItem;
            if (selectedItem == null)
            {
                throw new ApplicationException("No item selected.");
            }

            selectedItem.Open(this, StatusBar);
        }
        private void DoClose(object sender, ExecutedRoutedEventArgs e)
        {
            var selectedFile = TreeView.SelectedItem as PerfViewFile;
            if (selectedFile == null)
            {
                throw new ApplicationException("No file selected.");
            }

            // TODO FIX NOW Actually keep track of open windows, also does not track open event windows.
            if (StackWindow.StackWindows.Count != 0)
            {
                throw new ApplicationException("Currently can only close files if all stack windows are closed.");
            }

            if (selectedFile.IsOpened)
            {
                selectedFile.Close();
            }
        }
        private void DoDelete(object sender, ExecutedRoutedEventArgs e)
        {
            var selectedFile = TreeView.SelectedItem as PerfViewFile;
            if (selectedFile == null)
            {
                throw new ApplicationException("No file selected.");
            }

            var response = XamlMessageBox.Show(
                this,
                $"Delete {Path.GetFileName(selectedFile.FilePath)}?",
                "Delete Confirmation",
                MessageBoxButton.OKCancel);

            // TODO does not work with the unmerged files
            if (response == MessageBoxResult.OK)
            {
                string selectedFilePath = selectedFile.FilePath;
                // Delete the file.
                FileUtilities.ForceDelete(selectedFilePath);

                // If it is an ETL file, remove all the other components of an unmerged ETL file.
                if (selectedFilePath.EndsWith(".etl", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string relatedFile in System.IO.Directory.GetFiles(Path.GetDirectoryName(selectedFile.FilePath), Path.GetFileNameWithoutExtension(selectedFilePath) + ".*"))
                    {
                        Match m = Regex.Match(relatedFile, @"\.((clr.*)|(user.*)|(kernel.*)\.etl)$", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            FileUtilities.ForceDelete(relatedFile);
                        }
                    }
                }
            }

            // refresh the directory.
            RefreshCurrentDirectory();
        }

        private void DoRename(object sender, ExecutedRoutedEventArgs e)
        {
            var selectedFile = TreeView.SelectedItem as PerfViewFile;
            if (selectedFile == null)
            {
                throw new ApplicationException("No file selected.");
            }

            string selectedFilePath = selectedFile.FilePath;

            var targetPath = GetDataFileName("Rename File", false, "", null);
            if (targetPath == null)
            {
                StatusBar.Log("Operation Canceled");
                return;
            }

            // Add a ETL suffix if the source has one.
            bool selectedFileIsEtl = selectedFilePath.EndsWith(".etl", StringComparison.OrdinalIgnoreCase);
            if (selectedFileIsEtl && !Path.HasExtension(targetPath))
            {
                targetPath = Path.ChangeExtension(targetPath, ".etl");
            }

            // Do the move.
            FileUtilities.ForceMove(selectedFilePath, targetPath);

            // rename all the other variations of the unmerged file
            if (selectedFileIsEtl && targetPath.EndsWith(".etl", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string relatedFile in System.IO.Directory.GetFiles(Path.GetDirectoryName(selectedFilePath), Path.GetFileNameWithoutExtension(selectedFilePath) + ".*"))
                {
                    Match m = Regex.Match(relatedFile, @"\.((clr.*)|(user.*)|(kernel.*)\.etl)$", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        FileUtilities.ForceMove(relatedFile, Path.ChangeExtension(targetPath, m.Groups[1].Value));
                    }
                }
            }

            // refresh the directory.
            RefreshCurrentDirectory();
        }

        private void CanOpenFolder(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = TreeView.SelectedItem is PerfViewDirectory;
        }

        private void DoOpenFolder(object sender, ExecutedRoutedEventArgs e)
        {
            var directory = (PerfViewDirectory)TreeView.SelectedItem;

            ShellExecute("explorer.exe", directory.FilePath);
        }

        private void CanOpenContainingFolder(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = TreeView.SelectedItem is PerfViewFile;
        }

        private void DoOpenContainingFolder(object sender, ExecutedRoutedEventArgs e)
        {
            var file = (PerfViewFile)TreeView.SelectedItem;

            ShellExecute("explorer.exe", $"/select,\"{file.FilePath}\"");
        }

        private void ShellExecute(string fileName, string arguments)
        {
            try
            {
                Process.Start(fileName, arguments);
            }
            catch (Exception)
            {
            }
        }

        private void DoMakeLocalSymbolDir(object sender, ExecutedRoutedEventArgs e)
        {
            var selectedFile = TreeView.SelectedItem as PerfViewFile;
            if (selectedFile == null)
            {
                throw new ApplicationException("No file selected.");
            }

            var dir = Path.GetDirectoryName(selectedFile.FilePath);
            if (dir.Length == 0)
            {
                dir = ".";
            }

            var symbolDir = Path.Combine(dir, "symbols");
            if (System.IO.Directory.Exists(symbolDir))
            {
                StatusBar.Log("Local symbol directory " + symbolDir + " already exists.");
            }
            else
            {
                System.IO.Directory.CreateDirectory(symbolDir);
                StatusBar.Log("Created local symbol directory " + symbolDir + ".");
            }
        }

        // Misc Gui actions
        private void DoHyperlinkHelp(object sender, ExecutedRoutedEventArgs e)
        {
            var param = e.Parameter as string;
            if (param == null)
            {
                param = "MainViewerQuickStart";       // This is the F1 help
            }

            StatusBar.Log("Looking up topic " + param + " in Users Guide.");
            DisplayUsersGuide(param);
        }
        private void DoReleaseNotes(object sender, RoutedEventArgs e)
        {
            StatusBar.Log("Displaying the release notes.");
            DisplayUsersGuide("ReleaseNotes");
        }
        private void DoReferenceGuide(object sender, RoutedEventArgs e)
        {
            StatusBar.Log("Displaying the reference guide.");
            DisplayUsersGuide("ReferenceGuide");
        }
        private void DoFocusDirectory(object sender, RoutedEventArgs e)
        {
            Directory.Focus();
        }

        private void DoPrivacyPolicy(object sender, RoutedEventArgs e)
        {
            StatusBar.Log("Displaying the privacy policy.");
            Process.Start("https://go.microsoft.com/fwlink/?LinkId=521839");
        }

        private void UpdateFileFilter()
        {
            var filterText = FileFilterTextBox.Text;
            Regex filterRegex = null;
            if (filterText.Length != 0)
            {
                var morphed = Regex.Escape(filterText);
                morphed = "^" + morphed.Replace(@"\*", ".*");
                filterRegex = new Regex(morphed, RegexOptions.IgnoreCase);
            }
            m_CurrentDirectory.Filter = filterRegex;

            var children = m_CurrentDirectory.Children;
            TreeView.ItemsSource = children;
            if (children.Count > 0)
            {
                children[0].IsSelected = true;
            }

            if (children.Count <= 1 && m_CurrentDirectory.Filter != null)
            {
                StatusBar.LogError("WARNING: filter " + FileFilterTextBox.Text + " has excluded all items.");
            }
        }

        private void FilterTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateFileFilter();
        }

        private void FilterKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (m_CurrentDirectory.Children.Count > 0)
                {
                    var selected = TreeView.SelectedItem as PerfViewTreeItem;
                    if (selected == null)
                    {
                        selected = m_CurrentDirectory.Children[0];
                    }

                    selected.Open(this, StatusBar);
                    TreeView.Focus();
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (App.CommandProcessor.CollectingData)
            {
                DoAbort(null, null);
            }

            // DO NOT call Environment.Exit(0) under tests, it will kill the test runner, and tests won't complete.
            if (!_testing)
            {
                Environment.Exit(0);
            }
        }

        // GUI Command objects.
        public static RoutedUICommand CollectCommand = new RoutedUICommand("Collect", "Collect", typeof(MainWindow),
            new InputGestureCollection() { new KeyGesture(Key.C, ModifierKeys.Alt) });
        public static RoutedUICommand RunCommand = new RoutedUICommand("Run", "Run", typeof(MainWindow),
            new InputGestureCollection() { new KeyGesture(Key.R, ModifierKeys.Alt) });
        public static RoutedUICommand AbortCommand = new RoutedUICommand("Abort", "Abort", typeof(MainWindow),
            new InputGestureCollection() { new KeyGesture(Key.A, ModifierKeys.Alt) });
        public static RoutedUICommand MergeCommand = new RoutedUICommand("Merge", "Merge", typeof(MainWindow));
        public static RoutedUICommand ZipCommand = new RoutedUICommand("Zip", "Zip", typeof(MainWindow));
        public static RoutedUICommand UnZipCommand = new RoutedUICommand("UnZip", "UnZip", typeof(MainWindow));
        public static RoutedUICommand ItemHelpCommand = new RoutedUICommand("Help on Item", "ItemHelp", typeof(MainWindow));
        public static RoutedUICommand OpenInBrowserCommand = new RoutedUICommand("Open in Browser", "OpenInBrowser", typeof(MainWindow));
        public static RoutedUICommand UserCommand = new RoutedUICommand("User Command", "UserCommand", typeof(MainWindow),
    new InputGestureCollection() { new KeyGesture(Key.U, ModifierKeys.Alt) });
        public static RoutedUICommand RefreshDirCommand = new RoutedUICommand("Refresh Dir", "RefreshDir",
            typeof(MainWindow), new InputGestureCollection() { new KeyGesture(Key.F5) });
        public static RoutedUICommand OpenCommand = new RoutedUICommand("Open", "Open", typeof(MainWindow));
        public static RoutedUICommand DeleteCommand = new RoutedUICommand("Delete", "Delete", typeof(MainWindow),
            new InputGestureCollection() { new KeyGesture(Key.Delete) });   // TODO is this shortcut a good idea?
        public static RoutedUICommand RenameCommand = new RoutedUICommand("Rename", "Rename", typeof(MainWindow));
        public static RoutedUICommand OpenFolderCommand = new RoutedUICommand("Open _Folder", "OpenFolder", typeof(MainWindow));
        public static RoutedUICommand OpenContainingFolderCommand = new RoutedUICommand("Open Containing _Folder", "OpenContainingFolder", typeof(MainWindow));
        public static RoutedUICommand MakeLocalSymbolDirCommand = new RoutedUICommand(
            "Make Local Symbol Dir", "MakeLocalSymbolDir", typeof(MainWindow));
        public static RoutedUICommand CloseCommand = new RoutedUICommand("Close", "Close", typeof(MainWindow));
        public static RoutedUICommand CancelCommand = new RoutedUICommand("Cancel", "Cancel", typeof(EventWindow),
            new InputGestureCollection() { new KeyGesture(Key.Escape) });
        public static RoutedUICommand UsersGuideCommand = new RoutedUICommand("UsersGuide", "UsersGuide", typeof(MainWindow));
        public static RoutedUICommand HeapSnapshotCommand = new RoutedUICommand("Take Heap Snapshot", "HeapSnapshot", typeof(MainWindow),
            new InputGestureCollection() { new KeyGesture(Key.S, ModifierKeys.Alt) });
        public static RoutedUICommand DirectorySizeCommand = new RoutedUICommand("Directory Size", "DirectorySize", typeof(MainWindow),
            new InputGestureCollection() { new KeyGesture(Key.D, ModifierKeys.Alt) });
        public static RoutedUICommand ImageSizeCommand = new RoutedUICommand("Image Size", "ImageSize", typeof(MainWindow),
            new InputGestureCollection() { new KeyGesture(Key.I, ModifierKeys.Alt) });
        public static RoutedUICommand ILSizeCommand = new RoutedUICommand("IL Size", "ILSize", typeof(MainWindow),
            new InputGestureCollection() { new KeyGesture(Key.L, ModifierKeys.Alt) });

        public static RoutedUICommand HeapSnapshotFromDumpCommand = new RoutedUICommand("Take Heap Snapshot from Process Dump", "HeapSnapshotFromDump",
            typeof(MainWindow));
        public static RoutedUICommand FocusDirectoryCommand = new RoutedUICommand("Focus Directory", "FocusDirectory", typeof(MainWindow),
            new InputGestureCollection() { new KeyGesture(Key.L, ModifierKeys.Control) });
        #region private
        internal static List<string> ParseWordsOrQuotedStrings(string commandAndArgs)
        {
            var words = new List<string>();

            // Match the next work or quoted string
            // TODO quoting quotes
            var regex = new Regex("\\s*(([^\"]\\S*)|(\"[^\"]*\"))");
            int cur = 0;
            while (cur < commandAndArgs.Length)
            {
                var m = regex.Match(commandAndArgs, cur);
                if (!m.Success)
                {
                    break;
                }

                var start = m.Groups[1].Index;
                var len = m.Groups[1].Length;
                cur = start + len + 1;

                // Remove the quotes if necessary.
                if (commandAndArgs[start + len - 1] == '"')
                {
                    --len;
                }

                if (commandAndArgs[start] == '"')
                {
                    start++;
                    --len;
                }
                words.Add(commandAndArgs.Substring(start, len));
            }
            return words;
        }

        /// <summary>
        /// If we can't write to the directory as a normal user, change the directory to your home directory.
        /// This is useful if PerfVIew is launch from embeded E-mail to avoid writing in \Program Files
        /// </summary>
        private void ChangeCurrentDirectoryIfNeeded()
        {
            // See if the current directory is writable

            bool changDir = false;
            var curDir = Environment.CurrentDirectory;
            if (string.Compare(curDir, 1, @":\windows\System32", 0, 18, StringComparison.OrdinalIgnoreCase) == 0)
            {
                // ETW will refuse to write files int system32 and if people put PerfView there it will end up trying to do so.
                changDir = true;
            }
            else
            {
                try
                {
                    var testFile = Path.Combine(curDir, "PerfViewData.testfile");
                    File.Open(testFile, FileMode.Create, FileAccess.Write).Close();
                    File.Delete(testFile);
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    changDir = true;
                }
                catch (Exception) { }
            }
            if (changDir)
            {
                // No then change directory to my documents directory
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                StatusBar.Log("Current Directory " + Environment.CurrentDirectory + " is not writable, changing to " + docs);
                Environment.CurrentDirectory = docs;
            }
        }
        private PerfViewFile GetSelectedPerfViewData()
        {
            // TODO get item under cursor, not selected item
            var selectedItem = TreeView.SelectedItem as PerfViewFile;
            if (selectedItem == null)
            {
                throw new ApplicationException("No data file Selected.");
            }

            return selectedItem;
        }
        internal string GetDataFileName(string title, bool shouldExist, string fileName, string filter)
        {
            // TODO should use SaveFileDialog sometimes.
            StatusBar.Status = "";
            var openDialog = new Microsoft.Win32.OpenFileDialog();
            openDialog.FileName = fileName;
            openDialog.InitialDirectory = CurrentDirectory.FilePath;
            openDialog.Title = title;
            openDialog.DefaultExt = Path.GetExtension(fileName);
            openDialog.Filter = filter;     // Filter files by extension
            openDialog.AddExtension = true;
            openDialog.ReadOnlyChecked = shouldExist;
            openDialog.CheckFileExists = shouldExist;

            // Show open file dialog box
            Nullable<bool> result = openDialog.ShowDialog();
            if (result == true)
            {
                return (openDialog.FileName);
            }
            else
            {
                StatusBar.LogError("Operation canceled.");
            }

            return null;
        }
        internal static bool DisplayUsersGuide(string anchor = null)
        {
            // This is hack because of issues when we pass non-ascii characters to the WPF browser control.  Spawn a true browser
            // and use its current directory to avoid needing to pass the non-ascii characters.
            if (!IsPrintableAscii(SupportFiles.SupportFileDir))
            {
                Command.Run("UsersGuide.htm", new CommandOptions().AddCurrentDirectory(SupportFiles.SupportFileDir).AddStart());
                return true;
            }

            if (s_Browser == null)
            {
                s_Browser = new WebBrowserWindow(GuiApp.MainWindow);
                s_Browser.Title = "PerfView Help";

                // When you simply navigate, you don't remember your position.  In the case
                // Where the browser was closed you can at least fix it easily by starting over.
                // Thus we abandon browsers on close.
                s_Browser.Closing += delegate
                {
                    // WebBrowserWindow will dispose itself in Window_Closing
                    s_Browser = null;
                };

                s_Browser.Browser.NavigationStarting += delegate (object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
                {
                    if (e.Uri != null && Uri.TryCreate(e.Uri, UriKind.Absolute, out Uri uri) && !string.IsNullOrEmpty(uri.Host))
                    {
                        if (!GuiApp.MainWindow.AllowNavigateToWeb)
                        {
                            GuiApp.MainWindow.StatusBar.LogError("Navigating to web disallowed, canceling.");
                            e.Cancel = true;
                        }
                        else
                        {
                            OpenExternalBrowser(uri);
                            e.Cancel = true;
                        }
                    }
                };
            }

            string usersGuideFilePath = Path.Combine(SupportFiles.SupportFileDir, "UsersGuide.htm");
            string url = "file://" + usersGuideFilePath.Replace('\\', '/').Replace(" ", "%20");

            if (!string.IsNullOrEmpty(anchor))
            {
                url = url + "#" + anchor;
            }

            s_Browser.Source = new Uri(url);
            if (s_Browser.WindowState == WindowState.Minimized)
            {
                s_Browser.WindowState = WindowState.Normal;
            }

            s_Browser.Show();
            s_Browser._Browser.Focus();
            return true;
        }

        private static bool IsPrintableAscii(string url)
        {
            for (int i = 0; i < url.Length; i++)
            {
                char c = url[i];
                if (!(' ' <= c && c <= 'z'))
                {
                    return false;
                }
            }
            return true;
        }

        private static void OpenExternalBrowser(Uri uri)
        {
            Process.Start(uri.ToString());
        }

        private bool AllowNavigateToWeb
        {
            get
            {
                if (!m_AllowNavigateToWeb)
                {
                    var allowNavigateToWeb = App.UserConfigData["AllowNavigateToWeb"];
                    m_AllowNavigateToWeb = allowNavigateToWeb == "true";
                    if (!m_AllowNavigateToWeb)
                    {
                        var result = XamlMessageBox.Show(
                            """
                            PerfView is about to open content on the web.
                            Is this OK?
                            """,
                            "Navigate to Web",
                            MessageBoxButton.YesNo);

                        if (result == MessageBoxResult.Yes)
                        {
                            m_AllowNavigateToWeb = true;
                            App.UserConfigData["AllowNavigateToWeb"] = "true";
                        }
                    }
                }
                return m_AllowNavigateToWeb;
            }
        }

        private bool m_AllowNavigateToWeb;

        private PerfViewDirectory m_CurrentDirectory;
        private static WebBrowserWindow s_Browser;
        private UserCommandDialog m_UserDefineCommandDialog;
        #endregion

        /// <summary>
        /// Indicates that 'outputFileName' should be opened after the command is completed.
        /// </summary>
        public void OpenNext(string fileName)
        {
            m_openNextFileName = fileName;
        }

        private string m_openNextFileName;

        /// <summary>/
        /// When you right click an item in the TreeView it doesn't automatically change to the TreeViewItem you clicked on.
        /// This helper method changes focus so that the right-click menu items commands are bound to the right TreeViewItem
        /// </summary>
        private void TreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem treeViewItem = FindTreeViewItemInVisualHeirarchy(e.OriginalSource as DependencyObject);

            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Given an item in visual a tree, navigate the parents upwards until we find the TreeViewItem it represents.
        /// </summary>
        private static TreeViewItem FindTreeViewItemInVisualHeirarchy(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
            {
                source = VisualTreeHelper.GetParent(source);
            }

            return source as TreeViewItem;
        }

        /// <summary>
        /// Handler for when <see cref="AuthenticationCommands.UseGitCredentialManager"/> command is executed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        private void UseGCMAuth_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            AuthenticationViewModel.IsGitCredentialManagerEnabled = !AuthenticationViewModel.IsGitCredentialManagerEnabled;
            UpdateSymbolReaderHandler();
            e.Handled = true;
        }

        /// <summary>
        /// Handler to determine if <see cref="AuthenticationCommands.UseGitCredentialManager"/> should be enabled.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        private void UseGCMAuth_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = GitCredentialManagerHandler.IsGitCredentialManagerInstalled;
        }

        /// <summary>
        /// Handler for when <see cref="AuthenticationCommands.UseDeveloperIdentity"/> command is executed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        private void UseDeveloperIdentityAuth_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            AuthenticationViewModel.IsDeveloperIdentityEnabled = !AuthenticationViewModel.IsDeveloperIdentityEnabled;
            UpdateSymbolReaderHandler();
            e.Handled = true;
        }

        /// <summary>
        /// Handler for when <see cref="AuthenticationCommands.UseGitHubDeviceFlow"/> command is executed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        private void UseGitHubDeviceFlowAuth_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            AuthenticationViewModel.IsGitHubDeviceFlowEnabled = !AuthenticationViewModel.IsGitHubDeviceFlowEnabled;
            UpdateSymbolReaderHandler();
            e.Handled = true;
        }

        /// <summary>
        /// Handler for when <see cref="AuthenticationCommands.UseBasicHttpAuth"/> command is executed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        private void UseBasicHttpAuth_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            AuthenticationViewModel.IsBasicHttpAuthEnabled = !AuthenticationViewModel.IsBasicHttpAuthEnabled;
            UpdateSymbolReaderHandler();
            e.Handled = true;
        }

        /// <summary>
        /// Handler for when <see cref="ThemeViewModel.SetThemeCommand"/> command is executed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        private void SetTheme_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Theme theme = ((ThemeViewModel.SetThemeCommand)e.Command).Theme;
            ThemeViewModel.SetTheme(theme);

            XamlMessageBox.Show("Restart PerfView to apply theme changes.");

            e.Handled = true;
        }

        /// <summary>
        /// A cached instance of <see cref="SymbolReaderAuthenticationHandler"/>.
        /// We can't just keep a singleton because the App's SymbolReader
        /// occasionally gets recreated from scratch. We need a way to
        /// both configure an existing, live instance (when you enable
        /// or disable existing authentication providers) and to create
        /// new, configured handlers on demand.
        /// </summary>
        private SymbolReaderAuthenticationHandler _cachedSymbolReaderHandler;

        /// <summary>
        /// Update the current handler, if there is one. If the
        /// current instance has been disposed, then nothing happens.
        /// A a new one will be created and configured in the
        /// <see cref="GetSymbolReaderHandler(TextWriter)"/> callback.
        /// </summary>
        private void UpdateSymbolReaderHandler()
        {
            SymbolReaderAuthenticationHandler handler = _cachedSymbolReaderHandler;
            if (handler != null)
            {
                if (handler.IsDisposed)
                {
                    _cachedSymbolReaderHandler = null;
                }
                else
                {
                    handler.Configure(AuthenticationViewModel, App.CommandProcessor.LogFile, this);
                }
            }
        }

        /// <summary>
        /// Provides an instance of a <see cref="DelegatingHandler"/> to use
        /// in the <see cref="Microsoft.Diagnostics.Symbols.SymbolReader"/> constructor.
        /// This is called when we construct a new symbol reader
        /// in <see cref="App.GetSymbolReader(string, Microsoft.Diagnostics.Symbols.SymbolReaderOptions)"/>.
        /// </summary>
        /// <param name="log">The logger to use.</param>
        /// <returns>The handler.</returns>
        private DelegatingHandler GetSymbolReaderHandler(TextWriter log)
        {
            SymbolReaderAuthenticationHandler handler = _cachedSymbolReaderHandler;
            if (handler == null || handler.IsDisposed)
            {
                log?.WriteLine("Creating authentication handler for {0}.", AuthenticationViewModel);
                handler = _cachedSymbolReaderHandler = new SymbolReaderAuthenticationHandler();
            }

            handler.Configure(AuthenticationViewModel, log, this);
            return handler;
        }
    }
}
