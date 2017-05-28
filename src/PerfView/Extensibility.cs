using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Stacks;
using Graphs;
using PerfView;
using PerfView.GuiUtilities;
using PerfViewModel;
using Microsoft.Diagnostics.Symbols;
using Utilities;
using FastSerialization;
using Microsoft.Diagnostics.Utilities;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Session;
using Diagnostics.Tracing.StackSources;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Address = System.UInt64;
using System.Threading.Tasks;
using System.ComponentModel;
using PerfView.Dialogs;
using EventSources;

namespace PerfViewExtensibility
{
    /// <summary>
    /// CommandEnvironment defines the 'primitive starting points' that a PerfView Extension uses
    /// to start doing interesting things.   
    /// </summary>
    public class CommandEnvironment
    {
        // logging 
        /// <summary>
        /// The log file is a place to write verbose diagnostics.  It is either the console or the GUI log, or
        /// a file that use redirected logging to with the /logFile=XXX qualifier.
        /// </summary>
        public static TextWriter LogFile { get { return App.CommandProcessor.LogFile; } }

        // File I/O
        /// <summary>
        /// Open an ETL file (same as new TraceLog(etlFileName))
        /// </summary>
        /// <param name="etlFileName"></param>
        /// <returns></returns>
        public static ETLDataFile OpenETLFile(string etlFileName) { return new ETLDataFile(etlFileName); }
        /// <summary>
        /// Opens a *.perfView.xml.zip or *.perfview.xml 
        /// </summary>
        public static Stacks OpenPerfViewXmlFile(string perfViewXmlFileName)
        {
            var guiState = new StackWindowGuiState();
            var source = new XmlStackSource(perfViewXmlFileName, delegate (XmlReader reader)
            {
                if (reader.Name == "StackWindowGuiState")
                    guiState.ReadFromXml(reader);
                // These are here for backward compatibility.  Can remove after 2013.  
                else if (reader.Name == "FilterXml")
                    guiState.FilterGuiState.ReadFromXml(reader);
                else if (reader.Name == "Log")
                    guiState.Log = reader.ReadElementContentAsString().Trim();
                else if (reader.Name == "Notes")
                    guiState.Notes = reader.ReadElementContentAsString().Trim();
                else
                    reader.Skip();
            });
            var ret = new Stacks(source, perfViewXmlFileName);
            ret.GuiState = guiState;
            ret.Name = perfViewXmlFileName;
            return ret;
        }
        /// <summary>
        /// Opens a GCHeap dump (created with HeapSnapshot)
        /// </summary>
        public static Stacks OpenGCDumpFile(string gcDumpFileName)
        {
            var log = App.CommandProcessor.LogFile;
            var gcDump = new GCHeapDump(gcDumpFileName);

            Graph graph = gcDump.MemoryGraph;

            log.WriteLine(
                   "Opened Graph {0} Bytes: {1:f3}M NumObjects: {2:f3}K  NumRefs: {3:f3}K Types: {4:f3}K RepresentationSize: {5:f1}M",
                   gcDumpFileName, graph.TotalSize / 1000000.0, (int)graph.NodeIndexLimit / 1000.0,
                   graph.TotalNumberOfReferences / 1000.0, (int)graph.NodeTypeIndexLimit / 1000.0,
                   graph.SizeOfGraphDescription() / 1000000.0);

#if false // TODO FIX NOW remove
            using (StreamWriter writer = File.CreateText(Path.ChangeExtension(this.FilePath, ".heapDump.xml")))
            {
                ((MemoryGraph)graph).DumpNormalized(writer);
            }
#endif
            var retSource = new MemoryGraphStackSource(graph, log, gcDump.CountMultipliersByType);

            // Set the sampling ratio so that the number of objects does not get too far out of control.  
            if (2000000 <= (int)graph.NodeIndexLimit)
            {
                retSource.SamplingRate = ((int)graph.NodeIndexLimit / 1000000);
                log.WriteLine("Setting the sampling rate to {0} to keep processing under control.", retSource.SamplingRate);
            }

            // Figure out the multiplier 
            string extraTopStats = "";
            if (gcDump.CountMultipliersByType != null)
            {
                extraTopStats += string.Format(" Heap Sampled: Mean Count Multiplier {0:f2} Mean Size Multiplier {1:f2}",
                        gcDump.AverageCountMultiplier, gcDump.AverageSizeMultiplier);
            }

            log.WriteLine("Type Histograph > 1% of heap size");
            log.Write(graph.HistogramByTypeXml(graph.TotalSize / 100));

            // TODO FIX NOW better name. 
            var retStacks = new Stacks(retSource, "GC Heap Dump of " + Path.GetFileName(gcDumpFileName));
            retStacks.m_fileName = gcDumpFileName;
            retStacks.ExtraTopStats = extraTopStats;
            return retStacks;
        }

        // Data Collection
        /// <summary>
        /// Runs the command 'commandLine' with ETW enabled.   Creates  data file 'outputFileName'.  
        /// By default this is a Zipped ETL file.  
        /// </summary>  
        /// <param name="commandLine">The command line to run.</param>
        /// <param name="dataFile">The data file to place the profile data.  If omitted it is the parsedParams.DataFile</param>
        /// <param name="parsedCommandLine">Any other arguments for the run command.  
        /// If omitted it is inherited from the CommandEnvironment.CommandLineArgs </param>
        public static void Run(string commandLine, string dataFile = null, CommandLineArgs parsedCommandLine = null)
        {
            if (parsedCommandLine == null)
                parsedCommandLine = App.CommandLineArgs;

            parsedCommandLine.CommandLine = commandLine;
            if (dataFile != null)
                parsedCommandLine.DataFile = dataFile;
            App.CommandProcessor.Run(parsedCommandLine);
        }
        /// <summary>
        /// Run the 'Collect command creating the data file outputFileName.  By default this is a Zipped ETL file.  
        /// </summary>
        /// <param name="dataFile">The data file to place the profile data.  If omitted it is the parsedParams.DataFile</param>
        /// <param name="parsedCommandLine">Any other arguments for the run command.  
        /// If ommited it is inherited from the CommandEnvironment.CommandLineArgs </param>
        public static void Collect(string dataFile = null, CommandLineArgs parsedCommandLine = null)
        {
            if (parsedCommandLine == null)
                parsedCommandLine = App.CommandLineArgs;

            if (dataFile != null)
                parsedCommandLine.DataFile = dataFile;
            App.CommandProcessor.Collect(parsedCommandLine);
        }
        /// <summary>
        /// Collect a heap snapshot from 'process' placing the data in 'outputFileName' (a gcdump file)
        /// </summary>
        /// <param name="process">The name of the process or the process ID.  
        /// If there is more than one process with the same name, the one that was started LAST is chosen. </param>
        /// <param name="outputFileName">The data file (.gcdump) to generate.</param>
        public static void HeapSnapshot(string process, string outputFileName = null)
        {
            CommandLineArgs.Process = process;
            if (outputFileName != null)
                CommandLineArgs.DataFile = outputFileName;
            App.CommandProcessor.HeapSnapshot(CommandLineArgs);
        }
        /// <summary>
        /// Collect a heap snapshot from a process dump file 'dumpFile'  placing the data in 'outputFileName' (a gcdump file)
        /// </summary>
        /// <param name="inputDumpFile">The dump file to extract the heap from.</param>
        /// <param name="outputFileName">The data file (.gcdump) to generate. </param>
        public static void HeapSnapshotFromProcessDump(string inputDumpFile, string outputFileName = null)
        {
            CommandLineArgs.ProcessDumpFile = inputDumpFile;
            if (outputFileName != null)
                CommandLineArgs.DataFile = outputFileName;
            App.CommandProcessor.HeapSnapshot(CommandLineArgs);
        }

        // gui operations 
        /// <summary>
        /// Open a new stack viewer GUI window in the dat in 'stacks'
        /// </summary>
        /// <param name="stacks"></param>
        /// <param name="OnOpened"></param>
        public static void OpenStackViewer(Stacks stacks, Action<StackWindow> OnOpened = null)
        {
            GuiApp.MainWindow.Dispatcher.BeginInvoke((Action)delegate ()
            {
                // TODO FIX NOW Major hacks. 
                PerfViewStackSource perfViewStackSource;
                string filePath;
                if (stacks.m_EtlFile != null)
                {
                    filePath = stacks.m_EtlFile.FilePath;
                    ETLPerfViewData file = (ETLPerfViewData)PerfViewFile.Get(filePath);
                    file.OpenWithoutWorker(GuiApp.MainWindow, GuiApp.MainWindow.StatusBar);
                    var stackSourceName = stacks.Name.Substring(0, stacks.Name.IndexOf(" file"));
                    perfViewStackSource = file.GetStackSource(stackSourceName);
                    if (perfViewStackSource == null)
                        perfViewStackSource = new PerfViewStackSource(file, "");
                }
                else
                {
                    if (stacks.m_fileName != null)
                        filePath = stacks.m_fileName;
                    else
                        filePath = stacks.Name;
                    if (string.IsNullOrWhiteSpace(filePath))
                        filePath = "X.PERFVIEW.XML";            // MAJOR HACK.

                    var perfViewFile = PerfViewFile.Get(filePath);
                    var gcDumpFile = perfViewFile as HeapDumpPerfViewFile;
                    if (gcDumpFile != null)
                    {
                        gcDumpFile.OpenWithoutWorker(GuiApp.MainWindow, GuiApp.MainWindow.StatusBar);
                        var gcDump = gcDumpFile.GCDump;
                        if (gcDump.CreationTool != null && gcDump.CreationTool == "ILSize")
                        {
                            // Right now we set nothing.  
                            stacks.GuiState = new StackWindowGuiState();
                            stacks.GuiState.Columns = new List<string> { "NameColumn",
                                "ExcPercentColumn", "ExcColumn", "ExcCountColumn",
                                "IncPercentColumn", "IncColumn", "IncCountColumn",
                                "FoldColumn", "FoldCountColumn" };
                        }
                        perfViewStackSource = gcDumpFile.GetStackSource();

                    }
                    else
                    {
                        var xmlFile = perfViewFile as XmlPerfViewFile;
                        if (xmlFile == null)
                            throw new ApplicationException("Currently only support ETL files and XML files");
                        perfViewStackSource = new PerfViewStackSource(xmlFile, "");
                    }
                }
                var stackWindow = new StackWindow(GuiApp.MainWindow, perfViewStackSource);
                if (stacks.HasGuiState)
                    stackWindow.RestoreWindow(stacks.GuiState, filePath);
                stackWindow.Filter = stacks.Filter;
                stackWindow.SetStackSource(stacks.StackSource, delegate
                {
                    if (stacks.HasGuiState)
                        stackWindow.GuiState = stacks.GuiState;
                    else
                        perfViewStackSource.ConfigureStackWindow(stackWindow);

                    LogFile.WriteLine("[Opened stack viewer {0}]", filePath);
                    OnOpened?.Invoke(stackWindow);
                });
                stackWindow.Show();
            });
        }
        /// <summary>
        /// Open a new EventViewer with the given set of events
        /// </summary>
        /// <param name="events"></param>
        /// <param name="OnOpened">If non-null an action to perform after the window is opened (on the GUI thread)</param>
        /// <returns></returns>
        public static void OpenEventViewer(Events events, Action<EventWindow> OnOpened = null)
        {
            GuiApp.MainWindow.Dispatcher.BeginInvoke((Action)delegate ()
            {
                // TODO FIX NOW this is probably a hack?
                var file = PerfViewFile.Get(events.m_EtlFile.FilePath);
                var eventSource = new PerfViewEventSource(file);
                eventSource.m_eventSource = events;
                eventSource.Viewer = new EventWindow(GuiApp.MainWindow, eventSource);
                eventSource.Viewer.Show();
                if (OnOpened != null)
                    eventSource.Viewer.Loaded += delegate { OnOpened(eventSource.Viewer); };
            });
        }
        /// <summary>
        /// Displays an HTML file htmlFilePath, (typically created using CommandEnvironment.CreateUniqueCacheFileName()) 
        /// in a new window.  
        /// </summary>
        /// <param name="htmlFilePath">The path to the file containing the HTML.</param>
        /// <param name="title">The title for the new window.</param>
        /// <param name="DoCommand">Docommand(string command, TextWriter log), is a action that is called any time a URL of the
        /// form <a href="command:XXXX"></a> is clicked on.   The XXXX is passed as the command and a TextWriter that can display
        /// messages to the windows log is given, and a handle to the web browser window itself.   Messages surrounded by [] in 
        /// the log are also displayed on the windows one line status bar.   Thus important messages should be surrounded by [].   
        /// This callback is NOT on the GUI thread, so you have to use the window.Dispatcher.BeginInvoke() to cause actions on 
        /// the GUI thread.</param>
        /// <param name="OnOpened">If non-null an action to perform after the window is opened (on the GUI thread)</param>
        public static void OpenHtmlReport(string htmlFilePath, string title,
            Action<string, TextWriter, WebBrowserWindow> DoCommand = null, Action<WebBrowserWindow> OnOpened = null)
        {
            GuiApp.MainWindow.Dispatcher.BeginInvoke((Action)delegate ()
            {
                var viewer = new WebBrowserWindow();
                viewer.Browser.Navigating += delegate (object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
                {
                    if (e.Uri != null)
                    {
                        if (e.Uri.Scheme == "command")
                        {
                            e.Cancel = true;
                            if (viewer.StatusBar.Visibility != System.Windows.Visibility.Visible)
                                viewer.StatusBar.Visibility = System.Windows.Visibility.Visible;
                            viewer.StatusBar.StartWork("Following Hyperlink", delegate ()
                            {
                                if (DoCommand != null)
                                    DoCommand(e.Uri.LocalPath, viewer.StatusBar.LogWriter, viewer);
                                else
                                    viewer.StatusBar.Log("This view does not support command URLs.");
                                viewer.StatusBar.EndWork(null);
                            });
                        }
                    }
                };
                viewer.Width = 1000;
                viewer.Height = 600;
                viewer.Title = title;
                WebBrowserWindow.Navigate(viewer.Browser, Path.GetFullPath(htmlFilePath));
                viewer.Show();
                if (OnOpened != null)
                    viewer.Loaded += delegate { OnOpened(viewer); };

            });
        }
        /// <summary>
        /// Open Excel on csvFilePath.   
        /// </summary>
        public static void OpenExcel(string csvFilePath)
        {
            LogFile.WriteLine("[Opening CSV on {0}]", csvFilePath);
            Command.Run(Command.Quote(csvFilePath), new CommandOptions().AddStart().AddTimeout(CommandOptions.Infinite));
        }
        /// <summary>
        /// Opens the log window if this is running under the GUI, otherwise does nothing.  
        /// </summary>
        public static void OpenLog()
        {
            if (App.CommandLineArgs.NoGui)
                return;
            GuiApp.MainWindow.Dispatcher.BeginInvoke((Action)delegate ()
            {
                GuiApp.MainWindow.StatusBar.OpenLog();
            });
        }

        // environment support 
        /// <summary>
        /// The command lines passed to perfView itself.  These are also populated by default values.
        /// Setting these values in CommandLineArgs will cause the commands below to use the updated values. 
        /// </summary>
        public static CommandLineArgs CommandLineArgs { get { return App.CommandLineArgs; } }
        /// <summary>
        /// ConfigData is a set of key-value dictionary that is persisted (as AppData\Roaming\PerfView\UserConfig.xml)
        /// so it is remembered across invocations of the program.  
        /// </summary>
        public static ConfigData ConfigData { get { return App.ConfigData; } }
        /// <summary>
        /// This is a directory where you can place temporary files.   These files will be cleaned up
        /// eventually if the number grows too large.   (this is %TEMP%\PerfView)
        /// </summary>
        public static string CacheFileDirectory { get { return CacheFiles.CacheDir; } }
        /// <summary>
        /// Creates a file name that is in the CacheFileDirectory that will not collide with any other file.  
        /// You give it the base name (file name without extension), as well as the extension, and it returns
        /// a full file path that is guaranteed to be unique.  
        /// </summary>
        /// <param name="baseFilePath">The file name without extension (a suffix will be added to this)</param>
        /// <param name="extension">optionally an extension to add to the file name (must have a . in front)</param>
        /// <returns>The full path of a unique file in the CacheFileDirectory</returns>
        public static string CreateUniqueCacheFileName(string baseFilePath, string extension = "")
        {
            return CacheFiles.FindFile(baseFilePath, extension);
        }
        /// <summary>
        /// This is the directory that all extensions live in (e.g. PerfViewExtensibity next to PerfView.exe)
        /// </summary>
        public static string ExtensionsDirectory { get { return Extensions.ExtensionsDirectory; } }
        /// <summary>
        /// This is the directory where support files can go (.e.g AppData\Roaming\PerfView\VER.*)
        /// </summary>
        public static string SupportFilesDirectory { get { return SupportFiles.SupportFileDir; } }
    }

    public class DataFile : IDisposable
    {        /// <summary>
             /// The path of the data file
             /// </summary>
        public string FilePath { get { return m_FilePath; } }
        public void Close() { Dispose(); }
        public virtual void Dispose() { }

        #region private
        protected string m_FilePath;
        #endregion
    }

    /// <summary>
    /// ETL file is a simplified wrapper over the TraceLog class, which represents an ETL data file.
    /// If you need 'advanced' features access the TraceLog property.  
    /// </summary>
    public class ETLDataFile : DataFile
    {
        // We have the concept of a process to focus on.  All STACK sampling will be filtered by this.  
        // If null, then no filtering is done.   Do try to limit to one process if possible as it makes
        // analysis and symbol lookup faster.  
        public void SetFilterProcess(TraceProcess process) { m_FilterProcess = process; }
        public void SetFilterProcess(string name) { m_FilterProcess = Processes.LastProcessWithName(name); }
        public void SetFilterProcess(int processId) { m_FilterProcess = Processes.LastProcessWithID(processId); }
        public TraceProcess FilterProcess { get { return m_FilterProcess; } }

        /// <summary>
        /// ETL files collect machine wide, but it is good practice to focus on a particular process
        /// as quickly as possible.   TraceProcesses is a shortcut to TraceLog.Process that let you
        /// find a process of interest quickly.  
        /// </summary>
        public TraceProcesses Processes { get { return TraceLog.Processes; } }
        /// <summary>
        /// If the ETL file has stack samples, fetch them.  
        /// </summary>
        public Stacks CPUStacks(bool loadAllCachedSymbols = false)
        {
            return new Stacks(TraceLog.CPUStacks(m_FilterProcess), "CPU", this, loadAllCachedSymbols);
        }
        /// <summary>
        /// If the ETL file has stack samples, fetch them.  
        /// </summary>
        public Stacks ThreadTimeStacks(bool loadAllCachedSymbols = false)
        {
            return new Stacks(TraceLog.ThreadTimeStacks(m_FilterProcess), "Thread Time", this, loadAllCachedSymbols);
        }
        /// <summary>
        /// If the ETL file has stack samples, fetch them as an activity aware stack source.
        /// </summary>
        public Stacks ThreadTimeWithTasksStacks(bool loadAllCachedSymbols = false)
        {
            return new Stacks(TraceLog.ThreadTimeWithTasksStacks(m_FilterProcess), "Thread Time (with Tasks)", this, loadAllCachedSymbols);
        }
        /// <summary>
        /// Get the list of raw events.  
        /// </summary>
        public Events Events { get { return new Events(this); } }

        /// <summary>
        /// Open an existing ETL file by name
        /// </summary>
        /// <param name="fileName">The name of the ETL file to open</param>
        /// <param name="onLostEvents">If non-null, this method is called when there are lost events (with the count of lost events)</param>
        public ETLDataFile(string fileName, Action<bool, int, int> onLostEvents = null)
        {
            var log = App.CommandProcessor.LogFile;
            m_FilePath = fileName;

            var etlOrEtlXFileName = FilePath;
            UnZipIfNecessary(ref etlOrEtlXFileName, log);

            for (;;)  // RETRY Loop 
            {
                var usedAnExistingEtlxFile = false;
                var etlxFileName = etlOrEtlXFileName;
                if (etlOrEtlXFileName.EndsWith(".etl", StringComparison.OrdinalIgnoreCase))
                {
                    etlxFileName = CacheFiles.FindFile(etlOrEtlXFileName, ".etlx");
                    if (File.Exists(etlxFileName) && File.GetLastWriteTimeUtc(etlOrEtlXFileName) <= File.GetLastWriteTimeUtc(etlxFileName))
                    {
                        usedAnExistingEtlxFile = true;
                        log.WriteLine("Found a existing ETLX file in cache: {0}", etlxFileName);
                    }
                    else
                    {
                        var options = new TraceLogOptions();
                        options.ConversionLog = log;
                        if (App.CommandLineArgs.KeepAllEvents)
                            options.KeepAllEvents = true;
                        options.MaxEventCount = App.CommandLineArgs.MaxEventCount;
                        options.SkipMSec = App.CommandLineArgs.SkipMSec;
                        options.OnLostEvents = onLostEvents;
                        options.LocalSymbolsOnly = false;
                        options.ShouldResolveSymbols = delegate (string moduleFilePath) { return false; };

                        log.WriteLine("Creating ETLX file {0} from {1}", etlxFileName, etlOrEtlXFileName);
                        TraceLog.CreateFromEventTraceLogFile(etlOrEtlXFileName, etlxFileName, options);

                        var dataFileSize = "Unknown";
                        if (File.Exists(etlOrEtlXFileName))
                            dataFileSize = ((new System.IO.FileInfo(etlOrEtlXFileName)).Length / 1000000.0).ToString("n3") + " MB";

                        log.WriteLine("ETL Size {0} ETLX Size {1:n3} MB",
                            dataFileSize, (new System.IO.FileInfo(etlxFileName)).Length / 1000000.0);
                    }
                }
                // At this point we have an etlxFileName set, so we can open the TraceLog file. 
                try
                {
                    m_TraceLog = new TraceLog(etlxFileName);
                }
                catch (Exception)
                {
                    // Failure! If we used an existing ETLX file we should try to regenerate the file 
                    if (usedAnExistingEtlxFile)
                    {
                        log.WriteLine("Could not open the ETLX file, regenerating...");
                        FileUtilities.ForceDelete(etlxFileName);
                        if (!File.Exists(etlxFileName))
                            continue;       // Retry 
                    }
                    throw;
                }
                break;
            }

            // Yeah we have opened the log file!
            if (App.CommandLineArgs.UnsafePDBMatch)
                m_TraceLog.CodeAddresses.UnsafePDBMatching = true;
        }
        /// <summary>
        /// Lookup all symbols for any module with 'simpleFileName'.   If processID==0 then all processes are searched. 
        /// </summary>
        /// <param name="simpleModuleName">The name of the module without directory or extension</param>
        /// <param name="symbolFlags">Options for symbol reading</param>
        public void LookupSymbolsForModule(string simpleModuleName, SymbolReaderOptions symbolFlags = SymbolReaderOptions.None)
        {
            var symbolReader = GetSymbolReader(symbolFlags);
            var log = App.CommandProcessor.LogFile;

            // Remove any extensions.  
            simpleModuleName = Path.GetFileNameWithoutExtension(simpleModuleName);

            // If we have a process, look the DLL up just there
            var moduleFiles = new Dictionary<int, TraceModuleFile>();
            if (m_FilterProcess != null)
            {
                foreach (var loadedModule in m_FilterProcess.LoadedModules)
                {
                    var baseName = Path.GetFileNameWithoutExtension(loadedModule.Name);
                    if (string.Compare(baseName, simpleModuleName, StringComparison.OrdinalIgnoreCase) == 0)
                        moduleFiles[(int)loadedModule.ModuleFile.ModuleFileIndex] = loadedModule.ModuleFile;
                }
            }

            // We did not find it, try system-wide
            if (moduleFiles.Count == 0)
            {
                foreach (var moduleFile in TraceLog.ModuleFiles)
                {
                    var baseName = Path.GetFileNameWithoutExtension(moduleFile.Name);
                    if (string.Compare(baseName, simpleModuleName, StringComparison.OrdinalIgnoreCase) == 0)
                        moduleFiles[(int)moduleFile.ModuleFileIndex] = moduleFile;
                }
            }

            if (moduleFiles.Count == 0)
                throw new ApplicationException("Could not find module " + simpleModuleName + " in trace.");

            if (moduleFiles.Count > 1)
                log.WriteLine("Found {0} modules with name {1}", moduleFiles.Count, simpleModuleName);
            foreach (var moduleFile in moduleFiles.Values)
                TraceLog.CodeAddresses.LookupSymbolsForModule(symbolReader, moduleFile);
        }

        /// <summary>
        /// Access the underlying TraceLog class that actually represents the ETL data.   You use
        /// when the simplified wrappers are not sufficient.  
        /// </summary>
        public TraceLog TraceLog { get { return m_TraceLog; } }
        /// <summary>
        /// Returns a SymbolReader which knows to look in local places associated with this file as well
        /// as the user defined places. 
        /// </summary>
        public SymbolReader GetSymbolReader(SymbolReaderOptions symbolFlags = SymbolReaderOptions.None)
        {
            return App.GetSymbolReader(m_FilePath, symbolFlags);
        }

        /// <summary>
        /// Closes the file (so for example you could update it after this)
        /// </summary>
        public override void Dispose()
        {
            m_TraceLog.Dispose();
            m_TraceLog = null;
        }
        #region private

        private static void UnZipIfNecessary(ref string inputFileName, TextWriter log, bool unpackInCache = true)
        {
            if (string.Compare(Path.GetExtension(inputFileName), ".zip", StringComparison.OrdinalIgnoreCase) == 0)
            {
                string unzipedEtlFile;
                if (unpackInCache)
                {
                    unzipedEtlFile = CacheFiles.FindFile(inputFileName, ".etl");
                    if (File.Exists(unzipedEtlFile) && File.GetLastWriteTimeUtc(inputFileName) <= File.GetLastWriteTimeUtc(unzipedEtlFile))
                    {
                        log.WriteLine("Found a existing unzipped file {0}", unzipedEtlFile);
                        inputFileName = unzipedEtlFile;
                        return;
                    }
                }
                else
                {
                    if (!inputFileName.EndsWith(".etl.zip", StringComparison.OrdinalIgnoreCase))
                        throw new ApplicationException("File does not end with the .etl.zip file extension");
                    unzipedEtlFile = inputFileName.Substring(0, inputFileName.Length - 4);
                }

                Stopwatch sw = Stopwatch.StartNew();
                log.WriteLine("[Decompressing {0}]", inputFileName);
                log.WriteLine("Generating output file {0}", unzipedEtlFile);
                var zipArchive = ZipFile.OpenRead(inputFileName);

                ZipArchiveEntry zippedEtlFile = null;
                string dirForPdbs = null;
                foreach (var entry in zipArchive.Entries)
                {
                    if (entry.Length == 0)  // Skip directories. 
                        continue;

                    var fullName = entry.FullName;
                    if (fullName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                    {
                        fullName = fullName.Replace('/', '\\');     // normalize separator convention 
                        string pdbRelativePath = null;
                        if (fullName.StartsWith(@"symbols\", StringComparison.OrdinalIgnoreCase))
                            pdbRelativePath = fullName.Substring(8);
                        else if (fullName.StartsWith(@"ngenpdbs\", StringComparison.OrdinalIgnoreCase))
                            pdbRelativePath = fullName.Substring(9);
                        else
                        {
                            var m = Regex.Match(fullName, @"^[^\\]+\.ngenpdbs?\\(.*)", RegexOptions.IgnoreCase);
                            if (m.Success)
                                pdbRelativePath = m.Groups[1].Value;
                            else
                            {
                                log.WriteLine("WARNING: found PDB file that was not in a symbol server style directory, skipping extraction");
                                log.WriteLine("         Unzip this ETL and PDB by hand to use this PDB.");
                                continue;
                            }
                        }

                        if (dirForPdbs == null)
                        {
                            var inputDir = Path.GetDirectoryName(inputFileName);
                            if (inputDir.Length == 0)
                                inputDir = ".";
                            var symbolsDir = Path.Combine(inputDir, "symbols");
                            if (Directory.Exists(symbolsDir))
                                dirForPdbs = symbolsDir;
                            else
                                dirForPdbs = new SymbolPath(App.SymbolPath).DefaultSymbolCache();
                            log.WriteLine("Putting symbols in {0}", dirForPdbs);
                        }

                        var pdbTargetPath = Path.Combine(dirForPdbs, pdbRelativePath);
                        var pdbTargetName = Path.GetFileName(pdbTargetPath);
                        if (!File.Exists(pdbTargetPath) || (new System.IO.FileInfo(pdbTargetPath).Length != entry.Length))
                        {
                            var firstNameInRelativePath = pdbRelativePath;
                            var sepIdx = firstNameInRelativePath.IndexOf('\\');
                            if (sepIdx >= 0)
                                firstNameInRelativePath = firstNameInRelativePath.Substring(0, sepIdx);
                            var firstNamePath = Path.Combine(dirForPdbs, firstNameInRelativePath);
                            if (File.Exists(firstNamePath))
                            {
                                log.WriteLine("Deleting pdb file that is in the way {0}", firstNamePath);
                                FileUtilities.ForceDelete(firstNamePath);
                            }
                            log.WriteLine("Extracting PDB {0}", pdbRelativePath);
                            AtomicExtract(entry, pdbTargetPath);
                        }
                        else
                            log.WriteLine("PDB {0} exists, skipping", pdbRelativePath);
                    }
                    else if (fullName.EndsWith(".etl", StringComparison.OrdinalIgnoreCase))
                    {
                        if (zippedEtlFile != null)
                            throw new ApplicationException("The ZIP file does not have exactly 1 ETL file in it, can't auto-extract.");
                        zippedEtlFile = entry;
                    }
                }
                if (zippedEtlFile == null)
                    throw new ApplicationException("The ZIP file does not have any ETL files in it!");

                AtomicExtract(zippedEtlFile, unzipedEtlFile);
                log.WriteLine("Zipped size = {0:f3} MB Unzipped = {1:f3} MB",
                    zippedEtlFile.CompressedLength / 1000000.0, zippedEtlFile.Length / 1000000.0);

                File.SetLastWriteTime(unzipedEtlFile, DateTime.Now);       // Touch the file
                inputFileName = unzipedEtlFile;
                log.WriteLine("Finished decompression, took {0:f0} sec", sw.Elapsed.TotalSeconds);
            }
        }
        // Extract to a temp file and move so we get atomic update.  May leave trash behind
        private static void AtomicExtract(ZipArchiveEntry zipEntry, string targetPath)
        {
            // Insure directory exists. 
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            var extractPath = targetPath + ".new";
            try
            {
                zipEntry.ExtractToFile(extractPath, true);
                FileUtilities.ForceMove(extractPath, targetPath);
            }
            finally
            {
                FileUtilities.ForceDelete(extractPath);
            }
        }

        TraceLog m_TraceLog;
        TraceProcess m_FilterProcess;       // Only care about this process. 
        #endregion
    }

    public class Events : ETWEventSource
    {
        void SaveAsCSV(string csvFileName)
        {
            string listSeparator = Thread.CurrentThread.CurrentCulture.TextInfo.ListSeparator;
            using (var csvFile = File.CreateText(csvFileName))
            {
                // Write out column header
                csvFile.Write("Event Name{0}Time MSec{0}Process Name", listSeparator);
                // TODO get rid of ugly 4 column restriction
                var maxField = 0;
                var hasRest = true;
                if (ColumnsToDisplay != null)
                {
                    hasRest = false;
                    foreach (var columnName in ColumnsToDisplay)
                    {
                        Debug.Assert(!columnName.Contains(listSeparator));
                        if (maxField >= 4)
                        {
                            hasRest = true;
                            break;
                        }
                        maxField++;
                        csvFile.Write("{0}{1}", listSeparator, columnName);
                    }
                }
                if (hasRest)
                    csvFile.Write("{0}Rest", listSeparator);
                csvFile.WriteLine();

                // Write out events 
                this.ForEach(delegate (EventRecord _event)
                {
                    // Have we exceeded MaxRet?
                    if (_event.EventName == null)
                        return false;

                    csvFile.Write("{0}{1}{2:f3}{1}{3}", _event.EventName, listSeparator, _event.TimeStampRelatveMSec, EscapeForCsv(_event.ProcessName, listSeparator));
                    var fields = _event.DisplayFields;
                    for (int i = 0; i < maxField; i++)
                        csvFile.Write("{0}{1}", listSeparator, EscapeForCsv(fields[i], listSeparator));
                    if (hasRest)
                        csvFile.Write("{0}{1}", listSeparator, EscapeForCsv(_event.Rest, listSeparator));
                    csvFile.WriteLine();
                    return true;
                });
            }
        }
        void OpenInExcel()
        {
            var log = App.CommandProcessor.LogFile;

            var csvFile = CacheFiles.FindFile(m_EtlFile.FilePath, ".excel.csv");
            if (File.Exists(csvFile))
            {
                FileUtilities.TryDelete(csvFile);

                var baseFile = csvFile.Substring(0, csvFile.Length - 9);
                for (int i = 1; ; i++)
                {
                    csvFile = baseFile + i.ToString() + ".excel.csv";
                    if (!File.Exists(csvFile))
                        break;
                }
            }

            log.WriteLine("Saving to CSV file {0}", csvFile);
            SaveAsCSV(csvFile);
            log.WriteLine("Opening CSV .");
            Command.Run(Command.Quote(csvFile), new CommandOptions().AddStart().AddTimeout(CommandOptions.Infinite));
            log.WriteLine("CSV file opened.");
            throw new NotImplementedException();
        }

        public Events(ETLDataFile etlFile)
            : base(etlFile.TraceLog)
        {
            m_EtlFile = etlFile;
        }

        #region private
        /// <summary>
        /// Returns a string that is will be exactly one field of a CSV file.  Thus it escapes , and ""
        /// </summary>
        internal static string EscapeForCsv(string str, string listSeparator)
        {
            // TODO FIX NOW is this a hack?
            if (str == null)
                return "";
            // If you don't have a comma, you are OK (we are losing leading and trailing whitespace but I don't care about that. 
            if (str.IndexOf(listSeparator) < 0)
                return str;

            // Escape all " by repeating them
            str = str.Replace("\"", "\"\"");
            return "\"" + str + "\"";       // then quote the whole thing
        }

        internal ETLDataFile m_EtlFile;
        #endregion
    }

    /// <summary>
    /// A Stacks class represents what the PerfView 'stacks' view shows you.   It has 
    /// a 'FilterParams' property which represents all the filter strings in that view.  
    /// It also has properties that represent the various tabs in that view (calltee, byname ...)
    /// </summary>
    public class Stacks
    {
        public FilterParams Filter { get { return m_Filter; } set { m_Filter = value; m_StackSource = null; } }
        public void Update() { m_StackSource = null; }

        public CallTree CallTree
        {
            get
            {
                if (m_CallTree == null || m_StackSource == null)
                {
                    m_CallTree = new CallTree(ScalingPolicyKind.ScaleToData);
                    m_CallTree.StackSource = StackSource;
                }
                return m_CallTree;
            }
        }
        IEnumerable<CallTreeNodeBase> ByName
        {
            get
            {
                if (m_byName == null || m_CallTree == null || m_StackSource == null)
                    m_byName = CallTree.ByIDSortedExclusiveMetric();
                return m_byName;
            }
        }
        public CallTreeNodeBase FindNodeByName(string nodeNamePat)
        {
            var regEx = new Regex(nodeNamePat, RegexOptions.IgnoreCase);
            foreach (var node in ByName)
            {
                if (regEx.IsMatch(node.Name))
                    return node;
            }
            return CallTree.Root;
        }
        public CallTreeNode GetCallers(string focusNodeName)
        {
            var focusNode = FindNodeByName(focusNodeName);
            return AggregateCallTreeNode.CallerTree(focusNode);
        }
        public CallTreeNode GetCallees(string focusNodeName)
        {
            var focusNode = FindNodeByName(focusNodeName);
            return AggregateCallTreeNode.CalleeTree(focusNode);
        }

        /// <summary>
        /// Resolve the symbols of all modules that have at least 'minCount' INCLUSIVE samples.  
        /// symbolFlags indicate how aggressive you wish to be.   By default it is aggressive as possible (do 
        /// whatever you need to get the PDB you need).  
        /// Setting 'minCount' to 0 will try to look up all symbols possible (which is relatively expensive). 
        /// 
        /// By default all Warm symbols with a count > 50 AND in the machine local symbol cache are looked up.  
        /// If the cache is empty, or if you want even low count modules included, call this explicitly
        /// </summary>
        public void LookupWarmSymbols(int minCount, SymbolReaderOptions symbolFlags = SymbolReaderOptions.None)
        {
            TraceEventStackSource asTraceEventStackSource = GetTraceEventStackSource(m_rawStackSource);
            if (asTraceEventStackSource == null)
            {
                App.CommandProcessor.LogFile.WriteLine("LookupWarmSymbols: Stack source does not support symbols.");
                return;
            }
            string etlFilepath = null;
            if (m_EtlFile != null)
                etlFilepath = m_EtlFile.FilePath;

            var reader = App.GetSymbolReader(etlFilepath, symbolFlags);
            asTraceEventStackSource.LookupWarmSymbols(minCount, reader, StackSource);
            m_StackSource = null;
        }
        /// <summary>
        /// Lookup the symbols for a particular module (DLL). 
        /// </summary>
        /// <param name="simpleModuleName">The simple name (dll name without path or file extension)</param>
        /// <param name="symbolFlags">Optional flags that control symbol lookup</param>
        public void LookupSymbolsForModule(string simpleModuleName, SymbolReaderOptions symbolFlags = SymbolReaderOptions.None)
        {
            if (m_EtlFile != null)
            {
                m_EtlFile.LookupSymbolsForModule(simpleModuleName, symbolFlags);
                m_StackSource = null;
            }
            else
            {
                App.CommandProcessor.LogFile.WriteLine("LookupSymbolsForModule: Stack source does not support symbols.");
                return;
            }
        }

        /// <summary>
        /// Saves the stacks as a XML file (or a ZIPed XML file).  Only samples that pass the filter are saved.
        /// Also all interesting symbolic names should be resolved first because it is impossible to resolve them 
        /// later.   The saved samples CAN be regrouped later, however.  
        /// </summary>
        public void SaveAsXml(string outputFileName, bool zip = true, bool includeGuiState = true)
        {
            // TODO remember the status log even when we don't have a gui. 
            if (GuiApp.MainWindow != null)
                GuiState.Log = File.ReadAllText(App.LogFileName);

            Action<XmlWriter> additionalData = null;
            if (includeGuiState)
                additionalData = new Action<XmlWriter>((XmlWriter writer) => { GuiState.WriteToXml("StackWindowGuiState", writer); });

            // Intern to compact it, only take samples in the view but leave the names unmorphed. 
            InternStackSource source = new InternStackSource(StackSource, m_rawStackSource);
            if (zip)
                XmlStackSourceWriter.WriteStackViewAsZippedXml(source, outputFileName, additionalData);
            else
                XmlStackSourceWriter.WriteStackViewAsXml(source, outputFileName, additionalData);

            GuiState.Log = null;        // Save some space. 
        }
        /// <summary>
        /// This is an optional and is used for human interactions. 
        /// </summary>
        public string Name { get; internal set; }
        /// <summary>
        /// ExtraTopStats is a string that is pretty important that humans see when viewing these stacks.
        /// Often they are aggregate statistics, or maybe warnings or error summaries.   It should be 
        /// well under 100 chars long.  
        /// </summary>
        public string ExtraTopStats { get; set; }

        /// <summary>
        /// Stacks is just a convenience wrapper around the StackSource class.  For advanced use
        /// you may need to get at the stack source.   
        /// </summary>
        public StackSource StackSource
        {
            get
            {
                if (m_StackSource == null)
                    m_StackSource = new FilterStackSource(m_Filter, m_rawStackSource, ScalingPolicyKind.ScaleToData);
                return m_StackSource;
            }
        }
        /// <summary>
        /// If you have a stackSource and want a Stacks, 
        /// </summary>
        public Stacks(StackSource source, string name = "")
        {
            m_Filter = new FilterParams();
            m_rawStackSource = source;
            Name = name;
        }

        // GUI State
        /// <summary>
        /// There is a bunch of data that really is useful to persist to make 
        /// the GUI work well, (like the history of Filter selections etc) 
        /// but is not central to the model of the data.  This all goes into
        /// 'GuiState'.   This data gets persisted when SaveAsXml is called and
        /// is passed to the GUI when OpenStackViewer is called, but otherwise
        /// we don't do much with it.  (unless you wish influence the GUI
        /// by explicitly changing it).  
        /// </summary>
        public StackWindowGuiState GuiState
        {
            get
            {
                if (m_GuiState == null)
                    m_GuiState = DefaultCallStackWindowState("CPU");
                return m_GuiState;
            }
            set { m_GuiState = value; }
        }

        public bool HasGuiState { get { return m_GuiState != null; } }

        public static StackWindowGuiState DefaultCallStackWindowState(string name)
        {
            // TODO logic for getting out of ConfigSettings.  

            var ret = new StackWindowGuiState();
            ret.Columns = new List<string>() {
                    "NameColumn",
                    "ExcPercentColumn", "ExcColumn", "ExcCountColumn",
                    "IncPercentColumn", "IncColumn", "IncCountColumn",
                    "FoldColumn", "FoldCountColumn",
                    "WhenColumn", "FirstColumn", "LastColumn" };

            if (name == "Memory")
            {
                ret.FilterGuiState.FoldRegEx.Value = @"[];mscorlib!System.String";
            }
            else
            {
                ret.FilterGuiState.GroupRegEx.Value =
                    @"[group CLR/OS entries] \Temporary ASP.NET Files\->;v4.0.30319\%!=>CLR;v2.0.50727\%!=>CLR;mscoree=>CLR;\mscorlib.*!=>LIB;\System.*!=>LIB;" +
                    @"Presentation%=>WPF;WindowsBase%=>WPF;system32\*!=>OS;syswow64\*!=>OS;{%}!=> module $1";
                ret.FilterGuiState.GroupRegEx.History = new List<string> { ret.FilterGuiState.GroupRegEx.Value,
                     "[group modules]           {%}!->module $1",
                     "[group module entries]  {%}!=>module $1",
                     "[group full path module entries]  {*}!=>module $1",
                     "[group class entries]     {%!*}.%(=>class $1;{%!*}::=>class $1",
                     "[group classes]            {%!*}.%(->class $1;{%!*}::->class $1" };

                ret.FilterGuiState.ExcludeRegEx.Value = "^Process% Idle";
                ret.FilterGuiState.ExcludeRegEx.History = new List<string> { ret.FilterGuiState.ExcludeRegEx.Value };

                ret.FilterGuiState.FoldPercent.Value = "1";

                // Can allow users to tweek this with config data. 
                switch (name)
                {
                    case "CPU":
                        ret.Columns.Remove("IncCountColumn");
                        ret.Columns.Remove("ExcCountColumn");
                        ret.Columns.Remove("FoldCountColumn");
                        ret.ScalingPolicy = ScalingPolicyKind.TimeMetric;
                        break;
                }
            }
            return ret;
        }

        public override string ToString()
        {
            var sw = new System.IO.StringWriter();
            sw.Write("<Stacks");
            if (Name != null)
                sw.Write(" Name=\"{0}\"", Name);
            sw.WriteLine(">");
            sw.Write(" <RootStats ");
            CallTree.Root.ToXmlAttribs(sw);
            sw.Write("/>");
            sw.WriteLine("</Stacks>");
            return sw.ToString();
        }

        #region private
        /// <summary>
        /// TODO should not have to specify the ETL file. 
        /// </summary>
        public Stacks(StackSource source, string name, ETLDataFile etlFile, bool loadAllCachedSymbols = false)
        {
            m_Filter = new FilterParams();
            m_rawStackSource = source;
            m_EtlFile = etlFile;
            Name = string.Format("{0} file {1} in {2}", name, Path.GetFileName(etlFile.FilePath), Path.GetDirectoryName(etlFile.FilePath));

            // By default, look up all symbols in cache that have at least 50 samples.  
            LookupWarmSymbols((loadAllCachedSymbols ? 0 : 50), SymbolReaderOptions.CacheOnly);
        }
        protected Stacks() { }

        /// <summary>
        /// Unwind the wrapped sources to get to a TraceEventStackSource if possible. 
        /// </summary>
        static internal TraceEventStackSource GetTraceEventStackSource(StackSource source)
        {
            StackSourceStacks rawSource = source;
            TraceEventStackSource asTraceEventStackSource = null;
            for (;;)
            {
                asTraceEventStackSource = rawSource as TraceEventStackSource;
                if (asTraceEventStackSource != null)
                    return asTraceEventStackSource;

                var asCopyStackSource = rawSource as CopyStackSource;
                if (asCopyStackSource != null)
                {
                    rawSource = asCopyStackSource.SourceStacks;
                    continue;
                }
                var asStackSource = rawSource as StackSource;
                if (asStackSource != null && asStackSource != asStackSource.BaseStackSource)
                {
                    rawSource = asStackSource.BaseStackSource;
                    continue;
                }
                return null;
            }

        }

        protected StackSource m_rawStackSource;           // Before the filter is applied 
        private StackSource m_StackSource;              // After the filter is applied, note this changes every time the filter does. 
        private CallTree m_CallTree;
        private List<CallTreeNodeBase> m_byName;
        private FilterParams m_Filter;
        internal ETLDataFile m_EtlFile;                 // If this stack came from and ETL File this is that file.  
        internal string m_fileName;                     // TODO is this a hack.  This is the file name if present.  
        StackWindowGuiState m_GuiState;
        #endregion
    }

    #region internal classes
    internal static class Extensions
    {
        public static string ExtensionsDirectory
        {
            get
            {
                if (s_ExtensionsDirectory == null)
                {
                    var exeDir = Path.GetDirectoryName(SupportFiles.MainAssemblyPath);
                    // This is for development ease development of perfView itself.  
                    if (exeDir.EndsWith(@"\perfView\bin\Release", StringComparison.OrdinalIgnoreCase))
                        exeDir = exeDir.Substring(0, exeDir.Length - 20);
                    else if (exeDir.EndsWith(@"\perfView\bin\Debug", StringComparison.OrdinalIgnoreCase))
                        exeDir = exeDir.Substring(0, exeDir.Length - 18);

                    s_ExtensionsDirectory = Path.Combine(exeDir, "PerfViewExtensions");
                }
                return s_ExtensionsDirectory;
            }
        }

        public static void RunUserStartupCommands(StatusBar worker)
        {
            try
            {
                var startupFilePath = Path.Combine(ExtensionsDirectory, "PerfViewStartup");
                if (File.Exists(startupFilePath))
                {
                    string errorMessage = null;
                    string line = "";
                    int lineNum = 0;
                    using (var startupFile = File.OpenText(startupFilePath))
                    {
                        for (;;)
                        {
                            lineNum++;
                            line = startupFile.ReadLine();
                            if (line == null)
                                return;
                            line = line.Trim();
                            if (line.Length == 0 || line.StartsWith("#"))
                                continue;
                            List<string> commandAndArgs = MainWindow.ParseWordsOrQuotedStrings(line);
                            var command = "";
                            if (0 < commandAndArgs.Count)
                                command = commandAndArgs[0];
                            if (command == "OnStartup")
                            {
                                if (commandAndArgs.Count != 2)
                                {
                                    errorMessage = "OnStartup command requires 1 argument, the startup user command.";
                                    goto Failed;
                                }
                                ExecuteUserCommand(commandAndArgs[1], null);
                            }
                            else if (command == "OnFileOpen")
                            {
                                if (commandAndArgs.Count != 3)
                                {
                                    errorMessage = "OnFileOpen command requires 2 arguments, the file extension and the user command.";
                                    goto Failed;
                                }
                                string extension = commandAndArgs[1];
                                PerfViewFile.GetTemplateForExtension(extension).OnOpenFile(commandAndArgs[2]);
                            }
                            else if (command == "DeclareFileView")
                            {
                                if (commandAndArgs.Count != 4)
                                {
                                    errorMessage = "DeclareFileView command requires 3 arguments, the file extension, view name and the user command.";
                                    goto Failed;
                                }
                                string extension = commandAndArgs[1];
                                PerfViewFile.GetTemplateForExtension(extension).DeclareFileView(commandAndArgs[2], commandAndArgs[3]);
                            }
                            else
                            {
                                if (string.IsNullOrWhiteSpace(line))
                                    continue;
                                errorMessage = "Unrecognized command.";
                                goto Failed;
                            }
                        }
                        Failed:
                        throw new ApplicationException("Error: " + errorMessage + "  '" + line + @"' line " + lineNum + @" in PerfViewExtensions\PerfViewStartup file");
                    }
                }
            }
            catch (Exception e)
            {
                worker.LogError(@"Error executing PerfViewExtensions\PerfViewStartup file.\r\n" + e.Message);
            }
        }

        public static IEnumerable<string> GetExtensionDlls()
        {
            if (Directory.Exists(ExtensionsDirectory))
            {
                foreach (var dll in Directory.GetFiles(ExtensionsDirectory, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    using (var peFile = new PEFile.PEFile(dll))
                    {
                        if (!peFile.Header.IsManaged || peFile.Header.IsPE64)
                            continue;
                    }
                    yield return dll;
                }
            }
        }

        public static void GenerateHelp(TextWriter log)
        {
            var commandsHelp = GetUserCommandHelp();
            foreach (var method in Extensions.GetAllUserCommands())
            {
                var commandSummary = Extensions.GetCommandString(method);
                log.WriteLine();
                log.WriteLine("-------------------------------------------------------------------------");
                log.WriteLine("{0}", commandSummary);

                // Get command name
                var idx = commandSummary.IndexOf(' ');
                if (idx < 0) idx = commandSummary.Length;
                var commandName = commandSummary.Substring(0, idx);

                // Print extra help
                CommandHelp commandHelp;
                if (commandsHelp.TryGetValue(commandName, out commandHelp))
                {
                    log.WriteLine();
                    log.WriteLine("  Summary: ");
                    WriteWrapped("      ", commandHelp.Summary, "      ", 80, log);
                    if (commandHelp.Params != null)
                    {
                        log.WriteLine("  Parameters: ");
                        foreach (var param in commandHelp.Params)
                            WriteWrapped("      " + param.Name + ": ", param.Help, "          ", 80, log);
                    }
                }
            }
        }

        private static void WriteWrapped(string prefix, string body, string wrap, int maxColumn, TextWriter log)
        {
            body = Regex.Replace(body, @"\s+", " ");
            log.Write(prefix);
            int curColumn = prefix.Length;
            int idx = 0;
            do
            {
                var nextBreak = GetLineNextBreak(body, idx, curColumn, maxColumn);

                log.WriteLine(body.Substring(idx, nextBreak - idx));
                if (body.Length <= nextBreak)
                    break;

                idx = nextBreak + 1;
                log.Write(wrap);
                curColumn = wrap.Length;
            }
            while (idx < body.Length);
        }

        /// <summary>
        /// Find the end index of the chunk of 'str' if we are at 'curColumn' and we don't want the text
        /// to go beyond maxColumn.    
        /// </summary>
        private static int GetLineNextBreak(string str, int startIdx, int startColumn, int maxColumn)
        {
            var curPos = startIdx + (maxColumn - startColumn);
            if (curPos >= str.Length)
                return str.Length;

            var spaceIdx = str.LastIndexOf(' ', curPos, curPos - startIdx);
            if (0 <= spaceIdx)
                return spaceIdx;

            spaceIdx = str.IndexOf(' ', curPos);
            if (0 <= spaceIdx)
                return spaceIdx;

            return str.Length;
        }

        public static List<MethodInfo> GetAllUserCommands()
        {
            var ret = new List<MethodInfo>();

            // Get the ones built into perfView itself (in Extensibilty.cs PerfViewExtensibility\Commands)
            var methods = typeof(PerfViewExtensibility.Commands).GetMethods(
                BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
                ret.Add(method);

            // Find all the ones that are in user extension dlls.  
            foreach (var extensionDllPath in GetExtensionDlls())
            {
                try
                {
                    var assembly = Assembly.LoadFrom(extensionDllPath);
                    var commandType = assembly.GetType("Commands");
                    if (commandType != null)
                    {
                        methods = commandType.GetMethods(
                            BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        foreach (var method in methods)
                            ret.Add(method);
                    }
                }
                catch (Exception)
                {
                    App.CommandProcessor.LogFile.WriteLine("Dll " + extensionDllPath + "could not be loaded, assuming it has no user commands");
                }
            }

            return ret;
        }

        /// <summary>
        /// Returns strings of the from COMMANDNAME arg1Name [arg2Name] .... 
        /// where [] indicate optional arguments.  
        /// </summary>
        /// <returns></returns>
        public static string GetCommandString(MethodInfo method)
        {
            var sb = new StringBuilder();

            var assembly = method.DeclaringType.Assembly;
            var assemblyName = Path.GetFileNameWithoutExtension(assembly.ManifestModule.FullyQualifiedName);
            if (string.Compare(assemblyName, "Global", StringComparison.OrdinalIgnoreCase) != 0 && assembly != Assembly.GetExecutingAssembly())
                sb.Append(assemblyName).Append('.');

            sb.Append(method.Name);
            foreach (var param in method.GetParameters())
            {
                sb.Append(' ');
                var defaultValue = param.RawDefaultValue;
                if (defaultValue != System.DBNull.Value)
                    sb.Append('[').Append(param.Name).Append(']');
                else
                {
                    var attribs = param.GetCustomAttributes(typeof(ParamArrayAttribute), false);
                    if (attribs.Length != 0)
                        sb.Append('[').Append(param.Name).Append("...]");
                    else
                        sb.Append(param.Name);
                }
            }
            var ret = sb.ToString();
            return ret;
        }

        /// <summary>
        /// Return all the user command help, indexed by user command name.  
        /// </summary>
        public static Dictionary<string, CommandHelp> GetUserCommandHelp()
        {
            var ret = new Dictionary<string, CommandHelp>();
            foreach (var extDll in GetExtensionDlls())
            {
                var extName = Path.GetFileNameWithoutExtension(extDll);
                var xmlHelp = Path.ChangeExtension(extDll, ".xml");
                AddUserCommandHelp(ret, extName, xmlHelp);
            }
            // Add the help for the ones in Extensions.cs in perfView itself.  
            AddUserCommandHelp(ret, "", Path.Combine(SupportFiles.SupportFileDir, "PerfView.xml"));
            return ret;
        }

        /// <summary>
        /// Given the name of the extension DLL (extName) and the xml help file for the extension xmlHelp
        /// Find the XML comments for all commands and add them to the 'userComandHelp' dictionary.  
        /// </summary>
        private static void AddUserCommandHelp(Dictionary<string, CommandHelp> userComandHelp, string extName, string xmlHelp)
        {
            // TODO we actually end up with too large of dictionary because we also store the private methods too.  
            try
            {
                if (File.Exists(xmlHelp))
                {
                    XmlReaderSettings settings = new XmlReaderSettings() { IgnoreWhitespace = true, IgnoreComments = true };
                    XmlReader reader = XmlReader.Create(xmlHelp, settings);
                    reader.ReadToDescendant("members");
                    var startDepth = reader.Depth;
                    reader.Read();      // Advance to children 
                    while (startDepth < reader.Depth)
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            if (reader.Name == "member")
                            {
                                var xmlMemberName = reader.GetAttribute("name");
                                if (xmlMemberName.StartsWith("M:Commands."))
                                {
                                    var endIdx = xmlMemberName.IndexOf('(');
                                    if (endIdx < 0)
                                        endIdx = xmlMemberName.Length;
                                    var name = xmlMemberName.Substring(11, endIdx - 11);
                                    if (extName != "Global")
                                        name = extName + "." + name;
                                    userComandHelp.Add(name, new CommandHelp(name, reader));
                                }
                                else if (extName.Length == 0 && xmlMemberName.StartsWith("M:PerfViewExtensibility.Commands."))
                                {
                                    // Handle the case for user commands defined in PerfView.exe itself.  
                                    var endIdx = xmlMemberName.IndexOf('(');
                                    if (endIdx < 0)
                                        endIdx = xmlMemberName.Length;
                                    var name = xmlMemberName.Substring(33, endIdx - 33);
                                    userComandHelp[name] = new CommandHelp(name, reader);
                                }
                                else
                                    reader.Skip();
                            }
                            else
                                reader.Skip();
                        }
                        else if (!reader.Read())
                            break;
                    }

                }
            }
            catch (Exception e)
            {
                // TODO add logging that gets to the user.  
                Debug.WriteLine("error reading XML file " + e.Message);
            }
        }

        /// <summary>
        /// Represents the help for a method. 
        /// </summary>
        public class CommandHelp
        {
            public string Name;
            public string Summary;
            public List<CommandHelpParam> Params;
            private string name;

            public CommandHelp(string name, XmlReader reader)
            {
                this.name = name;

                var startDepth = reader.Depth;
                reader.Read();      // Advance to children 
                while (startDepth < reader.Depth)
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "summary")
                            this.Summary = reader.ReadElementContentAsString().Trim();
                        else if (reader.Name == "param")
                        {
                            if (Params == null)
                                Params = new List<CommandHelpParam>();
                            var newParam = new CommandHelpParam();
                            newParam.Name = reader.GetAttribute("name");
                            newParam.Help = reader.ReadElementContentAsString().Trim();
                            Params.Add(newParam);
                        }
                        else
                            reader.Skip();
                    }
                    else if (!reader.Read())
                        break;
                }
            }
        }
        /// <summary>
        /// Represents the help for a single parameter
        /// </summary>
        public class CommandHelpParam
        {
            public string Name;
            public string Help;
        }


        #region private
        private static string s_ExtensionsDirectory;

        private static Dictionary<string, object> LoadedObjects;

        internal static void ExecuteUserCommand(string command, string[] args)
        {
            object instance = null;     // The object instance to invoke
            Type instanceType = null;

            // Parse command into fileSpec and methodSpec
            var fileSpec = "Global";
            var methodSpec = command;
            var dotIndex = command.IndexOf('.');
            if (0 < dotIndex)
            {
                fileSpec = command.Substring(0, dotIndex);
                methodSpec = command.Substring(dotIndex + 1);
            }
            else
            {
                // It is a global command, first try look in PerfView itself 
                instanceType = typeof(Commands);
                if (instanceType.GetMethod(methodSpec) != null)
                    instance = new Commands();
            }

            // Could not find it in perfView, look in extensions.  
            if (instance == null)
            {
                // Find the instance of 'Commands' that we may have created previously, otherwise make a new one
                if (LoadedObjects == null)
                    LoadedObjects = new Dictionary<string, object>();
                if (!LoadedObjects.TryGetValue(fileSpec, out instance))
                {
                    var fullFilePath = Path.Combine(ExtensionsDirectory, fileSpec + ".dll");
                    if (!File.Exists(fullFilePath))
                    {
                        if (fileSpec == "Global")
                            throw new FileNotFoundException("Could not find " + methodSpec + " in PerfView's built in user commands.");

                        throw new FileNotFoundException("Could not find file " + fullFilePath + " for for user extensions.", fullFilePath);
                    }
                    var assembly = Assembly.LoadFrom(fullFilePath);

                    instanceType = assembly.GetType("Commands");
                    if (instanceType == null)
                        throw new ApplicationException("Could not find type 'Commands' in " + fullFilePath);
                    instance = Activator.CreateInstance(instanceType);
                    LoadedObjects[fileSpec] = instance;
                }
                else
                    instanceType = instance.GetType();
            }

            // Actually invoke the method.  
            try
            {
                instanceType.InvokeMember(methodSpec,
                   BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.Public | BindingFlags.OptionalParamBinding,
                   null, instance, args);
            }
            catch (TargetInvocationException ex)
            {
                // TODO we don't get the stack for the inner exception. 
                // When we move to V4.5.1 we can use ExceptionDispatchInfo to fix.  
                throw ex.InnerException;
            }
            catch (AggregateException ex)
            {
                throw ex.InnerExceptions[0];
            }
            catch (MissingMethodException)
            {
                throw new ApplicationException(string.Format(
                    "Could not find user command {0} that takes {1} arguments.  Use /userCommandHelp for help.", methodSpec, args == null ? 0 : args.Length));
            }
        }
        #endregion
    }

#if false
// TODO FIX NOW use or remove 
//
// What is the right model?
// 
// StackSource - represents the raw data.   No dependencies, Can do filtering. - Clean for Model
// CallTree - Depends on StackSource, model for treeview,  - Clean for Model
// AgreegateCallTree - callers view and callees view - Clean for Model
// EventSource - eventView - Clean for Model.  
// 
// MutableTraceEventStackSource - Sources for ETL file - Clean for Model 
// 
// PerfViewItem
//     Filename, ICON, help, Expanded, Children, Open 
// PerfViewFile
// PerfViewStackSource - know their view
//     At the very least they are a model for the MainViewer's GUI.  
//     They open 
// PerfViewEventSource - know their view
// PerfViewHtmlReport
// 
// These things know about StatusBars, they know their view.   
//
// Does the automation drive the GUI or does it drive the MODEL?  
static class GuiModel
{
    public static void Wait(this StatusBar worker)
    {
        while (worker.IsWorking)
            Thread.Sleep(100);
    }

    public static PerfViewFile Open(string fileName)
    {
        var ret = PerfViewFile.Get(fileName);

        var mainWindow = GuiApp.MainWindow;
        ret.Open(mainWindow, mainWindow.StatusBar);
        mainWindow.StatusBar.Wait();
        return ret;
    }

    public static void ResolveSymbols(this PerfViewStackSource source)
    {
        var viewer = source.Viewer;
        viewer.DoLookupWarmSymbols(null, null);
        viewer.StatusBar.Wait();
    }

    public static void SetFilter(this PerfViewStackSource source, FilterParams filter)
    {
        var viewer = source.Viewer;
        viewer.Filter = filter;
        viewer.Update();
        viewer.StatusBar.Wait();
    }

    public static CallTree CallTree(this PerfViewStackSource source)
    {
        return source.Viewer.CallTree;
    }

    public static void Save(this PerfViewStackSource source, string fileName)
    {
        var viewer = source.Viewer;
        viewer.FileName = fileName;
        viewer.DoSave(null, null);
        viewer.StatusBar.Wait();
    }
}
#endif
    #endregion
}

// PerfViewModel contains things that are not very important for the user to see 
// but are never the less part of the model.  
namespace PerfViewModel
{
    /// <summary>
    /// This class effectively serializes GUI state in the StackWindow.  It is 
    /// simply a strongly typed version of the XML data.  
    /// </summary>
    public class StackWindowGuiState
    {
        public StackWindowGuiState() { FilterGuiState = new FilterGuiState(); }
        public StackWindowGuiState ReadFromXml(XmlReader reader)
        {
            if (reader.NodeType != XmlNodeType.Element)
                throw new InvalidOperationException("Must advance to XML element (e.g. call ReadToDescendant)");

            var inputDepth = reader.Depth;
            // This is here for backward compatibility.  Can remove after 2013.  
            TabSelected = reader.GetAttribute("TabSelected");

            reader.Read();      // Advance to children 
            while (inputDepth < reader.Depth)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    string valueStr;
                    switch (reader.Name)
                    {
                        case "FilterGuiState":
                            FilterGuiState.ReadFromXml(reader);
                            break;
                        case "Notes":
                            Notes = reader.ReadElementContentAsString().Trim();
                            break;
                        case "Log":
                            Log = reader.ReadElementContentAsString().Trim();
                            break;
                        case "Columns":
                            Columns = TextBoxGuiState.ReadStringList(reader);
                            break;
                        case "NotesPaneHidden":
                            valueStr = reader.ReadElementContentAsString().Trim();
                            NotesPaneHidden = string.Compare(valueStr, "True", StringComparison.OrdinalIgnoreCase) == 0;
                            break;
                        case "ScalingPolicy":
                            valueStr = reader.ReadElementContentAsString().Trim();
                            if (string.Compare(valueStr, "TimeMetric", StringComparison.OrdinalIgnoreCase) == 0)
                                ScalingPolicy = ScalingPolicyKind.TimeMetric;
                            else
                                Debug.Assert(string.Compare(valueStr, "ScaleToData", StringComparison.OrdinalIgnoreCase) == 0);
                            break;
                        case "TabSelected":
                            TabSelected = reader.ReadElementContentAsString().Trim();
                            break;
                        case "FocusName":
                            FocusName = reader.ReadElementContentAsString().Trim();
                            break;
                        //case "ByNameSelection":
                        //    ByNameSelection = reader.ReadElementContentAsString().Trim();
                        //    break;
                        //case "CallTreeSelection":
                        //    CallTreeSelection = reader.ReadElementContentAsString().Trim();
                        //    break;
                        //case "CalleesSelection":
                        //    CalleesSelection = reader.ReadElementContentAsString().Trim();
                        //    break;
                        //case "CallersSelection":
                        //    CallersSelection = reader.ReadElementContentAsString().Trim();
                        //    break;
                        // This is here for backward compatibility.  Can remove after 2013.  
                        case "CallerCallee":
                            FocusName = reader.GetAttribute("Focus");
                            reader.Skip();
                            break;
                        default:
                            Debug.WriteLine("Skipping unknown element {0}", reader.Name);
                            reader.Skip();
                            break;
                    }
                }
                else if (!reader.Read())
                    break;
            }
            return this;
        }
        public void WriteToXml(string name, XmlWriter writer)
        {
            writer.WriteStartElement(name);
            FilterGuiState.WriteToXml("FilterGuiState", writer);

            writer.WriteElementString("Notes", Notes);
            writer.WriteElementString("Log", XmlUtilities.XmlEscape(Log));
            if (Columns != null)
            {
                writer.WriteStartElement("Columns");
                foreach (var columnName in Columns)
                    writer.WriteElementString("string", columnName);
                writer.WriteEndElement();
            }
            writer.WriteElementString("NotesPaneHidden", NotesPaneHidden.ToString());
            writer.WriteElementString("ScalingPolicy", ScalingPolicy.ToString());
            writer.WriteElementString("TabSelected", TabSelected);
            writer.WriteElementString("FocusName", FocusName);

            //writer.WriteElementString("ByNameSelection", ByNameSelection);
            //writer.WriteElementString("CallTreeSelection", CallTreeSelection);
            //writer.WriteElementString("CalleesSelection", CalleesSelection);
            //writer.WriteElementString("CallersSelection", CallersSelection);

            writer.WriteEndElement();
        }

        // Filter 
        public FilterGuiState FilterGuiState;
        public string Notes;
        public string Log;

        // Global attributes.  
        public List<string> Columns;
        public bool NotesPaneHidden;
        public ScalingPolicyKind ScalingPolicy;

        // What tab is selected
        public string TabSelected;
        public string FocusName;

        // Where the cursor is in each tab 
        //public string ByNameSelection;
        //public string CallTreeSelection;
        //public string CallersSelection;
        //public string CalleesSelection;
    }

    /// <summary>
    /// This class effectively serializes the GUI state of the filter parameters.  It is 
    /// simply a strongly typed version of the XML data.  
    /// </summary>
    public class FilterGuiState
    {
        public FilterGuiState()
        {
            Start = new TextBoxGuiState();
            End = new TextBoxGuiState();
            Scenarios = new TextBoxGuiState();
            GroupRegEx = new TextBoxGuiState();
            FoldPercent = new TextBoxGuiState();
            FoldRegEx = new TextBoxGuiState();
            IncludeRegEx = new TextBoxGuiState();
            ExcludeRegEx = new TextBoxGuiState();
            TypePriority = new TextBoxGuiState();
        }
        public FilterGuiState ReadFromXml(XmlReader reader)
        {
            if (reader.NodeType != XmlNodeType.Element)
                throw new InvalidOperationException("Must advance to XML element (e.g. call ReadToDescendant)");
            var inputDepth = reader.Depth;
            reader.Read();      // Advance to children 
            while (inputDepth < reader.Depth)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "Start":
                            Start.ReadFromXml(reader);
                            break;
                        case "End":
                            End.ReadFromXml(reader);
                            break;
                        case "GroupRegEx":
                            GroupRegEx.ReadFromXml(reader);
                            break;
                        case "FoldPercent":
                            FoldPercent.ReadFromXml(reader);
                            break;
                        case "FoldRegEx":
                            FoldRegEx.ReadFromXml(reader);
                            break;
                        case "IncludeRegEx":
                            IncludeRegEx.ReadFromXml(reader);
                            break;
                        case "ExcludeRegEx":
                            ExcludeRegEx.ReadFromXml(reader);
                            break;
                        case "TypePriority":
                            TypePriority.ReadFromXml(reader);
                            break;
                        case "Scenarios":
                            Scenarios.ReadFromXml(reader);
                            break;
                        default:
                            Debug.WriteLine("Skipping unknown element {0}", reader.Name);
                            reader.Skip();
                            break;
                    }
                }
                else if (!reader.Read())
                    break;
            }
            return this;
        }
        public void WriteToXml(string name, XmlWriter writer)
        {
            writer.WriteStartElement(name);
            Start.WriteToXml("Start", writer);
            End.WriteToXml("End", writer);
            Scenarios.WriteToXml("Scenarios", writer);
            GroupRegEx.WriteToXml("GroupRegEx", writer);
            FoldPercent.WriteToXml("FoldPercent", writer);
            FoldRegEx.WriteToXml("FoldRegEx", writer);
            IncludeRegEx.WriteToXml("IncludeRegEx", writer);
            ExcludeRegEx.WriteToXml("ExcludeRegEx", writer);
            ExcludeRegEx.WriteToXml("TypePriority", writer);
            writer.WriteEndElement();
        }

        public TextBoxGuiState Start;
        public TextBoxGuiState End;
        public TextBoxGuiState GroupRegEx;
        public TextBoxGuiState FoldPercent;
        public TextBoxGuiState FoldRegEx;
        public TextBoxGuiState IncludeRegEx;
        public TextBoxGuiState ExcludeRegEx;
        public TextBoxGuiState TypePriority;
        public TextBoxGuiState Scenarios;
    }

    /// <summary>
    /// This class effectively serializes the GUI state a single historyTextBox.  It is 
    /// simply a strongly typed version of the XML data.  
    /// 
    /// 
    /// </summary>
    public class TextBoxGuiState
    {
        public TextBoxGuiState() { }
        /// <summary>
        /// Assumes we are ON the 'MyName' node below.  Readers in the values.  
        /// Leaves the reader on the EndElement.   
        ///    <MyName>
        ///      <Value>myValue</Value>
        ///      <History>
        ///        <string>old</string>
        ///      </History>
        ///    </MyName>
        /// </summary>
        public TextBoxGuiState ReadFromXml(XmlReader reader)
        {
            if (reader.NodeType != XmlNodeType.Element)
                throw new InvalidOperationException("Must advance to XML element (e.g. call ReadToDescendant)");
            var inputDepth = reader.Depth;
            reader.Read();      // Advance to children 
            while (inputDepth < reader.Depth)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "Value")
                        Value = reader.ReadElementContentAsString().Trim();
                    else if (reader.Name == "History")
                        History = ReadStringList(reader);
                    else
                        reader.Skip();
                }
                // This is here for compatibilty
                else if (reader.NodeType == XmlNodeType.Text || Value == null)
                {
                    Value = reader.ReadString().Trim();
                }
                else if (!reader.Read())
                    break;
            }
            return this;
        }
        public void WriteToXml(string name, XmlWriter writer)
        {
            writer.WriteStartElement(name);
            if (Value != null)
                writer.WriteElementString("Value", Value);
            if (History != null)
            {
                writer.WriteStartElement("History");
                foreach (var str in History)
                    writer.WriteElementString("string", str);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        public string Value;
        public List<string> History;

        // TODO does not really belong here, it is generic code. 
        /// <summary>
        /// Reads a string list in XMLSerialization format.   Assumes we are on MyList element
        /// to start, and ends having read the end element of MyList.  
        ///      <MyList>
        ///        <string>elem1</string>
        ///        <string>elem2</string>
        ///        <string>elem3</string>
        ///      </MyList>
        /// </summary>
        internal static List<string> ReadStringList(XmlReader reader)
        {
            var ret = new List<string>();
            if (reader.NodeType != XmlNodeType.Element)
                throw new InvalidOperationException("Must advance to XML element (e.g. call ReadToDescendant)");
            var inputDepth = reader.Depth;
            reader.Read();      // Advance to children 
            while (inputDepth < reader.Depth)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    // HistoryItem and Column is there for compatibility.  Can be removed after 2013
                    if (reader.Name == "string" || reader.Name == "HistoryItem")
                        ret.Add(reader.ReadElementContentAsString().Trim());
                    else if (reader.Name == "Column")
                    {
                        ret.Add(reader.GetAttribute("Name").Trim());
                        reader.Skip();
                    }
                    else
                        reader.Skip();
                }
                else if (!reader.Read())
                    break;
            }
            return ret;
        }
    }
}

// This is an example use of the extensibility features.  
namespace PerfViewExtensibility
{
    /// <summary>
    /// Commands is an actual use of the extensibility functionality.   Normally a 'Commands'
    /// class is compiled into a user defined DLL.
    /// </summary>
    public class Commands : CommandEnvironment
    {
        /// <summary>
        /// Dump every event in 'etlFileName' (which can be a ETL file or an ETL.ZIP file), as an XML file 'xmlOutputFileName'
        /// If the output file name is not given, the input filename's extension is changed to '.etl.xml' and that is used. 
        /// 
        /// This command is particularly useful for EventSources, where you want to post-process the data in some other tool.  
        /// </summary>
        public void DumpEventsAsXml(string etlFileName, string xmlOutputFileName = null)
        {
            if (xmlOutputFileName == null)
                xmlOutputFileName = PerfViewFile.ChangeExtension(etlFileName, ".etl.xml");

            var eventCount = 0;
            using (var outputFile = File.CreateText(xmlOutputFileName))
            {
                using (var etlFile = OpenETLFile(etlFileName))
                {
                    var events = GetTraceEventsWithProcessFilter(etlFile);
                    var sb = new StringBuilder();

                    outputFile.WriteLine("<Events>");
                    foreach (TraceEvent _event in events)
                    {
                        sb.Clear();
                        _event.ToXml(sb);
                        outputFile.WriteLine(sb.ToString());
                        eventCount++;
                    }
                    outputFile.WriteLine("</Events>");
                }
            }
            LogFile.WriteLine("[Wrote {0} events to {1}]", eventCount, xmlOutputFileName);
        }

        /// <summary>
        /// Save the CPU stacks from 'etlFileName'.  If the /process qualifier is present use it to narrow what
        /// is put into the file to a single process.  
        /// </summary>
        public void SaveCPUStacks(string etlFileName, string processName = null)
        {
            using (var etlFile = OpenETLFile(etlFileName))
            {
                TraceProcess process = null;
                if (processName != null)
                {
                    process = etlFile.Processes.LastProcessWithName(processName);
                    if (process == null)
                        throw new ApplicationException("Could not find process named " + processName);
                }
                SaveCPUStacksForProcess(etlFile, process);
            }
        }

        /// <summary>
        /// Save the CPU stacks for a set of traces.
        /// 
        /// If 'scenario' is an XML file, it will be used as a configuration file.
        /// 
        /// Otherwise, 'scenario' must refer to a directory. All ETL files in that directory and
        /// any subdirectories will be processed according to the default rules.
        /// 
        /// Summary of config XML:      ([] used instead of brackets)
        ///     [ScenarioConfig]
        ///         [Scenarios files="*.etl" process="$1.exe" name="scenario $1" /]
        ///     [/ScenarioConfig]
        /// </summary>
        public void SaveScenarioCPUStacks(string scenario)
        {
            var startTime = DateTime.Now;
            int skipped = 0, updated = 0;

            Dictionary<string, ScenarioConfig> configs;
            var outputBuilder = new StringBuilder();
            string outputName = null;
            DateTime scenarioUpdateTime = DateTime.MinValue;
            var writerSettings = new XmlWriterSettings()
            {
                Indent = true,
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = true
            };

            using (var outputWriter = XmlWriter.Create(outputBuilder, writerSettings))
            {
                if (scenario.EndsWith(".xml"))
                {
                    using (var reader = XmlReader.Create(scenario))
                    {
                        configs = DeserializeScenarioConfig(reader, outputWriter, LogFile, Path.GetDirectoryName(scenario));
                    }
                    outputName = Path.ChangeExtension(scenario, ".scenarioSet.xml");
                    scenarioUpdateTime = File.GetLastWriteTimeUtc(scenario);
                }
                else
                {
                    configs = new Dictionary<string, ScenarioConfig>();
                    var dirent = new DirectoryInfo(scenario);
                    foreach (var etl in dirent.EnumerateFiles("*.etl").Concat(dirent.EnumerateFiles("*.etl.zip")))
                    {
                        configs[PerfViewFile.ChangeExtension(etl.FullName, ".perfView.xml.zip")] = new ScenarioConfig(etl.FullName);
                    }

                    // Write default ScenarioSet.
                    outputWriter.WriteStartDocument();
                    outputWriter.WriteStartElement("ScenarioSet");
                    outputWriter.WriteStartElement("Scenarios");
                    outputWriter.WriteAttributeString("files", "*.perfView.xml.zip");
                    outputWriter.WriteEndElement();
                    outputWriter.WriteEndElement();

                    outputName = Path.Combine(scenario, "Default.scenarioSet.xml");
                }
            }

            if (configs.Count == 0)
            {
                throw new ApplicationException("No ETL files specified");
            }

            foreach (var configPair in configs)
            {
                var destFile = configPair.Key;
                var config = configPair.Value;
                var filename = config.InputFile;

                // Update if we've been written to since updateTime (max of file and scenario config write time).
                var updateTime = File.GetLastWriteTimeUtc(filename);
                if (scenarioUpdateTime > updateTime)
                    updateTime = scenarioUpdateTime;

                if (File.Exists(destFile) &&
                    File.GetLastWriteTimeUtc(destFile) >= scenarioUpdateTime)
                {
                    LogFile.WriteLine("[Skipping file {0}: up to date]", filename);
                    skipped++;
                    continue;
                }

                var etl = OpenETLFile(filename);
                TraceProcess processOfInterest;

                bool wildCard = false;
                if (config.ProcessFilter == null)
                {
                    processOfInterest = FindProcessOfInterest(etl);
                }
                else if (config.ProcessFilter == "*")
                {
                    processOfInterest = null;
                    wildCard = true;
                }
                else
                {
                    processOfInterest = null;
                    foreach (var process in etl.Processes)
                    {
                        if (config.StartTime <= process.StartTimeRelativeMsec &&
                            string.Compare(process.Name, config.ProcessFilter, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            processOfInterest = process;
                            break;
                        }
                    }
                }

                if (processOfInterest == null & !wildCard)
                    throw new ApplicationException("Process of interest could not be located for " + filename);


                FilterParams filter = new FilterParams();
                filter.StartTimeRelativeMSec = config.StartTime.ToString("R");
                filter.EndTimeRelativeMSec = config.EndTime.ToString("R");
                SaveCPUStacksForProcess(etl, processOfInterest, filter, destFile);
                LogFile.WriteLine("[File {0} updated]", filename);
                updated++;
            }

            // Regenerate scenario set if out-of-date.
            if (!scenario.EndsWith(".xml") || !File.Exists(outputName) ||
                File.GetLastWriteTimeUtc(outputName) < File.GetLastWriteTimeUtc(scenario))
            {
                LogFile.WriteLine("[Writing ScenarioSet file {0}]", outputName);
                File.WriteAllText(outputName, outputBuilder.ToString(), Encoding.UTF8);
            }
            var endTime = DateTime.Now;

            LogFile.WriteLine("[Scenario {3}: {0} generated, {1} up-to-date [{2:F3} s]]",
                updated, skipped, (endTime - startTime).TotalSeconds,
                Path.GetFileName(PerfViewFile.ChangeExtension(outputName, "")));
        }

        /// <summary>
        /// If there are System.Diagnostics.Tracing.EventSources that are logging data to the ETL file
        /// then there are manifests for each of these EventSources in event stream.  This method 
        /// dumps these to 'outputDirectory' (each manifest file is 'ProviderName'.manifest.xml)
        /// 
        /// If outputDirectory is not present, then the directory 'EtwManifests' in the same directory
        /// as the 'etlFileName' is used as the output directory.  
        /// If 'pattern' is present this is a .NET regular expression and only EventSources that match 
        /// the pattern will be output. 
        /// </summary>
        public void DumpEventSourceManifests(string etlFileName, string outputDirectory = null, string pattern = null)
        {
            if (outputDirectory == null)
                outputDirectory = Path.Combine(Path.GetDirectoryName(etlFileName), "ETWManifests");

            var etlFile = OpenETLFile(etlFileName);
            Directory.CreateDirectory(outputDirectory);
            int manifestCount = 0;
            foreach (var parser in etlFile.TraceLog.Parsers)
            {
                var asDynamic = parser as DynamicTraceEventParser;
                if (asDynamic != null)
                {
                    foreach (var provider in asDynamic.DynamicProviders)
                    {
                        if (pattern == null || Regex.IsMatch(provider.Name, pattern))
                        {
                            var filePath = Path.Combine(outputDirectory, provider.Name + ".manifest.xml");
                            LogFile.WriteLine("Creating manifest file {0}", filePath);
                            File.WriteAllText(filePath, provider.Manifest);
                            manifestCount++;
                        }
                    }
                }
            }
            LogFile.WriteLine("[Created {0} manifest files in {1}]", manifestCount, outputDirectory);
        }

        /// <summary>
        /// This is a test hook.  
        /// </summary>
        public void DumpJSHeapAsEtlFile(string processID)
        {
            JavaScriptHeapDumper.DumpAsEtlFile(int.Parse(processID), processID + ".etl", LogFile);
        }

        /// <summary>
        /// Generate a GCDumpFile of a JavaScript heap from ETW data in 'etlFileName'
        /// </summary>
        public void JSGCDumpFromETLFile(string etlFileName, string gcDumpOutputFileName = null)
        {
            if (gcDumpOutputFileName == null)
                gcDumpOutputFileName = Path.ChangeExtension(etlFileName, ".gcdump");

            // TODO FIX NOW retrieve the process name, ID etc.  
            var reader = new JavaScriptDumpGraphReader(LogFile);
            var memoryGraph = reader.Read(etlFileName);
            GCHeapDump.WriteMemoryGraph(memoryGraph, gcDumpOutputFileName);
            LogFile.WriteLine("[Wrote gcDump file {0}]", gcDumpOutputFileName);
        }

        /// <summary>
        /// Generate a GCDumpFile of a DotNet heap from ETW data in 'etlFileName', 
        /// need to have a V4.5.1 runtime (preferably V4.5.2) to have the proper events.    
        /// </summary>
        public void DotNetGCDumpFromETLFile(string etlFileName, string processNameOrId = null, string gcDumpOutputFileName = null)
        {
            if (gcDumpOutputFileName == null)
                gcDumpOutputFileName = PerfViewFile.ChangeExtension(etlFileName, ".gcdump");

            ETLPerfViewData.UnZipIfNecessary(ref etlFileName, LogFile);

            // TODO FIX NOW retrieve the process name, ID etc.  
            var reader = new DotNetHeapDumpGraphReader(LogFile);
            var memoryGraph = reader.Read(etlFileName, processNameOrId);
            GCHeapDump.WriteMemoryGraph(memoryGraph, gcDumpOutputFileName);
            LogFile.WriteLine("[Wrote gcDump file {0}]", gcDumpOutputFileName);
        }

        /// <summary>
        /// Pretty prints the raw .NET GC dump events (GCBulk*) with minimal processing as XML.   This is mostly
        /// useful for debugging, to see if the raw data sane if there is a question on why something is not showing
        /// up properly in a more user-friendly view.  
        /// </summary>
        /// <param name="etlFileName">The input ETW file containing the GC dump events</param>
        /// <param name="processId">The process to focus on.  0 (the default) says to pick the first process with Bulk GC events.</param>
        /// <param name="outputFileName">The output XML file.</param>
        public void DumpRawDotNetGCHeapEvents(string etlFileName, string processId = null, string outputFileName = null)
        {
            if (outputFileName == null)
                outputFileName = Path.ChangeExtension(etlFileName, ".rawEtwGCDump.xml");

            int proccessIdInt = 0;
            if (processId != null)
                proccessIdInt = int.Parse(processId);

            ETLPerfViewData.UnZipIfNecessary(ref etlFileName, LogFile);
            var typeLookup = new Dictionary<Address, string>(500);
            var events = new List<TraceEvent>();
            var edges = new List<GCBulkEdgeTraceData>();

            using (var source = new ETWTraceEventSource(etlFileName, TraceEventSourceType.MergeAll))
            using (TextWriter output = File.CreateText(outputFileName))
            {
                source.Clr.TypeBulkType += delegate (GCBulkTypeTraceData data)
                {
                    if (proccessIdInt == 0)
                        proccessIdInt = data.ProcessID;
                    if (proccessIdInt != data.ProcessID)
                        return;

                    output.WriteLine(" <TypeBulkType Proc=\"{0}\" TimeMSec=\"{1:f3}\" Count=\"{2}\"/>",
                        data.ProcessID, data.TimeStampRelativeMSec, data.Count);
                    for (int i = 0; i < data.Count; i++)
                    {
                        var typeData = data.Values(i);
                        typeLookup[typeData.TypeID] = typeData.TypeName;
                    }
                };
                source.Clr.GCBulkEdge += delegate (GCBulkEdgeTraceData data)
                {
                    if (proccessIdInt != data.ProcessID)
                        return;
                    output.WriteLine(" <GCBulkEdge Proc=\"{0}\" TimeMSec=\"{1:f3}\" Count=\"{2}\"/>",
                        data.ProcessID, data.TimeStampRelativeMSec, data.Count);
                    edges.Add((GCBulkEdgeTraceData)data.Clone());
                };
                source.Clr.GCBulkNode += delegate (GCBulkNodeTraceData data)
                {
                    if (proccessIdInt != data.ProcessID)
                        return;
                    events.Add(data.Clone());
                };
                source.Clr.GCBulkRootStaticVar += delegate (GCBulkRootStaticVarTraceData data)
                {
                    if (proccessIdInt != data.ProcessID)
                        return;
                    events.Add(data.Clone());
                };
                source.Clr.GCBulkRootEdge += delegate (GCBulkRootEdgeTraceData data)
                {
                    if (proccessIdInt != data.ProcessID)
                        return;
                    events.Add(data.Clone());
                };
                source.Clr.GCBulkRootConditionalWeakTableElementEdge += delegate (GCBulkRootConditionalWeakTableElementEdgeTraceData data)
                {
                    if (proccessIdInt != data.ProcessID)
                        return;
                    events.Add(data.Clone());
                };
                source.Clr.GCBulkRootCCW += delegate (GCBulkRootCCWTraceData data)
                {
                    if (proccessIdInt != data.ProcessID)
                        return;
                    events.Add(data.Clone());
                };
                source.Clr.GCBulkRCW += delegate (GCBulkRCWTraceData data)
                {
                    if (proccessIdInt != data.ProcessID)
                        return;
                    events.Add(data.Clone());
                };
                output.WriteLine("<HeapDumpEvents>");
                // Pass one process types and gather up interesting events.  
                source.Process();

                // Need to do these things after all the type events are processed. 
                foreach (var data in events)
                {
                    var node = data as GCBulkNodeTraceData;
                    if (node != null)
                    {
                        output.WriteLine(" <GCBulkNode Proc=\"{0}\" TimeMSec=\"{1:f3}\" Count=\"{2}\">",
                        data.ProcessID, data.TimeStampRelativeMSec, node.Count);
                        for (int i = 0; i < node.Count; i++)
                        {
                            var value = node.Values(i);
                            output.WriteLine("  <Node Type=\"{0}\" ObjectID=\"0x{1:x}\" Size=\"{2}\" EdgeCount=\"{3}\"/>",
                                typeName(typeLookup, value.TypeID), value.Address, value.Size, value.EdgeCount);

                            // TODO can show edges.   
                        }
                        output.WriteLine(" </GCBulkNode>");
                        continue;
                    }
                    var rootEdge = data as GCBulkRootEdgeTraceData;
                    if (rootEdge != null)
                    {
                        output.WriteLine(" <GCBulkRootEdge Proc=\"{0}\" TimeMSec=\"{1:f3}\" Count=\"{2}\">",
                        rootEdge.ProcessID, rootEdge.TimeStampRelativeMSec, rootEdge.Count);

                        for (int i = 0; i < rootEdge.Count; i++)
                        {
                            var value = rootEdge.Values(i);
                            output.WriteLine("  <RootEdge GCRootID=\"0x{0:x}\" ObjectID=\"0x{1:x}\" GCRootKind=\"{2}\" GCRootFlag=\"{3}\"/>",
                                 value.GCRootID, value.RootedNodeAddress, value.GCRootKind, value.GCRootFlag);
                        }
                        output.WriteLine(" </GCBulkRootEdge>");
                        continue;
                    }
                    var staticVar = data as GCBulkRootStaticVarTraceData;
                    if (staticVar != null)
                    {
                        output.WriteLine(" <GCBulkRootStaticVar Proc=\"{0}\" TimeMSec=\"{1:f3}\" Count=\"{2}\">",
                            staticVar.ProcessID, staticVar.TimeStampRelativeMSec, staticVar.Count);

                        for (int i = 0; i < staticVar.Count; i++)
                        {
                            var value = staticVar.Values(i);
                            output.WriteLine("  <StaticVar Type=\"{0}\" Name=\"{1}\" GCRootID=\"0x{2:x}\" ObjectID=\"0x{3:x}\"/>",
                                typeName(typeLookup, value.TypeID), XmlUtilities.XmlEscape(value.FieldName), value.GCRootID, value.ObjectID);

                        }
                        output.WriteLine(" </GCBulkRootStaticVar>");
                        continue;
                    }
                    var rcw = data as GCBulkRCWTraceData;
                    if (rcw != null)
                    {
                        output.WriteLine(" <GCBulkRCW Proc=\"{0}\" TimeMSec=\"{1:f3}\" Count=\"{2}\">",
                            rcw.ProcessID, rcw.TimeStampRelativeMSec, rcw.Count);
                        for (int i = 0; i < rcw.Count; i++)
                        {
                            var value = rcw.Values(i);
                            output.WriteLine("  <RCW Type=\"{0}\" ObjectID=\"0x{1:x}\" IUnknown=\"0x{2:x}\"/>",
                                 typeName(typeLookup, value.TypeID), value.ObjectID, value.IUnknown);
                        }
                        output.WriteLine(" </GCBulkRCW>");
                        continue;
                    }
                    var ccw = data as GCBulkRootCCWTraceData;
                    if (ccw != null)
                    {
                        output.WriteLine(" <GCBulkRootCCW Proc=\"{0}\" TimeMSec=\"{1:f3}\" Count=\"{2}\">",
                            ccw.ProcessID, ccw.TimeStampRelativeMSec, ccw.Count);
                        for (int i = 0; i < ccw.Count; i++)
                        {
                            var value = ccw.Values(i);
                            output.WriteLine("  <RootCCW Type=\"{0}\" ObjectID=\"0x{1:x}\" IUnknown=\"0x{2:x}\"/>",
                                 typeName(typeLookup, value.TypeID), value.ObjectID, value.IUnknown);
                        }
                        output.WriteLine(" </GCBulkRootCCW>");
                        continue;
                    }
                    var condWeakTable = data as GCBulkRootConditionalWeakTableElementEdgeTraceData;
                    if (condWeakTable != null)
                    {
                        output.WriteLine(" <GCBulkRootConditionalWeakTableElementEdge Proc=\"{0}\" TimeMSec=\"{1:f3}\" Count=\"{2}\">",
                            condWeakTable.ProcessID, condWeakTable.TimeStampRelativeMSec, condWeakTable.Count);

                        for (int i = 0; i < condWeakTable.Count; i++)
                        {
                            var value = condWeakTable.Values(i);
                            output.WriteLine("  <ConditionalWeakTableElementEdge GCRootID=\"0x{0:x}\" GCKeyID=\"0x{1:x}\" GCValueID=\"0x{2:x}\"/>",
                                 value.GCRootID, value.GCKeyNodeID, value.GCValueNodeID);
                        }
                        output.WriteLine(" </GCBulkRootConditionalWeakTableElementEdge>");
                        continue;
                    }
                }
                output.WriteLine("</HeapDumpEvents>");
            }
            LogFile.WriteLine("[Wrote XML output for process {0} to file {1}]", processId, outputFileName);
        }

        private static string typeName(Dictionary<Address, string> types, Address typeId)
        {
            string ret;
            if (types.TryGetValue(typeId, out ret))
                return XmlUtilities.XmlEscape(ret);
            return "TypeID(0x" + typeId.ToString("x") + ")";
        }

        /// <summary>
        /// Dumps a GCDump file as XML.  Useful for debugging heap dumping issues.   It is easier to read than 
        /// what is produced by 'WriteGCDumpAsXml' but can't be read in with as a '.gcdump.xml' file.  
        /// </summary>
        /// <param name="gcDumpFileName"></param>
        public void DumpGCDumpFile(string gcDumpFileName)
        {
            var log = LogFile;
            var gcDump = new GCHeapDump(gcDumpFileName);

            Graph graph = gcDump.MemoryGraph;

            log.WriteLine(
                   "Opened Graph {0} Bytes: {1:f3}M NumObjects: {2:f3}K  NumRefs: {3:f3}K Types: {4:f3}K RepresentationSize: {5:f1}M",
                   gcDumpFileName, graph.TotalSize / 1000000.0, (int)graph.NodeIndexLimit / 1000.0,
                   graph.TotalNumberOfReferences / 1000.0, (int)graph.NodeTypeIndexLimit / 1000.0,
                   graph.SizeOfGraphDescription() / 1000000.0);

            var outputFileName = Path.ChangeExtension(gcDumpFileName, ".heapDump.xml");
            using (StreamWriter writer = File.CreateText(outputFileName))
                ((MemoryGraph)graph).DumpNormalized(writer);

            log.WriteLine("[File {0} dumped as {1}.]", gcDumpFileName, outputFileName);
        }

        /// <summary>
        /// Dumps a GCDump file as gcdump.xml file.  THese files can be read back by PerfView.   
        /// </summary>
        /// <param name="gcDumpFileName">The input file (.gcdump)</param>
        /// <param name="outputFileName">The output file name (defaults to input file with .gcdump.xml suffix)</param>
        public void WriteGCDumpAsXml(string gcDumpFileName, string outputFileName = null)
        {
            var log = LogFile;
            var gcDump = new GCHeapDump(gcDumpFileName);
            Graph graph = gcDump.MemoryGraph;
            log.WriteLine(
                   "Opened Graph {0} Bytes: {1:f3}M NumObjects: {2:f3}K  NumRefs: {3:f3}K Types: {4:f3}K RepresentationSize: {5:f1}M",
                   gcDumpFileName, graph.TotalSize / 1000000.0, (int)graph.NodeIndexLimit / 1000.0,
                   graph.TotalNumberOfReferences / 1000.0, (int)graph.NodeTypeIndexLimit / 1000.0,
                   graph.SizeOfGraphDescription() / 1000000.0);

            if (outputFileName == null)
                outputFileName = Path.ChangeExtension(gcDumpFileName, ".gcDump.xml");

            using (StreamWriter writer = File.CreateText(outputFileName))
                XmlGcHeapDump.WriteGCDumpToXml(gcDump, writer);

            log.WriteLine("[File {0} written as {1}.]", gcDumpFileName, outputFileName);
        }

        /// <summary>
        /// Given a name (or guid) of a provider registered on the system, generate a '.manifest.xml' file that 
        /// represents the manifest for that provider.   
        /// </summary>  
        public void DumpRegisteredManifest(string providerName, string outputFileName = null)
        {
            if (outputFileName == null)
                outputFileName = providerName + ".manifest.xml";

            var str = RegisteredTraceEventParser.GetManifestForRegisteredProvider(providerName);
            LogFile.WriteLine("[Output written to {0}]", outputFileName);
            File.WriteAllText(outputFileName, str);
        }

        /// <summary>
        /// Opens a text window that displays events from the given set of event source names
        /// By default the output goes to a GUI window but you can use the /LogFile option to 
        /// redirect it elsewhere.  
        /// </summary>
        /// <param name="etwProviderNames"> a comma separated list of providers specs (just like /Providers value)</param>
        public void Listen(string etwProviderNames)
        {
            var sessionName = "PerfViewListen";
            LogFile.WriteLine("Creating Session {0}", sessionName);
            using (var session = new TraceEventSession(sessionName))
            {
                TextWriter listenTextEditorWriter = null;
                if (!App.CommandLineArgs.NoGui)
                {
                    GuiApp.MainWindow.Dispatcher.BeginInvoke((Action)delegate ()
                    {
                        var logTextWindow = new Controls.TextEditorWindow();
                        // Destroy the session when the widow is closed.  
                        logTextWindow.Closed += delegate (object sender, EventArgs e) { session.Dispose(); };

                        listenTextEditorWriter = new Controls.TextEditorWriter(logTextWindow.m_TextEditor);
                        logTextWindow.TextEditor.IsReadOnly = true;
                        logTextWindow.Title = "Listening to " + etwProviderNames;
                        logTextWindow.Show();
                    });
                }

                // Add callbacks for any EventSource Events to print them to the Text window
                Action<TraceEvent> onAnyEvent = delegate (TraceEvent data)
                {
                    try
                    {
                        String str = data.TimeStamp.ToString("HH:mm:ss.fff ");
                        str += data.EventName;
                        str += "\\" + data.ProviderName + " ";
                        for (int i = 0; i < data.PayloadNames.Length; i++)
                        {
                            var payload = data.PayloadNames[i];
                            if (i != 0)
                                str += ",";
                            str += String.Format("{0}=\"{1}\"", payload, data.PayloadByName(payload));
                        }

                        if (App.CommandLineArgs.NoGui)
                            App.CommandProcessor.LogFile.WriteLine("{0}", str);
                        else
                        {
                            GuiApp.MainWindow.Dispatcher.BeginInvoke((Action)delegate ()
                            {
                                // This should be null because the BeginInvoke above came before this 
                                // and both are constrained to run in the same thread, so this has to
                                // be after it (and thus it is initialized).  
                                Debug.Assert(listenTextEditorWriter != null);
                                listenTextEditorWriter.WriteLine("{0}", str);
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        App.CommandProcessor.LogFile.WriteLine("Error: Exception during event processing of event {0}: {1}", data.EventName, e.Message);
                    }
                };

                session.Source.Dynamic.All += onAnyEvent;
                // Add support for EventWriteStrings (which are not otherwise parsable).  
                session.Source.UnhandledEvents += delegate (TraceEvent data)
                {
                    string formattedMessage = data.FormattedMessage;
                    if (formattedMessage != null)
                        listenTextEditorWriter.WriteLine("{0} {1} Message=\"{2}\"",
                            data.TimeStamp.ToString("HH:mm:ss.fff"), data.EventName, formattedMessage);
                };

                // Enable all the providers the users asked for

                var parsedProviders = ProviderParser.ParseProviderSpecs(etwProviderNames.Split(','), null, LogFile);
                foreach(var parsedProvider in parsedProviders)
                {
                    LogFile.WriteLine("Enabling provider {0}:{1:x}:{2}", parsedProvider.Name, (ulong) parsedProvider.MatchAnyKeywords, parsedProvider.Level);
                    session.EnableProvider(parsedProvider.Name, parsedProvider.Level, (ulong)parsedProvider.MatchAnyKeywords, parsedProvider.Options);
                }

                // Start listening for events.  
                session.Source.Process();
            }
        }

        /// <summary>
        /// Creates perfView.xml file that represents the directory size of 'directoryPath' and places
        /// it in 'outputFileName'.  
        /// </summary>
        /// <param name="directoryPath">The directory whose size is being computed (default to the current dir)</param>
        /// <param name="outputFileName">The output fileName (defaults to NAME.dirSize.PerfView.xml.zip) where NAME is
        /// the simple name of the directory.</param>
        public void DirectorySize(string directoryPath = null, string outputFileName = null)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                // Hop to the GUI thread and get the arguments from a dialog box and then call myself again.  
                GuiApp.MainWindow.Dispatcher.BeginInvoke((Action)delegate ()
                {
                    var dialog = new FileInputAndOutput(delegate (string dirPath, string outFileName)
                    {
                        App.CommandLineArgs.CommandAndArgs = new string[] { "DirectorySize", dirPath, outFileName };
                        App.CommandLineArgs.DoCommand = App.CommandProcessor.UserCommand;
                        GuiApp.MainWindow.ExecuteCommand("Computing directory size", App.CommandLineArgs.DoCommand);
                    });
                    dialog.SelectingDirectories = true;
                    dialog.OutputExtension = ".dirSize.perfView.xml.zip";
                    dialog.CurrentDirectory = GuiApp.MainWindow.CurrentDirectory.FilePath;
                    dialog.HelpAnchor = "DirectorySize";
                    dialog.Instructions = "Please enter the name of the directory on which to do a disk size analysis " +
                                          "and optionally the output file where place the resulting data.";
                    dialog.Title = "Disk Size Analysis";
                    dialog.Show();
                });
                return;
            }
            if (string.IsNullOrWhiteSpace(outputFileName))
            {
                if (char.IsLetterOrDigit(directoryPath[0]))
                    outputFileName = Path.GetFileNameWithoutExtension(Path.GetFullPath(directoryPath)) + ".dirSize.PerfView.xml.zip";
                else
                    outputFileName = "dirSize.PerfView.xml.zip";
            }

            LogFile.WriteLine("[Computing the file size of the directory {0}...]", directoryPath);
            // Open and close the output file to make sure we can write to it, that way we fail early if we can't
            File.OpenWrite(outputFileName).Close();
            File.Delete(outputFileName);

            FileSizeStackSource fileSizeStackSource = new FileSizeStackSource(directoryPath, LogFile);
            XmlStackSourceWriter.WriteStackViewAsZippedXml(fileSizeStackSource, outputFileName);
            LogFile.WriteLine("[Wrote file {0}]", outputFileName);

            if (!App.CommandLineArgs.NoGui && App.CommandLineArgs.LogFile == null)
            {
                if (outputFileName.EndsWith(".perfView.xml.zip", StringComparison.OrdinalIgnoreCase) && File.Exists(outputFileName))
                    GuiApp.MainWindow.OpenNext(outputFileName);
            }
        }

        /// <summary>
        /// Creates a .perfView.xml.zip that represents the profiling data from a perf script output dump. Adding a
        /// --threadtime tag enables blocked time investigations on the perf script dump.
        /// </summary>
        /// <param name="path">The path to the perf script dump, right now, either a file with suffix perf.data.dump,
        /// .trace.zip or .data.txt will be accepted.</param>
        /// <param name="threadTime">Option to turn on thread time on the perf script dump.</param>
        public void PerfScript(string path, string threadTime = null)
        {
            bool doThreadTime = threadTime != null && threadTime == "--threadtime";

            var perfScriptStackSource = new ParallelLinuxPerfScriptStackSource(path, doThreadTime);
            string outputFileName = Path.ChangeExtension(path, ".perfView.xml.zip");

            XmlStackSourceWriter.WriteStackViewAsZippedXml(perfScriptStackSource, outputFileName);

            if (!App.CommandLineArgs.NoGui && App.CommandLineArgs.LogFile == null)
            {
                if (outputFileName.EndsWith(".perfView.xml.zip", StringComparison.OrdinalIgnoreCase) && File.Exists(outputFileName))
                {
                    GuiApp.MainWindow.OpenNext(outputFileName);
                }
            }
        }

        /// <summary>
        /// Creates a stack source out of the textFileName where each line is a frame (which is directly rooted)
        /// and every such line has a metric of 1.  Thus it allows you to form histograms for these lines nicely
        /// in perfView.  
        /// </summary>
        /// <param name="textFilePath"></param>
        public void TextHistogram(string textFilePath)
        {
            LogFile.WriteLine("[Opening {0} as a Histogram]");
            var stackSource = new PerfView.OtherSources.TextStackSource();
            stackSource.Read(textFilePath);
            var stacks = new Stacks(stackSource);
            OpenStackViewer(stacks);
        }

        /// <summary>
        /// Reads a project N metaData.csv file (From ILC.exe)  and converts it to a .GCDump file (a heap)
        /// </summary>
        public void ProjectNMetaData(string projectNMetadataDataCsv)
        {
            var metaDataReader = new ProjectNMetaDataLogReader();
            var memoryGraph = metaDataReader.Read(projectNMetadataDataCsv);

            var outputName = Path.ChangeExtension(projectNMetadataDataCsv, ".gcdump");
            GCHeapDump.WriteMemoryGraph(memoryGraph, outputName);
            LogFile.WriteLine("[Writing the GCDump to {0}]", outputName);
        }

        /// <summary>
        /// This is used to visualize the Project N ILTransformed\*.reflectionlog.csv file so it can viewed 
        /// in PerfVIew.   
        /// </summary>
        /// <param name="reflectionLogFile">The name of the file to view</param>
        public void ReflectionUse(string reflectionLogFile)
        {
            LogFile.WriteLine("[Opening {0} as a Histogram]");
            var stackSource = new PerfView.OtherSources.TextStackSource();
            var lineNum = 0;

            stackSource.StackForLine = delegate (StackSourceInterner interner, string line)
            {
                lineNum++;
                StackSourceCallStackIndex ret = StackSourceCallStackIndex.Invalid;
                Match m = Regex.Match(line, "^(.*?),(.*?),\"(.*)\"");
                if (m.Success)
                {
                    string reflectionType = m.Groups[1].Value;
                    string entityKind = m.Groups[2].Value;
                    string symbol = m.Groups[3].Value;

                    if (entityKind == "Method" || entityKind == "Field")
                        symbol = Regex.Replace(symbol, "^.*?[^,] +", "");
                    ret = interner.CallStackIntern(interner.FrameIntern("REFLECTION " + reflectionType), ret);
                    ret = interner.CallStackIntern(interner.FrameIntern("KIND " + entityKind), ret);
                    ret = interner.CallStackIntern(interner.FrameIntern("SYM " + symbol), ret);
                }
                else
                    LogFile.WriteLine("Warning {0}: Could not parse {1}", lineNum, line);

                return ret;
            };
            stackSource.Read(reflectionLogFile);
            var stacks = new Stacks(stackSource);
            OpenStackViewer(stacks);
        }

        /// <summary>
        /// ImageSize generates a XML report (by default inputExeName.imageSize.xml) that 
        /// breaks down the executable file 'inputExeName' by the symbols in it (fetched from
        /// its PDB.  The PDB needs to be locatable (either on the _NT_SYMBOL_PATH, or next to 
        /// the file, or in its original build location).   This report can be viewed with
        /// PerfView (it looks like a GC heap).  
        /// </summary>
        /// <param name="inputExeName">The name of the EXE (or DLL) that you wish to analyze.  If blank it will prompt for one.</param>
        /// <param name="outputFileName">The name of the report file.  Defaults to the inputExeName
        /// with a .imageSize.xml suffix.</param>
        public void ImageSize(string inputExeName = null, string outputFileName = null)
        {
            if (outputFileName == null)
                outputFileName = Path.ChangeExtension(inputExeName, ".imageSize.xml");

            if (string.IsNullOrWhiteSpace(inputExeName))
            {
                if (App.CommandLineArgs.NoGui)
                    throw new ApplicationException("Must specify an input EXE name");
                // Hop to the GUI thread and get the arguments from a dialog box and then call myself again.  
                GuiApp.MainWindow.Dispatcher.BeginInvoke((Action)delegate ()
                {
                    var dialog = new FileInputAndOutput(delegate (string inExeName, string outFileName)
                    {
                        App.CommandLineArgs.CommandAndArgs = new string[] { "ImageSize", inExeName, outFileName };
                        App.CommandLineArgs.DoCommand = App.CommandProcessor.UserCommand;
                        GuiApp.MainWindow.ExecuteCommand("Computing directory size", App.CommandLineArgs.DoCommand);
                    });
                    dialog.InputExtentions = new string[] { ".dll", ".exe" };
                    dialog.OutputExtension = ".imageSize.xml";
                    dialog.CurrentDirectory = GuiApp.MainWindow.CurrentDirectory.FilePath;
                    dialog.HelpAnchor = "ImageSize";
                    dialog.Instructions = "Please enter the name of the EXE or DLL on which you wish to do a size analysis " +
                                          "and optionally the output file where place the resulting data.";
                    dialog.Title = "Image Size Analysis";
                    dialog.Show();
                });
                return;
            }

            string pdbScopeExe = Path.Combine(ExtensionsDirectory, "PdbScope.exe");
            if (!File.Exists(pdbScopeExe))
                throw new ApplicationException(@"The PerfViewExtensions\PdbScope.exe file does not exit.   ImageSize report not possible");

            // Currently we need to find the DLL again to unmangle names completely, and this DLL name is emedded in the output file.
            // Remove relative paths and try to make it universal so that you stand the best chance of finding this DLL.   
            inputExeName = App.MakeUniversalIfPossible(Path.GetFullPath(inputExeName));

            string commandLine = string.Format("{0} /x /f /s {1}", pdbScopeExe, Command.Quote(inputExeName));
            LogFile.WriteLine("Running command {0}", commandLine);

            FileUtilities.ForceDelete(outputFileName);
            Command.Run(commandLine, new CommandOptions().AddOutputStream(LogFile).AddTimeout(3600000));

            if (!File.Exists(outputFileName) || File.GetLastWriteTimeUtc(outputFileName) <= File.GetLastWriteTimeUtc(inputExeName))
            {
                // TODO can remove after pdbScope gets a proper outputFileName parameter
                string pdbScopeOutputFile = Path.ChangeExtension(Path.GetFullPath(Path.GetFileName(inputExeName)), ".pdb.xml");
                if (!File.Exists(pdbScopeOutputFile))
                    throw new ApplicationException("Error PdbScope did not create a file " + pdbScopeOutputFile);
                LogFile.WriteLine("Moving {0} to {1}", pdbScopeOutputFile, outputFileName);
                FileUtilities.ForceMove(pdbScopeOutputFile, outputFileName);
            }

            // TODO This is pretty ugly.  If the main window is working we can't launch it.   
            if (!App.CommandLineArgs.NoGui && App.CommandLineArgs.LogFile == null)
            {
                if (outputFileName.EndsWith(".imageSize.xml", StringComparison.OrdinalIgnoreCase) && File.Exists(outputFileName))
                    GuiApp.MainWindow.OpenNext(outputFileName);
            }
        }

        /// <summary>
        /// Dumps the PDB signature associated with pdb 'pdbName'
        /// </summary>
        public void PdbSignature(string pdbFileName)
        {
            var reader = new SymbolReader(LogFile);
            var module = reader.OpenSymbolFile(pdbFileName);
            LogFile.WriteLine("[{0} has Signature {1}]", pdbFileName, module.PdbGuid);
            OpenLog();
        }

        /// <summary>
        /// Mainly here for testing
        /// </summary>
        /// <param name="dllName"></param>
        /// <param name="ILPdb"></param>
        public void LookupSymbolsFor(string dllName, string ILPdb=null)
        {
            var symbolReader = App.GetSymbolReader();
            string ret = symbolReader.FindSymbolFilePathForModule(dllName, (ILPdb??"false") == "true");
            if (ret != null)
                LogFile.WriteLine("[Returned PDB {0}]", ret);
            else
                LogFile.WriteLine("[Could not find PDB for {0}]", dllName);
        }

        public void LookupSymbols(string pdbFileName, string pdbGuid, string pdbAge)
        {
            var symbolReader = App.GetSymbolReader();
            string ret = symbolReader.FindSymbolFilePath(pdbFileName, Guid.Parse(pdbGuid), int.Parse(pdbAge));
            if (ret != null)
                LogFile.WriteLine("[Returned PDB {0}]", ret);
            else
                LogFile.WriteLine("[Could not find PDB for {0}/{1}/{2}]", pdbFileName, pdbGuid, pdbAge);
        }

        class CodeSymbolListener
        {
            public CodeSymbolListener(TraceEventDispatcher source, string targetSymbolCachePath)
            {
                m_symbolFiles = new Dictionary<long, CodeSymbolState>();
                m_targetSymbolCachePath = targetSymbolCachePath;

                source.Clr.AddCallbackForEvents<ModuleLoadUnloadTraceData>(OnModuleLoad);
                source.Clr.AddCallbackForEvents<CodeSymbolsTraceData>(OnCodeSymbols);
            }

            #region private
            private void OnModuleLoad(ModuleLoadUnloadTraceData data)
            {
                Put(data.ProcessID, data.ModuleID, new CodeSymbolState(data, m_targetSymbolCachePath));
            }

            private void OnCodeSymbols(CodeSymbolsTraceData data)
            {
                CodeSymbolState state = Get(data.ProcessID, data.ModuleId);
                if (state != null)
                    state.OnCodeSymbols(data);
            }

            class CodeSymbolState
            {
                string m_pdbIndexPath;
                MemoryStream m_stream;
                private ModuleLoadUnloadTraceData m_moduleData;
                string m_symbolCachePath;

                public CodeSymbolState(ModuleLoadUnloadTraceData data, string path)
                {
                    // See Symbols/Symbolreader.cs for details on making Symbols server paths.   Here is the jist
                    // pdbIndexPath = pdbSimpleName + @"\" + pdbIndexGuid.ToString("N") + pdbIndexAge.ToString() + @"\" + pdbSimpleName;

                    // TODO COMPLETE
                    m_moduleData = data;
                    m_stream = new MemoryStream();
                    m_symbolCachePath = path;

                    string pdbSimpleName = data.ModuleILFileName.Replace(".exe", ".pdb").Replace(".dll", ".pdb");
                    if (!pdbSimpleName.EndsWith(".pdb"))
                    {
                        pdbSimpleName += ".pdb";
                    }
                    m_pdbIndexPath = pdbSimpleName + @"\" +
                        data.ManagedPdbSignature.ToString("N") + data.ManagedPdbAge.ToString() + @"\" + pdbSimpleName;
                }

                public void OnCodeSymbols(CodeSymbolsTraceData data)
                {
                    // TODO read in a chunk if it is out of order fail, when complete close the file.
                    //using (StreamWriter writer = File.WriteAllBytes(m_pdbIndexPath, m_bytes))
                    //    ((MemoryGraph)graph).DumpNormalized(writer);

                    //Assumes the length of the stream does not exceed 2^32
                    m_stream.Write(data.Chunk, 0, data.ChunkLength);
                    if ((data.ChunkNumber + 1) == data.TotalChunks)
                    {
                        byte[] bytes = new byte[m_stream.Length];
                        m_stream.Seek(0, SeekOrigin.Begin);
                        m_stream.Read(bytes, 0, (int)m_stream.Length);
                        string fullPath = m_symbolCachePath + @"\" + m_pdbIndexPath;
                        if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                        }
                        File.WriteAllBytes(fullPath, bytes);
                    }
                }
            }

            // hides details of how process/module IDs are looked up.  
            CodeSymbolState Get(int processID, long moduleID)
            {
                CodeSymbolState ret = null;
                m_symbolFiles.TryGetValue((((long)processID) << 48) + moduleID, out ret);
                return ret;
            }
            void Put(int processID, long moduleID, CodeSymbolState value)
            {
                m_symbolFiles[(((long)processID) << 48) + moduleID] = value;
            }

            // Indexed by key;
            Dictionary<long, CodeSymbolState> m_symbolFiles;
            string m_targetSymbolCachePath;
            #endregion
        }

        /// <summary>
        /// Listen for the CLR CodeSymbols events and when you find them write them 
        /// to the directory targetSymbolCachePath using standard symbol server conventions
        /// (Name.Pdb\GUID-AGE\Name.Pdb)
        /// 
        /// Usage 
        /// </summary>
        /// <param name="targetSymbolCachePath"></param>
        public void GetDynamicAssemblySymbols(string targetSymbolCachePath)
        {
            var sessionName = "PerfViewSymbolListener";
            LogFile.WriteLine("Creating Session {0}", sessionName);
            using (var session = new TraceEventSession(sessionName))
            {
                var codeSymbolListener = new CodeSymbolListener(session.Source, targetSymbolCachePath);
                LogFile.WriteLine("Enabling CLR Loader and CodeSymbols events");
                session.EnableProvider(ClrTraceEventParser.ProviderGuid, TraceEventLevel.Verbose,
                    (long)(ClrTraceEventParser.Keywords.Codesymbols | ClrTraceEventParser.Keywords.Loader));
                session.Source.Process();
            }
        }

        public void NGenImageSize(string ngenImagePath)
        {
            SymbolReader symReader = App.GetSymbolReader();
            MemoryGraph imageGraph = ImageFileMemoryGraph.Create(ngenImagePath, symReader);

            var fileName = Path.GetFileNameWithoutExtension(ngenImagePath);
            var outputFileName = fileName + ".gcdump";
            GCHeapDump.WriteMemoryGraph(imageGraph, outputFileName);
            LogFile.WriteLine("[Wrote file " + outputFileName + "]");

            if (!App.CommandLineArgs.NoGui && App.CommandLineArgs.LogFile == null)
                GuiApp.MainWindow.OpenNext(outputFileName);
        }

        /// <summary>
        /// Computes the GCStats HTML report for etlFile.  
        /// </summary>
        public void GCStats(string etlFile)
        {
            ETLPerfViewData.UnZipIfNecessary(ref etlFile, LogFile);

            List<Microsoft.Diagnostics.Tracing.Analysis.TraceProcess> processes = new List<Microsoft.Diagnostics.Tracing.Analysis.TraceProcess>();
            using (var source = new ETWTraceEventSource(etlFile))
            {
                Microsoft.Diagnostics.Tracing.Analysis.TraceLoadedDotNetRuntimeExtensions.NeedLoadedDotNetRuntimes(source);
                source.Process();
                foreach (var proc in Microsoft.Diagnostics.Tracing.Analysis.TraceProcessesExtensions.Processes(source))
                    if (Microsoft.Diagnostics.Tracing.Analysis.TraceLoadedDotNetRuntimeExtensions.LoadedDotNetRuntime(proc) != null) processes.Add(proc);
            }

            var outputFileName = Path.ChangeExtension(etlFile, ".gcStats.html");
            using (var output = File.CreateText(outputFileName))
            {
                LogFile.WriteLine("Wrote GCStats to {0}", outputFileName);
                Stats.ClrStats.ToHtml(output, processes, outputFileName, "GCStats", Stats.ClrStats.ReportType.GC);
                foreach (Microsoft.Diagnostics.Tracing.Analysis.TraceProcess proc in processes)
                {
                    var mang = Microsoft.Diagnostics.Tracing.Analysis.TraceLoadedDotNetRuntimeExtensions.LoadedDotNetRuntime(proc);
                    if (mang != null)
                    {
                        var csvName = Path.ChangeExtension(etlFile, ".gcStats." + proc.ProcessID.ToString() + ".csv");
                        LogFile.WriteLine("  Wrote CsvFile {0}", csvName);
                        Stats.GcStats.ToCsv(csvName, mang);
                    }
                }
            }
            if (!App.CommandLineArgs.NoGui)
                OpenHtmlReport(outputFileName, "GCStats report");
        }

        /// <summary>
        /// Outputs some detailed Server GC analysis to a file.
        /// </summary>
        public void ServerGCReport(string etlFile)
        {
            if (PerfView.AppLog.InternalUser)
            {
                ETLPerfViewData.UnZipIfNecessary(ref etlFile, LogFile);

                List<Microsoft.Diagnostics.Tracing.Analysis.TraceProcess> gcStats = new List<Microsoft.Diagnostics.Tracing.Analysis.TraceProcess>();
                using (TraceLog tracelog = TraceLog.OpenOrConvert(etlFile))
                {
                    using (var source = tracelog.Events.GetSource())
                    {
                        Microsoft.Diagnostics.Tracing.Analysis.TraceLoadedDotNetRuntimeExtensions.NeedLoadedDotNetRuntimes(source);
                        Microsoft.Diagnostics.Tracing.Analysis.TraceProcessesExtensions.AddCallbackOnProcessStart(source, proc => { proc.Log = tracelog; });
                        source.Process();
                        foreach (var proc in Microsoft.Diagnostics.Tracing.Analysis.TraceProcessesExtensions.Processes(source))
                            if (Microsoft.Diagnostics.Tracing.Analysis.TraceLoadedDotNetRuntimeExtensions.LoadedDotNetRuntime(proc) != null) gcStats.Add(proc);
                    }
                }

                var outputFileName = Path.ChangeExtension(etlFile, ".gcStats.html");
                using (var output = File.CreateText(outputFileName))
                {
                    LogFile.WriteLine("Wrote GCStats to {0}", outputFileName);
                    Stats.ClrStats.ToHtml(output, gcStats, outputFileName, "GCStats", Stats.ClrStats.ReportType.GC, false, true /* do server report */);
                }
                if (!App.CommandLineArgs.NoGui)
                    OpenHtmlReport(outputFileName, "GCStats report");
            }
        }

        /// <summary>
        /// Computes the JITStats HTML report for etlFile.  
        /// </summary>
        public void JITStats(string etlFile)
        {
            ETLPerfViewData.UnZipIfNecessary(ref etlFile, LogFile);

            List<Microsoft.Diagnostics.Tracing.Analysis.TraceProcess> jitStats = new List<Microsoft.Diagnostics.Tracing.Analysis.TraceProcess>(); ;
            using (var source = new ETWTraceEventSource(etlFile))
            {
                Microsoft.Diagnostics.Tracing.Analysis.TraceLoadedDotNetRuntimeExtensions.NeedLoadedDotNetRuntimes(source);
                source.Process();
                foreach (var proc in Microsoft.Diagnostics.Tracing.Analysis.TraceProcessesExtensions.Processes(source))
                    if (Microsoft.Diagnostics.Tracing.Analysis.TraceLoadedDotNetRuntimeExtensions.LoadedDotNetRuntime(proc) != null) jitStats.Add(proc);
            }

            var outputFileName = Path.ChangeExtension(etlFile, ".jitStats.html");
            using (var output = File.CreateText(outputFileName))
            {
                LogFile.WriteLine("Wrote JITStats to {0}", outputFileName);
                Stats.ClrStats.ToHtml(output, jitStats, outputFileName, "JitStats", Stats.ClrStats.ReportType.JIT);
                foreach (var proc in jitStats)
                {
                    var mang = Microsoft.Diagnostics.Tracing.Analysis.TraceLoadedDotNetRuntimeExtensions.LoadedDotNetRuntime(proc);

                    if (mang != null && mang.JIT.Stats().Interesting)
                    {
                        var csvName = Path.ChangeExtension(etlFile, ".jitStats." + proc.ProcessID.ToString() + ".csv");
                        LogFile.WriteLine("  Wrote CsvFile {0}", csvName);
                        Stats.JitStats.ToCsv(csvName, mang);
                    }
                }
            }
            if (!App.CommandLineArgs.NoGui)
                OpenHtmlReport(outputFileName, "JITStats report");
        }

        ///// <summary>
        ///// Given a PDB file, dump the source server information in the PDB file.
        ///// </summary>
        //public void DumpSourceServerStream(string pdbFile)
        //{
        //    SymbolReader reader = new SymbolReader(LogFile);
        //    SymbolModule module = reader.OpenSymbolFile(pdbFile);

        //    string srcsrvData = module.GetSrcSrvStream();
        //    if (srcsrvData == null)
        //        LogFile.WriteLine("[No source server information on {0}]", pdbFile);
        //    else
        //    {
        //        LogFile.WriteLine("Source Server Data for {0}", pdbFile);
        //        LogFile.Write(srcsrvData);
        //        LogFile.WriteLine();
        //        LogFile.WriteLine("[Source Server Data Dumped into log for {0}]", pdbFile);
        //    }
        //}

#if CROSS_GENERATION_LIVENESS
        public void CollectCrossGenerationLiveness(string processId, string generationToTrigger, string promotedBytes, string dumpFilePath)
        {
            // Convert the process id.
            int pid = Convert.ToInt32(processId);

            // Convert the generation to trigger.
            int gen = Convert.ToInt32(generationToTrigger);

            // Convert promoted bytes threshold.
            ulong promotedBytesThreshold = Convert.ToUInt64(promotedBytes);

            // Validate the input file name.
            string fileName = Path.GetFileName(dumpFilePath);
            if (string.IsNullOrEmpty(fileName) || !dumpFilePath.EndsWith(".gcdump", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Invalid GCDump file path.  The specified path must contain a file name that ends in .gcdump.");
            }

            HeapDumper.DumpGCHeapForCrossGenerationLiveness(pid, gen, promotedBytesThreshold, dumpFilePath, LogFile);
        }
#endif

        /// <summary>
        /// Gets the ETW keywords (bitset definitions of what can be turned on in the provider) for a given provider. 
        /// Currently does not work for EventSources 
        /// </summary>
        /// <param name="providerNameOrGuid">The name or GUID of the provider to look up</param>
        public void ListProviderKeywords(string providerNameOrGuid)
        {
            Guid providerGuid;
            if (Regex.IsMatch(providerNameOrGuid, "........-....-....-....-............"))
            {
                if (!Guid.TryParse(providerNameOrGuid, out providerGuid))
                    throw new ApplicationException("Could not parse Guid '" + providerNameOrGuid + "'");
            }
            else if (providerNameOrGuid.StartsWith("*"))
                providerGuid = TraceEventProviders.GetEventSourceGuidFromName(providerNameOrGuid.Substring(1));
            else
            {
                providerGuid = TraceEventProviders.GetProviderGuidByName(providerNameOrGuid);
                if (providerGuid == Guid.Empty)
                    throw new ApplicationException("Could not find provider name '" + providerNameOrGuid + "'");
            }

            // TODO support eventSources
            LogFile.WriteLine("Keywords for the Provider {0} ({1})", providerNameOrGuid, providerGuid);
            List<ProviderDataItem> keywords = TraceEventProviders.GetProviderKeywords(providerGuid);
            foreach (ProviderDataItem keyword in keywords)
                LogFile.WriteLine("  {0,-30} {1,16:x} {2}", keyword.Name, keyword.Value, keyword.Description);
            OpenLog();
        }

        /// <summary>
        /// returns a list of providers that exist (can be enabled) in a particular process.  Currently EventSouces 
        /// are returned as GUIDs.  
        /// </summary>
        /// <param name="processNameOrId">The process name (exe without extension) or process ID of the process of interest.</param>
        public void ListProvidersInProcess(string processNameOrId)
        {
            var processID = HeapDumper.GetProcessID(processNameOrId);
            if (processID < 0)
                throw new ApplicationException("Could not find a process with a name or ID of '" + processNameOrId + "'");

            LogFile.WriteLine("Providers that can be enabled in process {0} ({1})", processNameOrId, processID);
            LogFile.WriteLine("EventSources are shown as GUIDs, GUIDS that are likely to be EventSources are marked with *.");
            var providersInProcess = TraceEventProviders.GetRegisteredProvidersInProcess(processID);
            SortAndPrintProviders(providersInProcess, true);
        }

        /// <summary>
        /// returns a list of all providers that have published their meta-data to the Operating system.  This does NOT
        /// include EventSources and is a long list.  Some of these are not actually active and thus will have no effect
        /// if they are enabled (see ListProvidersInProcess). 
        /// </summary>
        public void ListPublishedProviders()
        {
            var publishedProviders = new List<Guid>(TraceEventProviders.GetPublishedProviders());
            LogFile.WriteLine("All Providers Published to the Operating System");
            SortAndPrintProviders(publishedProviders);
        }

        private void SortAndPrintProviders(List<Guid> providers, bool markEventSources = false)
        {
            // Sort them by name
            providers.Sort(delegate (Guid x, Guid y)
            {
                return string.Compare(TraceEventProviders.GetProviderName(x), TraceEventProviders.GetProviderName(y), StringComparison.OrdinalIgnoreCase);
            });

            string mark = "";
            foreach (var providerGuid in providers)
            {
                if (markEventSources)
                    mark = TraceEventProviders.MaybeAnEventSource(providerGuid) ? "*" : " ";
                LogFile.WriteLine("  {0}{1,-39} {2}", mark, TraceEventProviders.GetProviderName(providerGuid), providerGuid);
            }
            OpenLog();
        }

#if ENUMERATE_SERIALIZED_EXCEPTIONS_ENABLED     // TODO turn on when CLRMD has been updated. 
        /// <summary>
        /// PrintSerializedExceptionFromProcessDump
        /// </summary>
        /// <param name="inputDumpFile">inputDumpFile</param>
        public void PrintSerializedExceptionFromProcessDump(string inputDumpFile)
        {
            TextWriter log = LogFile;
            if (!App.IsElevated)
                throw new ApplicationException("Must be Administrator (elevated).");

            if (Environment.Is64BitOperatingSystem)
            {
                // TODO FIX NOW.   Find a way of determing which architecture a dump is
                try
                {
                    log.WriteLine("********** TRYING TO OPEN THE DUMP AS 64 BIT ************");
                    PrintSerializedExceptionFromProcessDumpThroughHeapDump(inputDumpFile, log, ProcessorArchitecture.Amd64);
                    return; // Yeah! success the first time
                }
                catch (Exception e)
                {
                    // It might have failed because this was a 32 bit dump, if so try again.  
                    if (e is ApplicationException)
                    {
                        log.WriteLine("********** TRYING TO OPEN THE DUMP AS 32 BIT ************");
                        PrintSerializedExceptionFromProcessDumpThroughHeapDump(inputDumpFile, log, ProcessorArchitecture.X86);
                        return;
                    }
                    throw;
                }
            }
            else
            {
                PrintSerializedExceptionFromProcessDumpThroughHeapDump(inputDumpFile, log, ProcessorArchitecture.X86);
            }

        }

        private void PrintSerializedExceptionFromProcessDumpThroughHeapDump(string inputDumpFile, TextWriter log, ProcessorArchitecture arch)
        {
            var directory = arch.ToString().ToLowerInvariant();
            var heapDumpExe = Path.Combine(SupportFiles.SupportFileDir, directory, "HeapDump.exe");
            var options = new CommandOptions().AddNoThrow().AddTimeout(CommandOptions.Infinite);
            options.AddOutputStream(LogFile);

            options.AddEnvironmentVariable("_NT_SYMBOL_PATH", App.SymbolPath);
            log.WriteLine("set _NT_SYMBOL_PATH={0}", App.SymbolPath);

            var commandLine = string.Format("\"{0}\" {1} \"{2}\"", heapDumpExe, "/dumpSerializedException:", inputDumpFile);
            log.WriteLine("Exec: {0}", commandLine);
            var cmd = Command.Run(commandLine, options);
            if (cmd.ExitCode != 0)
            {
                throw new ApplicationException("HeapDump failed with exit code " + cmd.ExitCode);
            }
        }
#endif

#if false 
        public void Test()
        {
            Cache<string, string> myCache = new Cache<string, string>(8);

            string prev = null;
            for(int i = 0; ;  i++)
            {
                var str = i.ToString() + (i*i).ToString(); 

                Trace.WriteLine(string.Format("**** BEFORE ITERATION {0} CACHE\r\n{1}********", i,  myCache.ToString()));
                if (i == 48)
                    break;

                Trace.WriteLine(string.Format("ADD {0}", str));
                myCache.Add(str, "Out" + str);

                if (i % 2 == 0) myCache.Get("00");

                Trace.WriteLine(string.Format("FETCH {0} = {1}", str, myCache.Get(str)));
                if (prev != null)
                    Trace.WriteLine(string.Format("FETCH {0} = {1}", prev, myCache.Get(prev)));
                prev = str;
            }
        }
#endif
        #region private
        /// <summary>
        /// Strips the file extension for files and if extension is .etl.zip removes both.
        /// </summary>
        private String StripFileExt(String inputFile)
        {
            string outputFileName = Path.ChangeExtension(inputFile, null);
            if (Path.GetExtension(outputFileName).CompareTo(".etl") == 0)
            {
                // In case extension was .etl.zip, remove both
                outputFileName = Path.ChangeExtension(outputFileName, null);
            }
            return outputFileName;
        }

        /// <summary>
        /// Gets the TraceEvents list of events from etlFile, applying a process filter if the /process argument is given. 
        /// </summary>
        private TraceEvents GetTraceEventsWithProcessFilter(ETLDataFile etlFile)
        {
            // If the user asked to focus on one process, do so.  
            TraceEvents events;
            if (CommandLineArgs.Process != null)
            {
                var process = etlFile.Processes.LastProcessWithName(CommandLineArgs.Process);
                if (process == null)
                    throw new ApplicationException("Could not find process named " + CommandLineArgs.Process);
                events = process.EventsInProcess;
            }
            else
                events = etlFile.TraceLog.Events;           // All events in the process.
            return events;
        }

        /// <summary>
        /// Save the CPU stacks for an ETL file into a perfView.xml.zip file.
        /// </summary>
        /// <param name="etlFile">The ETL file to save.</param>
        /// <param name="process">The process to save. If null, save all processes.</param>
        /// <param name="filter">The filter to apply to the stacks. If null, apply no filter.</param>
        /// <param name="outputName">The name of the file to output data to. If null, use the default.</param>
        private static void SaveCPUStacksForProcess(ETLDataFile etlFile, TraceProcess process = null, FilterParams filter = null, string outputName = null)
        {
            // Focus on a particular process if the user asked for it via command line args.
            if (process != null)
            {
                etlFile.SetFilterProcess(process);
            }

            if (filter == null)
                filter = new FilterParams();

            var stacks = etlFile.CPUStacks();
            stacks.Filter = filter;
            stacks.LookupWarmSymbols(10);           // Look up symbols (even on the symbol server) for modules with more than 50 inclusive samples
            stacks.GuiState.Notes = string.Format("Created by SaveCPUStacks from {0} on {1}", etlFile.FilePath, DateTime.Now);

            // Derive the output file name from the input file name.  
            var stackSourceFileName = PerfViewFile.ChangeExtension(etlFile.FilePath, ".perfView.xml.zip");
            stacks.SaveAsXml(outputName ?? stackSourceFileName);
            LogFile.WriteLine("[Saved {0} to {1}]", etlFile.FilePath, stackSourceFileName);
        }

        /// <summary>
        /// The configuration data for an ETL file dumped by SaveCPUStacksForProcess.
        /// </summary>
        private class ScenarioConfig
        {
            /// <summary>
            /// The file to read in.
            /// </summary>
            public readonly string InputFile;

            /// <summary>
            /// The name of the process of interest for the scenario.
            /// 
            /// If null, use heuristic detection.
            /// </summary>
            public readonly string ProcessFilter;
            /// <summary>
            /// The relative time to start taking samples from the ETL file.
            /// 
            /// Set to double.NegativeInfinity to take samples from the beginning.
            /// </summary>
            public readonly double StartTime;
            /// <summary>
            /// The time to stop taking samples from the ETL file.
            /// 
            /// Set to double.PositiveInfinity to take samples until the end.
            /// </summary>
            public readonly double EndTime;


            public ScenarioConfig(string inputFile, string processFilter, double startTime, double endTime)
            {
                ProcessFilter = processFilter;
                StartTime = startTime;
                EndTime = endTime;
                InputFile = inputFile;
            }

            public ScenarioConfig(string inputFile) : this(inputFile, null, double.NegativeInfinity, double.PositiveInfinity) { }
        }

        /// <summary>
        /// Parse a scenario config XML file, creating a mapping of filenames to file configurations.
        /// </summary>
        /// <param name="reader">The XmlReader to read config data from.</param>
        /// <param name="output">The XmlWriter to write a corresponding scenario-set definition to.</param>
        /// <param name="log">The log to write progress to.</param>
        /// <param name="baseDir">The base directory for relative path lookups.</param>
        /// <returns>A Dictionary mapping output filenames to ScenarioConfig objects holding configuration information.</returns>
        /// <remarks>
        /// Example config file:
        /// <ScenarioConfig>
        /// <Scenarios files="*.etl" />
        /// <Scenarios files="foo.etl.zip" name="scenario $1" process="bar" start="500" end="1000" />
        /// </ScenarioConfig>
        /// 
        /// Attributes on Scenarios element:
        /// - files (required)
        ///   The wildcard file pattern of ETL/ETL.ZIP files to include.
        /// - name (optional)
        ///   A pattern by which to name these scenarios. Passed through to scenario-set definition.
        /// - process (optional)
        ///   The name of the process of interest for this trace. If unset, the process of interest will be auto detected
        /// - start, end (optional)
        ///   The start and end times of the region of interest in the trace.
        /// - output (optional)
        ///   Specify name of output perfview.xml.zip.  This is needed to allow outputting different scenarios with the same etl file name to different file. 
        /// </remarks>
        private Dictionary<string, ScenarioConfig> DeserializeScenarioConfig(XmlReader reader, XmlWriter output, TextWriter log, string baseDir)
        {
            var config = new Dictionary<string, ScenarioConfig>();

            if (!reader.ReadToDescendant("ScenarioConfig"))
                throw new ApplicationException("The file does not have a Scenario element");

            if (!reader.ReadToDescendant("Scenarios"))
                throw new ApplicationException("No scenarios specified");

            output.WriteStartDocument();
            output.WriteStartElement("ScenarioSet");

            var defaultRegex = new Regex(@"(.*)", RegexOptions.IgnoreCase);

            do
            {
                string filePattern = reader["files"];
                string namePattern = reader["name"];
                string processPattern = reader["process"];
                string startTimeText = reader["start"];
                string endTimeText = reader["end"];
                string outputPattern = reader["output"];

                if (filePattern == null)
                    throw new ApplicationException("File pattern is required.");

                if (!filePattern.EndsWith(".etl") && !filePattern.EndsWith(".etl.zip"))
                    throw new ApplicationException("Files must be ETL files.");

                string replacePattern = Regex.Escape(filePattern)
                                        .Replace(@"\*", @"([^\\]*)")
                                        .Replace(@"\?", @"[^\\]");

                double startTime =
                    (startTimeText != null) ?
                        double.Parse(startTimeText) :
                        double.NegativeInfinity;

                double endTime =
                    (endTimeText != null) ?
                        double.Parse(endTimeText) :
                        double.PositiveInfinity;

                string pattern = Path.GetFileName(filePattern);
                string dir = Path.GetDirectoryName(filePattern);

                // Tack on the base directory if we're not already an absolute path.
                if (!Path.IsPathRooted(dir))
                    dir = Path.Combine(baseDir, dir);

                var replaceRegex = new Regex(replacePattern, RegexOptions.IgnoreCase);

                foreach (string file in Directory.GetFiles(dir, pattern))
                {
                    string process = null;
                    string outputName = null;
                    if (processPattern != null)
                    {
                        var match = replaceRegex.Match(file);

                        // We won't have a group to match if there were no wildcards in the pattern.
                        if (match.Groups.Count < 1)
                            match = defaultRegex.Match(PerfViewFile.GetFileNameWithoutExtension(file));

                        process = match.Result(processPattern);
                    }

                    if (outputPattern != null)
                    {
                        var match = replaceRegex.Match(file);

                        // We won't have a group to match if there were no wildcards in the pattern.
                        if (match.Groups.Count < 1)
                            match = defaultRegex.Match(PerfViewFile.GetFileNameWithoutExtension(file));

                        outputName = Path.Combine(Path.GetDirectoryName(file), match.Result(outputPattern));
                    }
                    else
                    {
                        outputName = PerfViewFile.ChangeExtension(file, "");
                    }

                    config[outputName + ".perfView.xml.zip"] = new ScenarioConfig(file, process, startTime, endTime);

                    log.WriteLine("Added {0}", file);
                }

                string outputWildcard;
                if (outputPattern != null)
                    outputWildcard = Regex.Replace(outputPattern, @"\$([0-9]+|[&`'+_$])", m => (m.Value == "$$" ? "$" : "*"));
                else
                    outputWildcard = PerfViewFile.ChangeExtension(filePattern, "");

                // Dump out scenario set data from the config input.
                output.WriteStartElement("Scenarios");
                output.WriteAttributeString("files", outputWildcard + ".perfView.xml.zip");
                if (namePattern != null)
                {
                    output.WriteAttributeString("namePattern", namePattern);
                }
                output.WriteEndElement();
            }
            while (reader.ReadToNextSibling("Scenarios"));

            output.WriteEndElement();
            output.WriteEndDocument();

            return config;
        }

        /// <summary>
        /// Heuristically find the process of interest for a given ETL trace.
        /// </summary>
        /// <param name="etlFile">The ETL file to search.</param>
        /// <returns>The "most interesting" process in the trace.</returns>
        private TraceProcess FindProcessOfInterest(ETLDataFile etlFile)
        {
            // THE HEURISTIC:
            // - First Win8 Store app to start.
            // *else*
            // - First app to start that runs for more than half the trace length.
            // *else*
            // - First app to start after the trace starts.

            double threshold = etlFile.TraceLog.SessionDuration.TotalMilliseconds * 0.5;

            // Find first Win8 store app if available.
            var isWin8StoreApp = etlFile.Processes.FirstOrDefault(IsStoreApp);

            if (isWin8StoreApp != null)
            {
                LogFile.WriteLine("Found Win 8 store app {0}", isWin8StoreApp.Name);
                return isWin8StoreApp;
            }


            // Find processes that started after tracing began.
            // This will exclude system processes, services, and many test harnesses.
            var startedInTrace = etlFile.Processes.Where(x => x.StartTimeRelativeMsec > 0.0);

            var retval = startedInTrace.FirstOrDefault(
                x => x.EndTimeRelativeMsec - x.StartTimeRelativeMsec >= threshold
            );

            if (retval != null)
            {
                LogFile.WriteLine("Found over-threshold app {0}", retval.Name);
            }
            else
            {
                retval = null;
                LogFile.WriteLine("Process of interest could not be determined automatically.");
            }
            return retval;
        }

        private static bool IsStoreApp(TraceProcess proc)
        {
            if (proc.StartTimeRelativeMsec == 0.0) return false;

            var startEvent = proc.EventsInProcess.ByEventType<ProcessTraceData>().First();

            return (startEvent.Flags & ProcessFlags.PackageFullName) != 0;
        }
        #endregion
    }
}

// TODO FIX NOW decide where to put these.
public static class TraceEventStackSourceExtensions
{
    public static StackSource CPUStacks(this TraceLog eventLog, TraceProcess process = null, bool showUnknownAddresses = false, Predicate<TraceEvent> predicate = null)
    {
        TraceEvents events;
        if (process == null)
            events = eventLog.Events.Filter((x) => ((predicate == null) || predicate(x)) && x is SampledProfileTraceData && x.ProcessID != 0);
        else
            events = process.EventsInProcess.Filter((x) => ((predicate == null) || predicate(x)) && x is SampledProfileTraceData);

        var traceStackSource = new TraceEventStackSource(events);
        traceStackSource.ShowUnknownAddresses = showUnknownAddresses;
        // We clone the samples so that we don't have to go back to the ETL file from here on.  
        return CopyStackSource.Clone(traceStackSource);
    }
    public static MutableTraceEventStackSource ThreadTimeStacks(this TraceLog eventLog, TraceProcess process = null, bool showUnknownAddresses = false)
    {
        var stackSource = new MutableTraceEventStackSource(eventLog);
        stackSource.ShowUnknownAddresses = App.CommandLineArgs.ShowUnknownAddresses;

        var computer = new ThreadTimeStackComputer(eventLog, App.GetSymbolReader(eventLog.FilePath));
        computer.ExcludeReadyThread = true;
        computer.GenerateThreadTimeStacks(stackSource);

        return stackSource;
    }
    public static MutableTraceEventStackSource ThreadTimeWithReadyThreadStacks(this TraceLog eventLog, TraceProcess process = null, bool showUnknownAddresses = false)
    {
        var stackSource = new MutableTraceEventStackSource(eventLog);
        stackSource.ShowUnknownAddresses = App.CommandLineArgs.ShowUnknownAddresses;

        var computer = new ThreadTimeStackComputer(eventLog, App.GetSymbolReader(eventLog.FilePath));
        computer.GenerateThreadTimeStacks(stackSource);

        return stackSource;
    }
    public static MutableTraceEventStackSource ThreadTimeWithTasksStacks(this TraceLog eventLog, TraceProcess process = null, bool showUnknownAddresses = false)
    {
        // Use MutableTraceEventStackSource to disable activity tracing support
        var stackSource = new MutableTraceEventStackSource(eventLog);
        stackSource.ShowUnknownAddresses = App.CommandLineArgs.ShowUnknownAddresses;
        var computer = new ThreadTimeStackComputer(eventLog, App.GetSymbolReader(eventLog.FilePath));
        computer.UseTasks = true;
        computer.ExcludeReadyThread = true;
        computer.GenerateThreadTimeStacks(stackSource);

        return stackSource;
    }
    public static MutableTraceEventStackSource ThreadTimeWithTasksAspNetStacks(this TraceLog eventLog, TraceProcess process = null, bool showUnknownAddresses = false)
    {
        var stackSource = new MutableTraceEventStackSource(eventLog);
        stackSource.ShowUnknownAddresses = App.CommandLineArgs.ShowUnknownAddresses;

        var computer = new ThreadTimeStackComputer(eventLog, App.GetSymbolReader(eventLog.FilePath));
        computer.UseTasks = true;
        computer.GroupByAspNetRequest = true;
        computer.ExcludeReadyThread = true;
        computer.GenerateThreadTimeStacks(stackSource);

        return stackSource;
    }
    public static MutableTraceEventStackSource ThreadTimeAspNetStacks(this TraceLog eventLog, TraceProcess process = null, bool showUnknownAddresses = false)
    {
        var stackSource = new MutableTraceEventStackSource(eventLog);
        stackSource.ShowUnknownAddresses = App.CommandLineArgs.ShowUnknownAddresses;

        var computer = new ThreadTimeStackComputer(eventLog, App.GetSymbolReader(eventLog.FilePath));
        computer.ExcludeReadyThread = true;
        computer.GroupByAspNetRequest = true;
        computer.GenerateThreadTimeStacks(stackSource);

        return stackSource;
    }
}
