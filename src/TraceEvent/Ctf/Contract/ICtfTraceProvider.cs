using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.Ctf.Contract
{
    public delegate void NewMetadataEventHandler(ICtfMetadata metadata);
    public delegate void NewEventTracesEventHandler(IEnumerable<ICtfEventTrace> metadata);

    public interface ICtfTraceProvider : IDisposable
    {
        event NewMetadataEventHandler NewCtfMetadata;
        event NewEventTracesEventHandler NewCtfEventTraces;

        int PointerSize { get; }
        int ProcessorCount { get; }

        void Process();
        void StopProcessing();

    }
}
