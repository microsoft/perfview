using Microsoft.Samples.Debugging.CorMetadata.NativeApi;
using Profiler;
using System;
using System.IO;

#if false 
    // TODO FIX NOW experimental
 
    /// <summary>
    /// A simple wrapper that lets you resolve tokens to names.  
    /// </summary>
    class MetaDataReader
    {
        MetaDataReader(string moduleName)
        {
            CLRMetaHost mh = new CLRMetaHost();
            CLRRuntimeInfo highestInstalledRuntime = null;
            foreach (CLRRuntimeInfo runtime in mh.EnumerateInstalledRuntimes())
            {
                if (highestInstalledRuntime == null ||
                    string.Compare(highestInstalledRuntime.GetVersionString(), runtime.GetVersionString(), StringComparison.OrdinalIgnoreCase) < 0)
                    highestInstalledRuntime = runtime;
            }
            if (highestInstalledRuntime == null)
                throw new ApplicationException("Could not enumerate .NET runtimes on the system.");

            IMetaDataDispenser metaDataDispenser = highestInstalledRuntime.GetIMetaDataDispenser();

            Guid IMetaDataImport2_Guid = Guid("7DAC8207-D3AE-4c75-9B67-92801A497D44");
            object metaDataImportObj;
            metaDataDispenser.OpenScope(moduleName, 0, ref IMetaDataImport2_Guid, out metaDataImportObj);
            m_metaDataImport = metaDataImportObj as IMetadataImport2;
        }

        IMetadataImport2 m_metaDataImport;
    }

#endif 