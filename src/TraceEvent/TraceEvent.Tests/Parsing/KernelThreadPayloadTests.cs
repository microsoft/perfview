using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TraceEventTests
{
    /// <summary>
    /// Checks thread fixup boundaries directly with padded, test-owned classic ETW records.
    /// Kernel thread events are ETW-only; these tests do not use EventPipe or require malformed ETL files.
    /// Padding safely exposes writes beyond the advertised payload without exceeding the backing allocation.
    /// </summary>
    public unsafe class KernelThreadPayloadTests
    {
        /// <summary>
        /// Covers legacy ID layouts (0/1), the pointer-sized prefix (2), optional tails (3), and a later version (4).
        /// Pointer widths describe the trace, not the test process; opcodes 1-4 are Start, Stop, DCStart, and DCStop.
        /// </summary>
        public static IEnumerable<object[]> ThreadLayouts()
        {
            foreach (int version in new[] { 0, 1, 2, 3, 4 })
            foreach (int pointerSize in new[] { 4, 8 })
            foreach (int opcode in new[] { 1, 2, 3, 4 })
            {
                yield return new object[] { version, pointerSize, opcode };
            }
        }

        /// <summary>
        /// Rejects every length below the required prefix before changing header IDs, parent state, or payload bytes.
        /// The record advertises the short length, but padded backing storage keeps baseline reads/writes in bounds.
        /// </summary>
        [Theory]
        [MemberData(nameof(ThreadLayouts))]
        public void ShortPayloadIsRejectedBeforeMutation(int version, int pointerSize, int opcode)
        {
            ThreadTraceData template = CreateTemplate(opcode);
            int minimumLength = MinimumLength(version, pointerSize);
            for (int length = 0; length < minimumLength; length++)
            {
                byte[] payload = CreatePayload(minimumLength);
                WithRecord(template, version, pointerSize, opcode, payload, length, data =>
                {
                    data.ParentThread = 321;
                    FormatException error = Assert.Throws<FormatException>(() => data.FixupData());
                    Assert.Contains("thread event payload", error.Message);
                    Assert.Equal(101, data.eventRecord->EventHeader.ProcessId);
                    Assert.Equal(202, data.eventRecord->EventHeader.ThreadId);
                    Assert.Equal(321, data.ParentThread);
                    Assert.Equal(payload.Take(length), data.EventData());
                }, verifyWholeStorage: true);
            }
        }

        /// <summary>
        /// Preserves valid ID remapping and optional-field access across all layouts, including short legacy stops.
        /// A valid/malformed/valid sequence on the same template verifies that rejection does not break reuse.
        /// </summary>
        [Theory]
        [MemberData(nameof(ThreadLayouts))]
        public void ValidPayloadPreservesFixupSemantics(int version, int pointerSize, int opcode)
        {
            ThreadTraceData template = CreateTemplate(opcode);
            int minimumLength = MinimumLength(version, pointerSize);
            // V1's extended layout is 8 ID bytes + six pointers + a 1-byte WaitMode (33/57 bytes).
            // V3+ tests each partial priority/flags tail and appended zero bytes for an empty name/future extensions.
            IEnumerable<int> lengths = version >= 3 ? Enumerable.Range(minimumLength, 11) :
                version == 1 ? new[] { minimumLength, 33 + (pointerSize - 4) * 6 } : new[] { minimumLength };
            foreach (int length in lengths)
            {
                byte[] payload = CreatePayload(length);
                if (version >= 3)
                {
                    for (int i = minimumLength; i < Math.Min(length, minimumLength + 4); i++)
                    {
                        payload[i] = (byte)(11 + i - minimumLength);
                    }
                }
                WithRecord(template, version, pointerSize, opcode, payload, length, data =>
                {
                    data.FixupData();
                    // Only Stop retains the header's IDs; DCStop still obtains its IDs from the payload.
                    bool changesIds = version >= 2 && opcode != (int)TraceEventOpcode.Stop;
                    Assert.Equal(changesIds ? 303 : 101, data.ProcessID);
                    Assert.Equal(changesIds ? 404 : 202, data.ThreadID);
                    // Only Start records the creator as parent; other fixed-up events use -1.
                    bool hasParent = version >= 2 && opcode == (int)TraceEventOpcode.Start;
                    Assert.Equal(hasParent ? 101 : -1, data.ParentProcessID);
                    Assert.Equal(hasParent ? 202 : -1, data.ParentThreadID);

                    byte[] expected = (byte[])payload.Clone();
                    if (version >= 2)
                    {
                        Buffer.BlockCopy(BitConverter.GetBytes(hasParent ? 101 : -1), 0, expected, 0, 4);
                        Buffer.BlockCopy(BitConverter.GetBytes(hasParent ? 202 : -1), 0, expected, 4, 4);
                    }
                    Assert.Equal(expected, data.EventData());
                    if (version >= 2)
                    {
                        Assert.Equal((ulong)0, data.StackBase);
                        Assert.Equal((ulong)0, data.StackLimit);
                        Assert.Equal((ulong)0, data.UserStackBase);
                        Assert.Equal((ulong)0, data.UserStackLimit);
                        Assert.Equal((ulong)0, data.StartAddr);
                        Assert.Equal((ulong)0, data.Win32StartAddr);
                        Assert.Equal((ulong)0, data.TebBase);
                        Assert.Equal(0, data.SubProcessTag);
                    }
                    Assert.Equal(version >= 3 && length > minimumLength ? 11 : 0, data.BasePriority);
                    Assert.Equal(version >= 3 && length > minimumLength + 1 ? 12 : 0, data.PagePriority);
                    Assert.Equal(version >= 3 && length > minimumLength + 2 ? 13 : 0, data.IoPriority);
                    Assert.Equal(version >= 3 && length > minimumLength + 3 ? 14 : 0, data.ThreadFlags);
                    Assert.Equal("", data.ThreadName);
                });
            }

            WithRecord(template, version, pointerSize, opcode, CreatePayload(minimumLength), minimumLength - 1,
                data => Assert.Throws<FormatException>(() => data.FixupData()), verifyWholeStorage: true);
            WithRecord(template, version, pointerSize, opcode, CreatePayload(minimumLength), minimumLength,
                data => data.FixupData());
        }

        #region private
        private static int MinimumLength(int version, int pointerSize)
        {
            // Versions 0/1 require only the two 4-byte process/thread IDs (8 bytes).
            // Version 2+ adds seven pointers and a 4-byte SubProcessTag: 40 bytes with 4-byte pointers.
            // Each pointer adds pointerSize - 4 bytes for a 64-bit trace, giving a 68-byte fixed prefix.
            return version < 2 ? 8 : 40 + (pointerSize - 4) * 7;
        }

        private static ThreadTraceData CreateTemplate(int opcode)
        {
            return new ThreadTraceData(data => { }, 0xFFFF, 2, "Thread", KernelTraceEventParser.ThreadTaskGuid,
                opcode, ((TraceEventOpcode)opcode).ToString(), KernelTraceEventParser.ProviderGuid,
                KernelTraceEventParser.ProviderName, null);
        }

        private static byte[] CreatePayload(int length)
        {
            // Allocate the full test layout (at least 8 bytes), even when the record advertises a shorter payload.
            // ProcessId and ThreadId are 4-byte integers at offsets 0 and 4, independent of pointer width.
            byte[] payload = new byte[length];
            Buffer.BlockCopy(BitConverter.GetBytes(303), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(404), 0, payload, 4, 4);
            return payload;
        }

        private static void WithRecord(ThreadTraceData template, int version, int pointerSize, int opcode,
            byte[] payload, int length, Action<ThreadTraceData> action, bool verifyWholeStorage = false)
        {
            const int padding = 16;
            // Keep baseline reads/writes inside pinned storage, even past the advertised payload boundary.
            // 0xA5 guard bytes on each side detect writes outside the logical payload without a native overrun.
            byte[] storage = Enumerable.Repeat((byte)0xA5, padding + payload.Length + padding).ToArray();
            Buffer.BlockCopy(payload, 0, storage, padding, payload.Length);
            byte[] original = (byte[])storage.Clone();
            fixed (byte* bytes = storage)
            {
                TraceEventNativeMethods.EVENT_RECORD record = new TraceEventNativeMethods.EVENT_RECORD();
                record.EventHeader.Flags = (ushort)(TraceEventNativeMethods.EVENT_HEADER_FLAG_CLASSIC_HEADER |
                    (pointerSize == 4 ? TraceEventNativeMethods.EVENT_HEADER_FLAG_32_BIT_HEADER :
                        TraceEventNativeMethods.EVENT_HEADER_FLAG_64_BIT_HEADER));
                record.EventHeader.Version = (byte)version;
                record.EventHeader.Opcode = (byte)opcode;
                record.EventHeader.ProcessId = 101;
                record.EventHeader.ThreadId = 202;
                record.UserDataLength = (ushort)length;
                record.UserData = (IntPtr)(bytes + padding);
                template.eventRecord = &record;
                template.userData = record.UserData;
                try
                {
                    action(template);
                }
                finally
                {
                    template.eventRecord = null;
                    template.userData = IntPtr.Zero;
                    if (verifyWholeStorage)
                    {
                        Assert.Equal(original, storage);
                    }
                    else
                    {
                        Assert.Equal(original.Take(padding), storage.Take(padding));
                        Assert.Equal(original.Skip(padding + length), storage.Skip(padding + length));
                    }
                }
            }
        }
        #endregion
    }
}
