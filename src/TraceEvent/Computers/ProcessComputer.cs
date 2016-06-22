// Copyright (c) Microsoft Corporation.  All rights reserved
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.FrameworkEventSource;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Parsers.Symbol;
using Microsoft.Diagnostics.Tracing.Parsers.Tpl;
using Microsoft.Diagnostics.Tracing.Stacks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Tracing
{
    /// <summary>
    /// An ProcessComputer is a state machine that tracks information about Processes.  Large portions
    /// of the code are taken from TraceLog with the goal of long term unification.
    /// 
    /// TODO This implementation is poor at idenitfying the ParentPID, 64bitness, and Start/End times
    /// </summary>
    public class ProcessComputer
    {
        /// <summary>
        /// Gathers relevant details about the processes in the event source
        /// </summary>
        /// <param name="rawEvents"></param>
        /// <param name="log"></param>
        public ProcessComputer(TraceEventDispatcher rawEvents, TraceLog log = null)
        {
            processingDisabled = false;
            sampleProfileInterval100ns = 1;
            processes = new TraceProcesses(log);

            //
            // Code lifted from TraceLog.cs to create the TraceProcess
            //

            // These parsers create state and we want to collect that so we put it on our 'parsers' list that we serialize.  
            var kernelParser = rawEvents.Kernel;

            // Process level events. 
            kernelParser.ProcessStartGroup += delegate (ProcessTraceData data)
            {
                this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC, data.Opcode == TraceEventOpcode.Start).ProcessStart(data);
                // Don't filter them out (not that many, useful for finding command line)
            };

            kernelParser.ProcessEndGroup += delegate (ProcessTraceData data)
            {
                this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC).ProcessEnd(data);
                // Don't filter them out (not that many, useful for finding command line)
            };
            // Thread level events
            kernelParser.ThreadStartGroup += delegate (ThreadTraceData data)
            {
                this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC);
            };
            kernelParser.ThreadEndGroup += delegate (ThreadTraceData data)
            {
                this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC);
            };

            kernelParser.ImageGroup += delegate (ImageLoadTraceData data)
            {
                this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC);
            };

            rawEvents.Clr.LoaderModuleLoad += delegate (ModuleLoadUnloadTraceData data)
            {
                this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC).LoadedModules.ManagedModuleLoadOrUnload(data, true, false);
            };
            rawEvents.Clr.LoaderModuleUnload += delegate (ModuleLoadUnloadTraceData data)
            {
                this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC).LoadedModules.ManagedModuleLoadOrUnload(data, false, false);
            };
            rawEvents.Clr.LoaderModuleDCStopV2 += delegate (ModuleLoadUnloadTraceData data)
            {
                this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC).LoadedModules.ManagedModuleLoadOrUnload(data, false, true);
            };

            var ClrRundownParser = new ClrRundownTraceEventParser(rawEvents);
            Action<ModuleLoadUnloadTraceData> onLoaderRundown = delegate (ModuleLoadUnloadTraceData data)
            {
                this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC).LoadedModules.ManagedModuleLoadOrUnload(data, false, true);
            };

            ClrRundownParser.LoaderModuleDCStop += onLoaderRundown;
            ClrRundownParser.LoaderModuleDCStart += onLoaderRundown;

            Action<MethodLoadUnloadVerboseTraceData> onMethodStart = delegate (MethodLoadUnloadVerboseTraceData data)
            {
                if (data.IsJitted)
                {
                    TraceProcess process = this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC);
                }
            };
            rawEvents.Clr.MethodLoadVerbose += onMethodStart;
            rawEvents.Clr.MethodDCStartVerboseV2 += onMethodStart;
            ClrRundownParser.MethodDCStartVerbose += onMethodStart;

            Action<ClrStackWalkTraceData> clrStackWalk = delegate (ClrStackWalkTraceData data)
            {
                // Avoid creating data structures for events we will throw away
                if (processingDisabled)
                    return;

                var process = Processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC);
 
            };
            rawEvents.Clr.ClrStackWalk += clrStackWalk;

            Action<RuntimeInformationTraceData> doAtRuntimeStart = delegate (RuntimeInformationTraceData data)
            {
                TraceProcess process = this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC);

                process.clrRuntimeVersion = new Version(data.VMMajorVersion, data.VMMinorVersion, data.VMBuildNumber, data.VMQfeNumber);
                process.ClrStartupFlags = data.StartupFlags;
                // proxy for bitness, given we don't have a traceevent to pass through
                process.loadedAModuleHigh = (data.RuntimeDllPath.ToLower().Contains("framework64"));

                if (process.commandLine.Length == 0)
                    process.commandLine = data.CommandLine;
            };
            ClrRundownParser.RuntimeStart += doAtRuntimeStart;
            rawEvents.Clr.RuntimeStart += doAtRuntimeStart;

            var clrPrivate = new ClrPrivateTraceEventParser(rawEvents);
            clrPrivate.ClrStackWalk += clrStackWalk;

            kernelParser.StackWalkStack += delegate (StackWalkStackTraceData data)
            {
                if (processingDisabled)
                    return;

                var timeStampQPC = data.TimeStampQPC;
                TraceProcess process = this.processes.GetOrCreateProcess(data.ProcessID, timeStampQPC);
            };

            // Attribute CPU samples to processes.
            kernelParser.PerfInfoSample += delegate (SampledProfileTraceData data)
            {
                if (data.ThreadID == 0)    // Don't count process 0 (idle)
                {
                    return;
                }

                var process = Processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC);
                process.cpuSamples++;
            };


            // We assume that the sampling interval is uniform over the trace.   We pick the start if it 
            // is there, otherwise the OLD value of the LAST set interval (since we RESET the interval at the end)
            // OR the OLD value at the end.  
            bool setSeen = false;
            bool startSeen = false;

            kernelParser.PerfInfoCollectionStart += delegate (SampledProfileIntervalTraceData data)
            {
                startSeen = true;
                sampleProfileInterval100ns = data.NewInterval;
            };

            kernelParser.PerfInfoSetInterval += delegate (SampledProfileIntervalTraceData data)
            {
                setSeen = true;
                if (!startSeen)
                    sampleProfileInterval100ns = data.OldInterval;
            };

            kernelParser.PerfInfoSetInterval += delegate (SampledProfileIntervalTraceData data)
            {
                if (!setSeen && !startSeen)
                    sampleProfileInterval100ns = data.OldInterval;
            };
        }

        /// <summary>
        /// All the processes in the event source.  
        /// </summary>
        public TraceProcesses Processes { get { return processes; } }

        #region private
        private TraceProcesses processes;
        private bool processingDisabled;                    // Have we turned off processing because of a MaxCount? 
        private int sampleProfileInterval100ns;
        internal TraceLogOptions options;
        #endregion
    }
}