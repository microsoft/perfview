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
using Microsoft.Diagnostics.Tracing.Parsers.ClrPrivate;
using Microsoft.Diagnostics.Tracing.Parsers.FrameworkEventSource;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Parsers.Symbol;
using Microsoft.Diagnostics.Tracing.Parsers.Tpl;
using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.Diagnostics.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Tracing.Analysis
{
    /// <summary>
    /// Extension methods to enable TraceManagedProcess
    /// </summary>
    public static class TraceManagedSamplesExtensions
    {
        public static void NeedManagedSamples(this TraceEventDispatcher source, Microsoft.Diagnostics.Tracing.Etlx.TraceLog tracelog)
        {
            // ensure there are base processes
            source.NeedProcesses();
            source.NeedManagedProcesses();

            if (!source.UserData.ContainsKey("Computers/ManagedSamples"))
            {
                TraceProcesses processes = source.Processes();

                SetupCallbacks(source, tracelog);
                source.UserData["Computers/ManagedSamples"] = processes;
            }
        }

        #region private
        private static void SetupCallbacks(TraceEventDispatcher source, Microsoft.Diagnostics.Tracing.Etlx.TraceLog tracelog)
        {
            source.Kernel.PerfInfoSample += delegate (SampledProfileTraceData data)
            {
                var stats = data.Process();
                var mang = stats.AsManagedProcess();
                if (mang != null)
                {
                    mang.JIT.Stats.TotalCpuTimeMSec += 1;
                    Tuple<string/*module*/,string/*method*/> name = GetSampledMethodName(tracelog, mang.JIT, data);

                    if (name == null) return;

                    MethodInformation meth = null;
                    var methods = mang.JIT.Methods.Where(m => m.ModuleILPath.Contains(name.Item1) && m.MethodName.Equals(name.Item2));
                    if (methods.Count() > 0)
                    {
                        Debug.Assert(methods.Count() == 1);
                        meth = methods.First();
                    }
                    else
                    {
                        meth = new MethodInformation( stats.ProcessIndex);
                        meth.ModuleILPath = name.Item1;
                        meth.MethodName = name.Item2;
                        meth.ILSize = meth.NativeSize = -1;
                        meth.CompileCpuTimeMSec = -1;
                        meth.StartTimeMSec = -1;
                        mang.JIT.Methods.Add(meth);
                    }
                    meth.RunCpuTimeMSec++;
                }
            };
        }

        private static Tuple<string/*module*/,string/*method*/> GetSampledMethodName(Microsoft.Diagnostics.Tracing.Etlx.TraceLog tracelog, JITDetails details, SampledProfileTraceData data)
        {
            TraceCodeAddress ca = details.Stats.GetManagedMethodOnStack(data, tracelog);
            if (ca == null)
            {
                return null;
            }

            if (String.IsNullOrEmpty(ca.ModuleName))
            {
                return null;
            }

            // Lookup symbols, if not already looked up.
            if (!details.Stats.SymbolsLookedUp.Contains(ca.ModuleName))
            {
                try
                {
                    LookupSymbolsForModule(tracelog, data, ca.ModuleName);
                }
                catch (Exception)
                {
                    return null;
                }
                details.Stats.SymbolsLookedUp.Add(ca.ModuleName);
            }

            if (ca.Method == null)
            {
                details.Stats.SymbolsMissing.Add(ca.ModuleName);
                return null;
            }
            return new Tuple<string,string>(ca.ModuleName, ca.Method.FullMethodName);
        }

        public static void LookupSymbolsForModule(Microsoft.Diagnostics.Tracing.Etlx.TraceLog tracelog, SampledProfileTraceData data, string simpleModuleName)
        {
            var symbolReader = GetSymbolReader(tracelog.FilePath);
            var filterProcess = tracelog.Processes.LastProcessWithID(data.ProcessID);

            // Remove any extensions.  
            simpleModuleName = Path.GetFileNameWithoutExtension(simpleModuleName);

            // If we have a process, look the DLL up just there
            var moduleFiles = new Dictionary<int, TraceModuleFile>();
            if (filterProcess != null)
            {
                foreach (var loadedModule in filterProcess.LoadedModules)
                {
                    var baseName = Path.GetFileNameWithoutExtension(loadedModule.Name);
                    if (string.Compare(baseName, simpleModuleName, StringComparison.OrdinalIgnoreCase) == 0)
                        moduleFiles[(int)loadedModule.ModuleFile.ModuleFileIndex] = loadedModule.ModuleFile;
                }
            }

            // We did not find it, try system-wide
            if (moduleFiles.Count == 0)
            {
                foreach (var moduleFile in tracelog.ModuleFiles)
                {
                    var baseName = Path.GetFileNameWithoutExtension(moduleFile.Name);
                    if (string.Compare(baseName, simpleModuleName, StringComparison.OrdinalIgnoreCase) == 0)
                        moduleFiles[(int)moduleFile.ModuleFileIndex] = moduleFile;
                }
            }

            if (moduleFiles.Count == 0)
                throw new ApplicationException("Could not find module " + simpleModuleName + " in trace.");

            foreach (var moduleFile in moduleFiles.Values)
            {
                try
                {
                    tracelog.CodeAddresses.LookupSymbolsForModule(symbolReader, moduleFile);
                }
                catch (ApplicationException)
                {
                }
            }
        }

        public static SymbolReader GetSymbolReader(string etlFilePath = null, SymbolReaderOptions symbolFlags = SymbolReaderOptions.None)
        {
            SymbolPath symPath = new SymbolPath(SymbolPath);
            if ((symbolFlags & SymbolReaderOptions.CacheOnly) != 0)
                symPath = new SymbolPath("SRV*" + symPath.DefaultSymbolCache());

            var sourcePath = SourcePath; // ???
            bool hasLocalSymDir = false;
            if (etlFilePath != null)
            {
                // Add the directory where the file resides and a 'symbols' subdirectory 
                var filePathDir = Path.GetDirectoryName(etlFilePath);
                if (filePathDir.Length != 0)
                {
                    // Then the directory where the .ETL file lives. 
                    symPath.Insert(filePathDir);

                    // The symbols directory has even higher priority (less likely to have a false positive)
                    var localSymDir = Path.Combine(filePathDir, "symbols");
                    if (Directory.Exists(localSymDir))
                    {
                        hasLocalSymDir = true;
                        symPath.Insert(localSymDir);
                        symPath.Insert("SRV*" + localSymDir);
                    }

                    // WPR conventions add any .etl.ngenPDB directory to the path too.   has higher priority still. 
                    var wprSymDir = etlFilePath + ".NGENPDB";
                    if (Directory.Exists(wprSymDir))
                        symPath.Insert("SRV*" + wprSymDir);
                    else
                    {
                        // I have now seen both conventions .etl.ngenpdb and .ngenpdb, so look for both.  
                        wprSymDir = Path.ChangeExtension(etlFilePath, ".NGENPDB");
                        if (Directory.Exists(wprSymDir))
                            symPath.Insert("SRV*" + wprSymDir);
                    }
                    // VS uses .NGENPDBS as a convention.  
                    wprSymDir = etlFilePath + ".NGENPDBS";
                    if (Directory.Exists(wprSymDir))
                        symPath.Insert("SRV*" + wprSymDir);

                    if (!string.IsNullOrWhiteSpace(sourcePath))
                        sourcePath += ";";
                    sourcePath += filePathDir;
                    var srcDir = Path.Combine(filePathDir, "src");
                    if (Directory.Exists(srcDir))
                        sourcePath += ";" + srcDir;
                }
            }

            // Can we use the cached symbol reader?
            if (s_symbolReader != null)
            {
                s_symbolReader.SourcePath = sourcePath;
                if (symbolFlags == SymbolReaderOptions.None && s_symbolReader.SymbolPath == symPath.ToString())
                    return s_symbolReader;

                s_symbolReader.Dispose();
                s_symbolReader = null;
            }

            SymbolReader ret = new SymbolReader(TextWriter.Null, symPath.ToString());
            ret.SourcePath = sourcePath;

            ret.Options = symbolFlags;

            ret.SecurityCheck = (pdbFile => true);
            ret.CacheUnsafeSymbols = hasLocalSymDir;

            if (symbolFlags == SymbolReaderOptions.None)
                s_symbolReader = ret;
            return ret;
        }

        public static string SymbolPath
        {
            get
            {
                if (m_SymbolPath == null)
                {
                    // Start with _NT_SYMBOL_PATH
                    var symPath = new SymbolPath(Microsoft.Diagnostics.Symbols.SymbolPath.SymbolPathFromEnvironment);

                    // If we still don't have anything, add a default one
                    // Since the default goes off machine, if we are outside of Microsoft, we have to ask
                    // the user for permission. 
                    symPath.Add(Microsoft.Diagnostics.Symbols.SymbolPath.MicrosoftSymbolServerPath);

                    // Remember it.  
                    SymbolPath = symPath.InsureHasCache(symPath.DefaultSymbolCache()).CacheFirst().ToString();
                }
                return m_SymbolPath;
            }
            set
            {
                m_SymbolPath = value;
            }
        }
        public static string SourcePath
        {
            get
            {
                if (m_SourcePath == null)
                {
                    var symPath = new SymbolPath(Environment.GetEnvironmentVariable("_NT_SOURCE_PATH"));

                    // Remember it.  
                    SourcePath = symPath.ToString();
                }
                return m_SourcePath;
            }
            set
            {
                m_SourcePath = value;
            }
        }

        private static string m_SymbolPath;
        private static string m_SourcePath;
        private static SymbolReader s_symbolReader;
        #endregion
    }
}