#nullable disable
namespace Microsoft.Diagnostics.HeapDump
{
    using System.Collections.Generic;

    /// <summary>
    /// The target collection source
    /// </summary>
    public enum TargetSource { LiveProcess, MiniDumpFile };

    /// <summary>
    /// Class that holds metadata about a data collection
    /// </summary>
    public class CollectionMetadata
    {
        public TargetSource Source { get; internal set; }

        public bool Is64BitSource { get; internal set; }

        public IList<string> ConfigurationDirectories { get; internal set; }
    }
}
