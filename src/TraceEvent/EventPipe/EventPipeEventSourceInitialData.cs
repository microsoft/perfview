using System;
using System.IO;
using FastSerialization;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    internal class EventPipeEventSourceInitialData
    {
        internal Deserializer Deserializer { get; }
        internal string ProcessName { get; }
        internal int Version { get; }
        internal int ReaderVersion { get; }

        public DateTime CreationTime { get; }
        public long StartTimeStamp { get; }
        public long ClockFrequency { get; }

        internal int PointerSize { get; }
        internal int NumberOfProcessors { get; }

        internal StreamLabel EndOfStream { get; }

        internal EventPipeEventSourceInitialData(string processName, int version, int readerVersion,
            DateTime creationTime, long startTimeStamp, long clockFrequency,
            int pointerSize, int numberOfProcessors,
            StreamLabel endOfStream)
        {
            ProcessName = processName;
            Version = version;
            ReaderVersion = readerVersion;
            PointerSize = pointerSize;
            NumberOfProcessors = numberOfProcessors;
            CreationTime = creationTime;
            StartTimeStamp = startTimeStamp;
            ClockFrequency = clockFrequency;
            EndOfStream = endOfStream;
        }

        /// <summary>
        /// currently no info about OsVersion, returns "0.0.0.0"
        /// </summary>
        internal Version OsVersion => new Version("0.0.0.0");

        /// <summary>
        /// currently no info about CpuSpeedMHz, returns 10
        /// </summary>
        internal int CpuSpeedMHz => 10;

        /// <summary>
        /// currently no info about ProcessId, returns 0
        /// </summary>
        internal int ProcessId => 0;
    }
}
