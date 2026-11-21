using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace TraceEventTests.Regression
{
    public class BPerfHeaderTests
    {
        [Fact]
        public void InitialHeaderRequiresCompleteStructure()
        {
            IsolatedTest.Run(() =>
            {
                foreach (int length in new[] { 0, 63, 64, 67, 263 })
                {
                    WithSource(length, path => Assert.Throws<InvalidDataException>(() => Open(path)));
                }
                WithSource(264, path => { using (Open(path)) { } });
            }, GetType());
        }

        [Fact]
        public void HeaderCallbackRequiresCompleteDestination()
        {
            IsolatedTest.Run(() => WithSource(264, path =>
            {
                using (var source = Open(path))
                {
                    foreach (int length in new[] { 0, 63, 64, 67 })
                    {
                        Assert.Throws<InvalidDataException>(() => InvokeHeader(source, length));
                    }
                    Assert.Equal(123, InvokeHeader(source, 68));
                }
            }), GetType());
        }

        #region private
        private static BPerfEventSource Open(string path)
        {
            return new BPerfEventSource(path, null, new byte[1024], new byte[1024],
                decompressDelegate: (input, offset, length, output, capacity) =>
                {
                    Array.Copy(input, offset, output, 0, length);
                    return length;
                });
        }

        private static unsafe int InvokeHeader(BPerfEventSource source, int length)
        {
            byte[] payload = new byte[68];
            fixed (byte* pointer = payload)
            {
                var record = new TraceEventNativeMethods.EVENT_RECORD();
                record.EventHeader.ProviderId = HeaderGuid;
                record.EventHeader.Flags = TraceEventNativeMethods.EVENT_HEADER_FLAG_CLASSIC_HEADER;
                record.UserDataLength = (ushort)length;
                record.UserData = (IntPtr)pointer;
                var data = Assert.IsType<EventTraceHeaderTraceData>(source.Lookup(&record));
                ((Action<EventTraceHeaderTraceData>)data.Target)(data);
                return Marshal.ReadInt32((IntPtr)pointer, 64);
            }
        }

        private static unsafe void WithSource(int length, Action<string> test)
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".btl");
            try
            {
                byte[] record = new byte[(112 + length + 15) & ~15];
                fixed (byte* pointer = record)
                {
                    var header = (TraceEventNativeMethods.EVENT_RECORD*)pointer;
                    header->EventHeader.ProviderId = HeaderGuid;
                    header->EventHeader.TimeStamp = 1;
                    header->UserDataLength = (ushort)length;
                }
                if (length >= 264)
                {
                    BitConverter.GetBytes(4).CopyTo(record, 112 + 44);
                    BitConverter.GetBytes(123).CopyTo(record, 112 + 68);
                    BitConverter.GetBytes(10000000L).CopyTo(record, 112 + 248);
                    BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(record, 112 + 256);
                }
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(record.Length);
                    writer.Write(record.Length);
                    writer.Write(record);
                }
                test(path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static readonly Guid HeaderGuid = new Guid("68fdd900-4a3e-11d1-84f4-0000f80464e3");
        #endregion
    }
}
