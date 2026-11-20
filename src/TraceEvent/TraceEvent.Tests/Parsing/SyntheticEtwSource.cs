// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace TraceEventTests
{
    /// <summary>
    /// In-memory ETW source for tests: derived classes emit hand-built EVENT_RECORDs from <c>Process()</c>,
    /// and <see cref="ConvertAndVerify"/> round-trips them through an ETLX file.
    /// </summary>
    internal abstract unsafe class SyntheticEtwSource : TraceEventDispatcher
    {
        protected SyntheticEtwSource(long startQpc, int processId)
        {
            m_processId = processId;
            pointerSize = 8;
            numberOfProcessors = 1;
            cpuSpeedMHz = 3000;
            _QPCFreq = 10_000_000;
            _syncTimeQPC = sessionStartTimeQPC = startQpc;
            sessionEndTimeQPC = startQpc + 100;
            _syncTimeUTC = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            osVersion = new Version(10, 0);
        }

        public override int EventsLost => 0;

        public static void ConvertAndVerify(Func<TraceEventDispatcher> createSource, Action<TraceLog> verify)
        {
            string path = Path.Combine(Path.GetTempPath(), "synthetic_etw_" + Guid.NewGuid().ToString("N") + ".etlx");
            try
            {
                using (TraceEventDispatcher source = createSource())
                {
                    TraceLog.CreateFromTraceEventSource(source, path, new TraceLogOptions());
                }
                using (var log = new TraceLog(path))
                {
                    verify(log);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        protected void Emit(Guid provider, ushort id, byte opcode, long qpc, int threadId, bool classic, byte[] payload,
            TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM* extended = null)
        {
            fixed (byte* bytes = payload)
            {
                var record = new TraceEventNativeMethods.EVENT_RECORD();
                record.EventHeader.ProviderId = provider;
                record.EventHeader.Id = id;
                record.EventHeader.Opcode = opcode;
                record.EventHeader.Version = 2;
                record.EventHeader.Flags = TraceEventNativeMethods.EVENT_HEADER_FLAG_64_BIT_HEADER;
                if (classic) { record.EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_CLASSIC_HEADER; }
                record.EventHeader.ProcessId = m_processId;
                record.EventHeader.ThreadId = threadId;
                record.EventHeader.TimeStamp = qpc;
                record.UserData = (IntPtr)bytes;
                record.UserDataLength = (ushort)payload.Length;
                record.ExtendedData = extended;
                record.ExtendedDataCount = (ushort)(extended == null ? 0 : 1);
                Dispatch(Lookup(&record));
            }
        }

        protected static byte[] Payload(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream)) { write(writer); }
                return stream.ToArray();
            }
        }

        private readonly int m_processId;
    }
}
