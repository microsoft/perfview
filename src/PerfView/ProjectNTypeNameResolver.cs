using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Parsers.Symbol;

using Address = System.UInt64;

namespace PerfView
{
    /// <summary>
    /// Maintains the state necessary to resolve GCAllocationTick typenames for Project N.
    /// </summary>
    public sealed class ProjectNTypeNameResolver
    {
        /// <summary>
        /// The map that contains module information for each process.
        /// Use ProcessIndex as the index variable.
        /// </summary>
        private static ProcessModuleList[] _ProcessModuleMap;

        /// <summary>
        /// Responsible for translating a module and symbol to a type name.
        /// </summary>
        private TypeNameSymbolResolver _TypeNameResolver;
        
        private TextWriter _Log;
        private string _FilePath;

        public ProjectNTypeNameResolver(TraceLogEventSource eventSource, TextWriter log, string filePath)
        {
            _Log = log;
            _FilePath = filePath;
            _TypeNameResolver = new TypeNameSymbolResolver(_FilePath, _Log);
            _ProcessModuleMap = new ProcessModuleList[eventSource.TraceLog.Processes.Count];
            RegisterModuleCallbacks(eventSource);
        }

        /// <summary>
        /// Resolve a type name.
        /// </summary>
        public string ResolveTypeName(ProcessIndex processIndex, Address typeID)
        {
            // Get the Module for the allocation.
            ProcessModuleList procModuleList = ModuleListForProcess(processIndex);
            Graphs.Module mod = procModuleList.ModuleForEEType(typeID);

            // Resolve the type name.
            string typeName = null;
            if (mod != null)
            {
                typeName = _TypeNameResolver.ResolveTypeName((int)(typeID - mod.ImageBase), mod);
            }

            // Trim the module from the type name.
            if (!string.IsNullOrEmpty(typeName))
            {
                // Strip off the module name if present.
                string[] typeNameParts = typeName.Split(new char[] { '!' }, 2);
                if (typeNameParts.Length == 2)
                {
                    typeName = typeNameParts[1];
                }
            }

            return typeName;
        }

        /// <summary>
        /// Register TraceEvent callbacks for module events.
        /// </summary>
        /// <param name="eventSource"></param>
        private void RegisterModuleCallbacks(TraceLogEventSource eventSource)
        {
            Dictionary<int, DbgIDRSDSTraceData> _ProcessToDbgDataMap = new Dictionary<int, DbgIDRSDSTraceData>();

            var symbolParser = new SymbolTraceEventParser(eventSource);
            symbolParser.ImageIDDbgID_RSDS += delegate(DbgIDRSDSTraceData data)
            {
                _ProcessToDbgDataMap[data.ProcessID] = (DbgIDRSDSTraceData)data.Clone();
            };

            eventSource.Kernel.ImageGroup += delegate(ImageLoadTraceData data)
            {
                ProcessModuleList procModuleList = ModuleListForProcess(data.Process().ProcessIndex);

                Graphs.Module module = new Graphs.Module(data.ImageBase);
                module.Path = data.FileName;
                module.Size = data.ImageSize;
                module.BuildTime = data.BuildTime;

                DbgIDRSDSTraceData lastDbgData = null;
                _ProcessToDbgDataMap.TryGetValue(data.ProcessID, out lastDbgData);

                if (lastDbgData != null && data.TimeStampRelativeMSec == lastDbgData.TimeStampRelativeMSec)
                {
                    module.PdbGuid = lastDbgData.GuidSig;
                    module.PdbAge = lastDbgData.Age;
                    module.PdbName = lastDbgData.PdbFileName;

                }
                procModuleList.Modules[module.ImageBase] = module;
            };
        }

        private ProcessModuleList ModuleListForProcess(ProcessIndex processIndex)
        {
            ProcessModuleList moduleList = _ProcessModuleMap[(int)processIndex];
            if (moduleList == null)
            {
                moduleList = _ProcessModuleMap[(int)processIndex] = new ProcessModuleList();
            }

            return moduleList;
        }
    }

    /// <summary>
    /// Represents a map of modules per process.  Used to store and lookup module and PDB information for each process.
    /// </summary>
    internal sealed class ProcessModuleList
    {
        private Dictionary<Address, Graphs.Module> _Modules = new Dictionary<Address, Graphs.Module>();

        /// <summary>
        /// Exposes the list of modules for the process.
        /// </summary>
        public Dictionary<Address, Graphs.Module> Modules
        {
            get { return _Modules; }
        }

        /// <summary>
        /// Get the module for the input EEType.
        /// </summary>
        /// <param name="eeTypePointer"></param>
        /// <returns></returns>
        public Graphs.Module ModuleForEEType(Address eeTypePointer)
        {
            foreach(Graphs.Module mod in _Modules.Values)
            {
                if ((eeTypePointer >= mod.ImageBase) && (eeTypePointer < (mod.ImageBase + (ulong)mod.Size)))
                {
                    return mod;
                }
            }

            return null;
        }
    }
}