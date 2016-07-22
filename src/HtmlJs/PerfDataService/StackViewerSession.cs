namespace PerfDataService
{
    using System;
    using Diagnostics.Tracing.StackSources;
    using Microsoft.Diagnostics.Symbols;
    using Microsoft.Diagnostics.Tracing.Etlx;
    using TraceDataPlugins;

    public class StackViewerSession
    {
        private ICallTreeDataProvider dataProvider;

        private readonly SymbolReader reader;

        public StackViewerSession(string filename, string stacktype, TraceLog tracelog, FilterParams filterParams, SymbolReader reader)
        {
            if (filename == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(filename));
            }

            if (stacktype == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(stacktype));
            }

            if (tracelog == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(tracelog));
            }

            if (filterParams == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(filterParams));
            }

            if (reader == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(reader));
            }

            this.Filename = filename;
            this.StackType = stacktype;
            this.FilterParams = filterParams;
            this.TraceLog = tracelog;
            this.reader = reader;
            this.Pending = true;
        }

        public TraceLog TraceLog { get; set; }

        public FilterParams FilterParams { get; set; }

        public string Filename { get; set; }

        public string StackType { get; set; }

        public bool Pending { get; set; }

        public void InitializeDataProvider()
        {
            if (string.Equals(this.StackType, "exceptions", StringComparison.OrdinalIgnoreCase))
            {
                this.dataProvider = new CallTreeDataProvider(this.TraceLog, this.FilterParams, this.reader, new ExceptionTraceDataPlugin());
            }

            if (string.Equals(this.StackType, "cpu", StringComparison.OrdinalIgnoreCase))
            {
                this.dataProvider = new CallTreeDataProvider(this.TraceLog, this.FilterParams, this.reader, new SampleProfileTraceDataPlugin());
            }

            if (string.Equals(this.StackType, "memory", StringComparison.OrdinalIgnoreCase))
            {
                this.dataProvider = new CallTreeDataProvider(this.TraceLog, this.FilterParams, this.reader, new HeapAllocationTraceDataPlugin());
            }

            if (string.Equals(this.StackType, "contention", StringComparison.OrdinalIgnoreCase))
            {
                this.dataProvider = new CallTreeDataProvider(this.TraceLog, this.FilterParams, this.reader, new ContentionTraceDataPlugin());
            }
        }

        public ICallTreeDataProvider GetDataProvider()
        {
            return this.dataProvider;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (this.FilterParams != null ? this.FilterParams.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Filename != null ? this.Filename.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.StackType != null ? this.StackType.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((StackViewerSession)obj);
        }

        protected bool Equals(StackViewerSession other)
        {
            return Equals(this.FilterParams, other.FilterParams) && string.Equals(this.Filename, other.Filename) && string.Equals(this.StackType, other.StackType);
        }
    }
}