using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;

namespace PerfView
{
    /// <summary>
    /// Monitors for Runtime/Start events from the Microsoft-Windows-DotNETRuntime and Microsoft-Windows-DotNETRuntimeRundown providers
    /// and logs the version information for the associated runtime DLLs.
    /// </summary>
    internal static class DotNetVersionLogger
    {
        private static VersionLogger _loggerInstance;

        private sealed class VersionLogger : IDisposable
        {
            internal const string SessionName = "PerfView-DotNetVersionLogger-Session";
            private readonly static TextWriter Log = App.CommandProcessor.LogFile;
            private TraceEventSession _session;
            private AutoResetEvent _sessionStopEvent = new AutoResetEvent(false);
            private HashSet<string> _loggedPaths = new HashSet<string>();

            public void Dispose()
            {
                if (_session != null)
                {
                    _session.Dispose();
                    _session = null;
                }
            }

            public void Start()
            {
                try
                {
                    _session = new TraceEventSession(SessionName);
                    _session.EnableProvider(
                        ClrTraceEventParser.ProviderGuid,
                        TraceEventLevel.Always,
                        0x8000000000000000UL,
                        new TraceEventProviderOptions() { EventIDsToEnable = new List<int> { 187 } });

                    _session.Source.Clr.RuntimeStart += OnRuntimeInformationStartEvent;
                    ClrRundownTraceEventParser rundownParser = new ClrRundownTraceEventParser(_session.Source);
                    rundownParser.RuntimeStart += OnRuntimeInformationStartEvent;

                    Task.Factory.StartNew(() =>
                    {
                        _session.Source.Process();
                        _sessionStopEvent.Set();
                    });
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"Failed to start dotnet version tracking: {ex}");
                }
            }

            public void StartRundown()
            {
                try
                {
                    _session.EnableProvider(
                        ClrRundownTraceEventParser.ProviderGuid,
                        TraceEventLevel.Always,
                        0x8000000000000000UL,
                        new TraceEventProviderOptions() { EventIDsToEnable = new List<int> { 187 } });
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"Failed to enable rundown provider: {ex}");
                }
            }

            public void Stop()
            {
                if (_session != null)
                {
                    _session.Dispose();
                    _session = null;

                    // Wait for the session to stop.
                    _sessionStopEvent.WaitOne();

                    try
                    {
                        foreach (string dllPath in _loggedPaths)
                        {
                            if (File.Exists(dllPath))
                            {
                                FileVersionInfo info = FileVersionInfo.GetVersionInfo(dllPath);
                                PerfViewLogger.Log.RuntimeVersion(dllPath, info.FileVersion);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine($"Failed to enumerate .NET runtime version information: {ex}");
                    }
                }
            }

            private void OnRuntimeInformationStartEvent(RuntimeInformationTraceData data)
            {
                _loggedPaths.Add(data.RuntimeDllPath);
            }
        }

        public static bool Running
        {
            get { return _loggerInstance != null; }
        }

        public static void Start()
        {
            if (App.CommandLineArgs.DisableDotNetVersionLogging)
            {
                return;
            }

            Stop();

            _loggerInstance = new VersionLogger();
            _loggerInstance.Start();
        }

        public static void StartRundown()
        {
            if (_loggerInstance != null)
            {
                _loggerInstance.StartRundown();
            }
        }

        public static void Stop()
        {
            if (App.CommandLineArgs.DisableDotNetVersionLogging)
            {
                return;
            }

            if (_loggerInstance != null)
            {
                _loggerInstance.Stop();
                _loggerInstance.Dispose();
            }
        }

        public static void Abort()
        {

            try
            {
                using (TraceEventSession session = new TraceEventSession(
                    VersionLogger.SessionName,
                    TraceEventSessionOptions.Attach))
                {
                    session.Stop(true);
                }
            }
            catch (Exception) { }
        }
    }
}