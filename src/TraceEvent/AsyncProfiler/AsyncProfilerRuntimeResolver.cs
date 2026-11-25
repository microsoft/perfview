// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

using RuntimeNames = Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerRuntimeNames;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Resolves runtime support and CoreLib ownership from modules recorded in the trace.
    /// Caches the results and never inspects runtime binaries on this machine.
    /// Unknown or conflicting runtime information uses common support.
    /// </summary>
    internal sealed class AsyncProfilerRuntimeResolver
    {
        public AsyncProfilerRuntimeResolver(TraceModuleFiles moduleFiles)
        {
            m_moduleFiles = moduleFiles ?? throw new ArgumentNullException(nameof(moduleFiles));
        }

        /// <summary>
        /// Resolves support for a captured CoreLib module or a native image linked to it.
        /// Other modules use common support.
        /// </summary>
        public AsyncProfilerRuntimeSupport ResolveModuleSupport(ModuleFileIndex moduleIndex)
        {
            if (m_supports.TryGetValue(moduleIndex, out AsyncProfilerRuntimeSupport support))
            {
                return support;
            }

            TraceModuleFile module = m_moduleFiles[moduleIndex];
            if (!IsHostModule(moduleIndex))
            {
                m_supports.Add(moduleIndex, AsyncProfilerRuntimeSupport.Common);
                return AsyncProfilerRuntimeSupport.Common;
            }

            module = module.ManagedModule ?? module;
            ModuleFileIndex coreLibIndex = module.ModuleFileIndex;
            if (!m_supports.TryGetValue(coreLibIndex, out support))
            {
                if (m_runtimeFlavors == null)
                {
                    m_runtimeFlavors = BuildRuntimeFlavors();
                }

                m_runtimeFlavors.TryGetValue(coreLibIndex, out RuntimeSku flavor);
                support = AsyncProfilerRuntimeSupport.Select(module.FileVersion, module.ProductVersion, flavor);
                m_supports.Add(coreLibIndex, support);
            }

            if (moduleIndex != coreLibIndex)
            {
                m_supports.Add(moduleIndex, support);
            }

            return support;
        }

        /// <summary>
        /// Resolves support shared by the process's captured CoreLib modules.
        /// Uses common support if none are found or their selections disagree.
        /// </summary>
        public AsyncProfilerRuntimeSupport ResolveProcessSupport(ProcessIndex processIndex)
        {
            if (processIndex == ProcessIndex.Invalid)
            {
                return AsyncProfilerRuntimeSupport.Common;
            }

            if (m_processSupports.TryGetValue(processIndex, out AsyncProfilerRuntimeSupport support))
            {
                return support;
            }

            foreach (TraceLoadedModule loaded in m_moduleFiles.Log.Processes[processIndex].LoadedModules)
            {
                if (!IsHostModule(loaded.ModuleFile.ModuleFileIndex))
                {
                    continue;
                }

                AsyncProfilerRuntimeSupport moduleSupport = ResolveModuleSupport(loaded.ModuleFile.ModuleFileIndex);
                if (support != null && !ReferenceEquals(support, moduleSupport))
                {
                    support = AsyncProfilerRuntimeSupport.Common;
                    break;
                }

                support = moduleSupport;
            }

            support = support ?? AsyncProfilerRuntimeSupport.Common;
            m_processSupports.Add(processIndex, support);
            return support;
        }

        /// <summary>Checks whether the module has a captured CoreLib owner.</summary>
        public bool IsHostModule(ModuleFileIndex moduleIndex) =>
            GetHostModuleOwner(moduleIndex) != ModuleFileIndex.Invalid;

        /// <summary>
        /// Returns the CoreLib owner, or Invalid if the module is not CoreLib.
        /// Linked native/IL modules and duplicate entries with matching paths and PDB identities share an owner.
        /// </summary>
        public ModuleFileIndex GetHostModuleOwner(ModuleFileIndex moduleIndex)
        {
            if (moduleIndex == ModuleFileIndex.Invalid)
            {
                return ModuleFileIndex.Invalid;
            }

            if (m_hostModuleOwners == null || m_hostModuleOwners.Length != m_moduleFiles.Count)
            {
                m_hostModuleOwners = BuildHostModuleOwners();
            }

            return (uint)moduleIndex < (uint)m_hostModuleOwners.Length
                ? m_hostModuleOwners[(int)moduleIndex]
                : ModuleFileIndex.Invalid;
        }

        /// <summary>Recognizes CoreLib by the captured name of the module or its linked IL module.</summary>
        public static bool IsHostModule(TraceModuleFile module)
        {
            if (module == null)
            {
                return false;
            }

            if (string.Equals(
                module.Name, AsyncProfilerRuntimeSupport.HostModuleName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            TraceModuleFile managedModule = module.ManagedModule;
            return managedModule != null && string.Equals(
                managedModule.Name, AsyncProfilerRuntimeSupport.HostModuleName, StringComparison.OrdinalIgnoreCase);
        }

        #region private

        private ModuleFileIndex[] BuildHostModuleOwners()
        {
            var owners = new ModuleFileIndex[m_moduleFiles.Count];
            var identities = new Dictionary<(string Path, Guid Signature, int Age), ModuleFileIndex>();
            foreach (TraceModuleFile captured in m_moduleFiles)
            {
                ModuleFileIndex owner = ModuleFileIndex.Invalid;
                if (IsHostModule(captured))
                {
                    TraceModuleFile module = captured.ManagedModule ?? captured;
                    owner = module.ModuleFileIndex;
                    // Missing paths or PDB identities cannot prove that duplicate entries are the same module.
                    if (module.PdbSignature != Guid.Empty && !string.IsNullOrEmpty(module.FilePath))
                    {
                        var identity = (module.FilePath, module.PdbSignature, module.PdbAge);
                        if (identities.TryGetValue(identity, out ModuleFileIndex existing))
                        {
                            owner = existing;
                        }
                        else
                        {
                            identities.Add(identity, owner);
                        }
                    }
                }
                owners[(int)captured.ModuleFileIndex] = owner;
            }

            return owners;
        }

        private Dictionary<ModuleFileIndex, RuntimeSku> BuildRuntimeFlavors()
        {
            var result = new Dictionary<ModuleFileIndex, RuntimeSku>();
            foreach (TraceProcess process in m_moduleFiles.Log.Processes)
            {
                RuntimeSku flavor = RuntimeSku.None;
                foreach (TraceLoadedModule loaded in process.LoadedModules)
                {
                    flavor |= GetEngineFlavor(loaded.Name);
                }

                bool isSingleFlavor =
                    flavor == RuntimeSku.CoreClr ||
                    flavor == RuntimeSku.Mono ||
                    flavor == RuntimeSku.DesktopClr;
                if (!isSingleFlavor)
                {
                    flavor = RuntimeSku.None;
                }

                foreach (TraceLoadedModule loaded in process.LoadedModules)
                {
                    TraceModuleFile module = loaded.ModuleFile;
                    if (!IsHostModule(module.ModuleFileIndex))
                    {
                        continue;
                    }

                    ModuleFileIndex index = (module.ManagedModule ?? module).ModuleFileIndex;
                    if (result.TryGetValue(index, out RuntimeSku existing) && existing != flavor)
                    {
                        // Keep the flavor unknown if processes disagree about this CoreLib module.
                        result[index] = RuntimeSku.None;
                    }
                    else
                    {
                        result[index] = flavor;
                    }
                }
            }

            return result;
        }

        private static RuntimeSku GetEngineFlavor(string moduleName)
        {
            // TraceModuleFile.Name strips Windows extensions but retains Unix shared-library extensions.
            if (moduleName.EndsWith(RuntimeNames.NativeModules.LinuxExtension, StringComparison.OrdinalIgnoreCase) ||
                moduleName.EndsWith(RuntimeNames.NativeModules.MacExtension, StringComparison.OrdinalIgnoreCase))
            {
                moduleName = moduleName.Substring(0, moduleName.LastIndexOf('.'));
            }

            if (string.Equals(moduleName, RuntimeNames.NativeModules.CoreClr, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(moduleName, RuntimeNames.NativeModules.UnixCoreClr, StringComparison.OrdinalIgnoreCase))
            {
                return RuntimeSku.CoreClr;
            }

            if (string.Equals(moduleName, RuntimeNames.NativeModules.MonoSGen, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(moduleName, RuntimeNames.NativeModules.UnixMonoSGen, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(moduleName, RuntimeNames.NativeModules.UnixMonoBoehm, StringComparison.OrdinalIgnoreCase))
            {
                return RuntimeSku.Mono;
            }

            if (string.Equals(moduleName, RuntimeNames.NativeModules.DesktopClr, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(moduleName, RuntimeNames.NativeModules.LegacyDesktopClr, StringComparison.OrdinalIgnoreCase))
            {
                return RuntimeSku.DesktopClr;
            }

            return RuntimeSku.None;
        }

        private readonly TraceModuleFiles m_moduleFiles;
        private readonly Dictionary<ModuleFileIndex, AsyncProfilerRuntimeSupport> m_supports =
            new Dictionary<ModuleFileIndex, AsyncProfilerRuntimeSupport>();
        private readonly Dictionary<ProcessIndex, AsyncProfilerRuntimeSupport> m_processSupports =
            new Dictionary<ProcessIndex, AsyncProfilerRuntimeSupport>();
        private Dictionary<ModuleFileIndex, RuntimeSku> m_runtimeFlavors;
        private ModuleFileIndex[] m_hostModuleOwners;

        #endregion
    }
}
