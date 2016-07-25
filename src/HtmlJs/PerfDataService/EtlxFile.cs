namespace PerfDataService
{
    using Microsoft.Diagnostics.Tracing.Etlx;

    public sealed class EtlxFile
    {
        public EtlxFile(string filename)
        {
            this.FileName = filename;
        }

        public string FileName { get; private set; }

        public bool Pending { get; set; }

        public TraceLog TraceLog { get; set; }
    }
}