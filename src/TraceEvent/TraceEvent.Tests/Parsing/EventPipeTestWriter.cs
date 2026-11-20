using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace TraceEventTests
{
    public class EventMetadata
    {
        public EventMetadata(int metadataId, string providerName, string eventName, int eventId, params MetadataParameter[] parameters)
        {
            MetadataId = metadataId;
            ProviderName = providerName;
            EventName = eventName;
            EventId = eventId;
            Parameters = parameters;
        }

        public int MetadataId { get; set; }
        public string ProviderName { get; set; }
        public string EventName { get; set; }
        public int EventId { get; set; }
        public MetadataParameter[] Parameters { get; set; }

        // V6 Optional metadata
        public byte OpCode { get; set; }
        public string Description { get; set; }
        public string MessageTemplate { get; set; }
        public Guid ProviderId { get; set; }
        public long Keywords { get; set; }
        public byte Level { get; set; }
        public byte Version { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

    }

    public class MetadataParameter
    {
        public MetadataParameter(string name, MetadataType type)
        {
            Name = name;
            Type = type;
        }
        public MetadataParameter(string name, MetadataTypeCode typeCode)
        {
            Name = name;
            Type = new MetadataType(typeCode);
        }
        public string Name { get; set; }
        public MetadataType Type { get; set; }
    }

    public class MetadataType
    {
        public MetadataType(MetadataTypeCode typeCode)
        {
            TypeCode = typeCode;
        }
        public MetadataTypeCode TypeCode { get; set; }
    }

    public class ArrayMetadataType : MetadataType
    {
        public ArrayMetadataType(MetadataType elementType) : base(MetadataTypeCode.Array)
        {
            ElementType = elementType;
        }
        public MetadataType ElementType { get; set; }
    }

    public class ObjectMetadataType : MetadataType
    {
        public ObjectMetadataType(params MetadataParameter[] parameters) : base(MetadataTypeCode.Object)
        {
            Parameters = parameters;
        }
        public MetadataParameter[] Parameters { get; set; }
    }

    public class FixedLengthArrayMetadataType : MetadataType
    {
        public FixedLengthArrayMetadataType(int elementCount, MetadataType elementType) : base(MetadataTypeCode.FixedLengthArray)
        {
            ElementType = elementType;
            ElementCount = elementCount;
        }
        public MetadataType ElementType { get; set; }
        public int ElementCount { get; set; }
    }

    public class RelLocMetadataType : MetadataType
    {
        public RelLocMetadataType(MetadataType elementType) : base(MetadataTypeCode.RelLoc)
        {
            ElementType = elementType;
        }
        public MetadataType ElementType { get; set; }
    }

    public class DataLocMetadataType : MetadataType
    {
        public DataLocMetadataType(MetadataType elementType) : base(MetadataTypeCode.DataLoc)
        {
            ElementType = elementType;
        }
        public MetadataType ElementType { get; set; }
    }

    public enum MetadataTypeCode
    {
        Object = 1,                        // Concatenate together all of the encoded fields
        Boolean32 = 3,                     // A 4-byte LE integer with value 0=false and 1=true.
        UTF16CodeUnit = 4,                 // a 2-byte UTF16 code unit
        SByte = 5,                         // 1-byte signed integer
        Byte = 6,                          // 1-byte unsigned integer
        Int16 = 7,                         // 2-byte signed LE integer
        UInt16 = 8,                        // 2-byte unsigned LE integer
        Int32 = 9,                         // 4-byte signed LE integer
        UInt32 = 10,                       // 4-byte unsigned LE integer
        Int64 = 11,                        // 8-byte signed LE integer
        UInt64 = 12,                       // 8-byte unsigned LE integer
        Single = 13,                       // 4-byte single-precision IEEE754 floating point value
        Double = 14,                       // 8-byte double-precision IEEE754 floating point value
        DateTime = 16,                     // Encoded as 8 concatenated Int16s representing year, month, dayOfWeek, day, hour, minute, second, and milliseconds.
        Guid = 17,                         // A 16-byte guid encoded as the concatenation of an Int32, 2 Int16s, and 8 Uint8s
        NullTerminatedUTF16String = 18,    // A string encoded with UTF16 characters and a 2-byte null terminator
        Array = 19,                        // New in V5 optional params: a UInt16 length-prefixed variable-sized array. Elements are encoded depending on the ElementType.
        VarInt = 20,                       // New in V6: variable-length signed integer with zig-zag encoding (defined the same as in Protobuf)
        VarUInt = 21,                      // New in V6: variable-length unsigned integer (ULEB128)
        FixedLengthArray = 22,             // New in V6: A fixed-length array of elements. The size is determined by the metadata.
        UTF8CodeUnit = 23,                 // New in V6: A single UTF8 code unit (1 byte).
        RelLoc = 24,                       // New in V6: An array at a relative location within the payload.
        DataLoc = 25,                      // New in V6: An absolute data location within the payload.
        Boolean8 = 26                      // New in V6: A 1-byte boolean with value 0=false and 1=true.
    }

    public class EventPayloadWriter
    {
        BinaryWriter _writer = new BinaryWriter(new MemoryStream());

        public void WriteNullTerminatedUTF16String(string arg)
        {
            _writer.Write(Encoding.Unicode.GetBytes(arg));
            _writer.Write((ushort)0);
        }

        public void WriteArray<T>(T[] elements, Action<T> writeElement)
        {
            WriteArrayLength(elements.Length);
            for (int i = 0; i < elements.Length; i++)
            {
                writeElement(elements[i]);
            }
        }

        public void WriteArrayLength(int length)
        {
            _writer.Write((ushort)length);
        }

        public byte[] ToArray()
        {
            return (_writer.BaseStream as MemoryStream).ToArray();
        }
    }

    abstract class EventPipeWriter
    {
        protected BinaryWriter _writer;

        protected EventPipeWriter()
            : this(new MemoryStream())
        {
        }

        protected EventPipeWriter(Stream stream)
        {
            _writer = new BinaryWriter(stream);
        }

        public byte[] ToArray()
        {
            return (_writer.BaseStream as MemoryStream).ToArray();
        }

        public void Flush()
        {
            _writer.Flush();
        }

        abstract public void WriteHeaders();
        abstract public void WriteMetadataBlock(params EventMetadata[] metadataBlobs);
    }

    class EventPipeWriterV5 : EventPipeWriter
    {
        public override void WriteHeaders()
        {
            _writer.WriteNetTraceHeaderV5();
            _writer.WriteFastSerializationHeader();
            _writer.WriteTraceObjectV5();
        }
        public override void WriteMetadataBlock(params EventMetadata[] metadataBlobs)
        {
            _writer.WriteMetadataBlockV5OrLess(metadataBlobs);
        }
        public void WriteMetadataBlock(Action<BinaryWriter> writeMetadataEventBlobs)
        {
            _writer.WriteMetadataBlockV5OrLess(writeMetadataEventBlobs);
        }

        public void WriteEventBlock(Action<BinaryWriter> writeEventBlobs)
        {
            _writer.WriteEventBlockV5OrLess(writeEventBlobs);
        }

        public void WriteEndObject()
        {
            _writer.WriteEndObject();
        }

        public void WriteBlock(string name, Action<BinaryWriter> writeBlockData, long previousBytesWritten = 0)
        {
            _writer.WriteBlockV5OrLess(name, writeBlockData, previousBytesWritten);
        }
    }

    public struct V6ThreadSequencePoint
    {
        public V6ThreadSequencePoint(ulong threadIndex, uint sequenceNumber)
        {
            ThreadIndex = threadIndex;
            SequenceNumber = sequenceNumber;
        }
        public ulong ThreadIndex;
        public uint SequenceNumber;
    }

    class EventPipeWriterV6 : EventPipeWriter
    {
        public EventPipeWriterV6()
        {
        }

        public EventPipeWriterV6(Stream stream)
            : base(stream)
        {
        }

        public override void WriteHeaders() => WriteHeaders(null, 6, 0);

        public void WriteHeaders(Dictionary<string,string> keyValues, int majorVersion = 6, int minorVersion = 0, long syncTimeQpc = 0,
            long qpcFrequency = 1000)
        {
            if(keyValues == null)
            {
                keyValues = new Dictionary<string, string>();
            }
            _writer.WriteNetTraceHeaderV6OrGreater(majorVersion, minorVersion);
            _writer.WriteTraceBlockV6OrGreater(keyValues, syncTimeQpc, qpcFrequency);
        }

        public override void WriteMetadataBlock(params EventMetadata[] metadataBlobs)
        {
            _writer.WriteMetadataBlockV6OrGreater(metadataBlobs);
        }

        public void WriteMetadataBlock(Action<BinaryWriter> writeMetadataBlobs)
        {
            _writer.WriteMetadataBlockV6OrGreater(writeMetadataBlobs);
        }

        public void WriteEventBlock(Action<V6EventBlockWriter> writeEventBlobs) => WriteEventBlock(false, writeEventBlobs);

        public void WriteEventBlock(bool useCompressedHeader, Action<V6EventBlockWriter> writeEventBlobs)
        {
            WriteBlock(2 /* Event */, w =>
            {
                V6EventBlockWriter blockWriter = new V6EventBlockWriter(w, useCompressedHeader);
                blockWriter.WriteHeader();
                writeEventBlobs(blockWriter);
            });
        }

        public void WriteThreadBlock(Action<BinaryWriter> writeThreadEntries)
        {
            _writer.WriteThreadBlock(writeThreadEntries);
        }

        public void WriteRemoveThreadBlock(Action<BinaryWriter> writeThreadEntries)
        {
            _writer.WriteRemoveThreadBlock(writeThreadEntries);
        }

        public void WriteSequencePointBlock(long timestamp, bool resetThreadIndicies, bool resetMetadataIndices, params V6ThreadSequencePoint[] sequencePoints)
        {
            WriteBlock(4 /* BlockKind.SequencePoint */, w =>
            {
                w.Write(timestamp);
                int flags = 0;
                if (resetThreadIndicies)
                {
                    flags |= 1;
                }
                if (resetMetadataIndices)
                {
                    flags |= 2;
                }

                w.Write(flags);
                w.Write(sequencePoints.Length);
                foreach (var sequencePoint in sequencePoints)
                {
                    w.WriteVarUInt(sequencePoint.ThreadIndex);
                    w.WriteVarUInt(sequencePoint.SequenceNumber);
                }
            });
        }

        public void WriteLabelListBlock(int firstIndex, int count, Action<V6LabelListBlockWriter> writeLabelListEntries)
        {
            _writer.WriteV6LabelListBlock(firstIndex, count, writeLabelListEntries);
        }

        /// <summary>Writes a stack block; <paramref name="writeStacks"/> must write <paramref name="count"/> stack payloads.</summary>
        public void WriteStackBlock(int firstStackId, int count, Action<BinaryWriter> writeStacks)
        {
            WriteBlock(5 /* BlockKind.StackBlock */, w =>
            {
                w.Write(firstStackId);
                w.Write(count);
                writeStacks(w);
            });
        }

        public void WriteEndBlock() => WriteBlock(0 /* BLockKind.EndOfStream */, w => { });

        public void WriteBlock(byte blockKind, Action<BinaryWriter> writePayload)
        {
            _writer.WriteBlockV6OrGreater(blockKind, writePayload);
        }
    }

    public class WriteEventOptions
    {
        public int MetadataId { get; set; }
        public long ThreadIndexOrId { get; set; }
        public long CaptureThreadIndexOrId { get; set; }
        public int SequenceNumber { get; set; }
        public int ProcNumber { get; set; }
        public int StackId { get; set; }
        public long Timestamp { get; set; }
        public int LabelListId { get; set; }
        public Guid ActivityId { get; set; }
        public Guid RelatedActivityId { get; set; }
        public bool IsSorted { get; set; }

    }

    public static class BinaryWriterExtensions
    {
        public static void WriteNetTraceHeaderV5(this BinaryWriter writer)
        {
            writer.Write(Encoding.UTF8.GetBytes("Nettrace"));
        }

        public static void WriteNetTraceHeaderV6OrGreater(this BinaryWriter writer, int majorVersion, int minorVersion)
        {
            writer.Write(Encoding.UTF8.GetBytes("Nettrace"));
            writer.Write(0); // reserved
            writer.Write(majorVersion);
            writer.Write(minorVersion);
        }

        public static void WriteFastSerializationHeader(this BinaryWriter writer)
        {
            WriteInt32PrefixedUTF8String(writer, "!FastSerialization.1");
        }

        public static void WriteInt32PrefixedUTF8String(this BinaryWriter writer, string val)
        {
            writer.Write(val.Length);
            writer.Write(Encoding.UTF8.GetBytes(val));
        }

        public static void WriteNullTerminatedUTF16String(this BinaryWriter writer, string val)
        {
            writer.Write(Encoding.Unicode.GetBytes(val));
            writer.Write((short)0);
        }

        public static void WriteLengthPrefixedUTF16String(this BinaryWriter writer, string val)
        {
            byte[] utf16Bytes = Encoding.Unicode.GetBytes(val);
            writer.Write((ushort)val.Length);
            writer.Write(utf16Bytes);
        }

        public static void WriteLengthPrefixedUTF8String(this BinaryWriter writer, string val)
        {
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(val);
            writer.Write((ushort)utf8Bytes.Length);
            writer.Write(utf8Bytes);
        }


        public static void WriteVarUInt(this BinaryWriter writer, ulong val)
        {
            while (true)
            {
                byte low7 = (byte)(val & 0x7F);
                val >>= 7;
                if (val == 0)
                {
                    writer.Write(low7);
                    break;
                }
                else
                {
                    writer.Write((byte)(low7 | 0x80));
                }
            }
        }

        public static void WriteVarInt(this BinaryWriter writer, long val)
        {
            if(val < 0)
            {
                writer.WriteVarUInt((ulong)(~val << 1) | 0x1);
            }
            else
            {
                writer.WriteVarUInt((ulong)(val << 1));
            }
        }

        public static void WriteVarUIntPrefixedUTF8String(this BinaryWriter writer, string val)
        {
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(val);
            WriteVarUInt(writer, (ulong)utf8Bytes.Length);
            writer.Write(utf8Bytes);
        }

        public static void Write(this BinaryWriter writer, Guid val)
        {
            writer.Write(val.ToByteArray());
        }

        // used in versions <= 5
        public static void WriteObject(this BinaryWriter writer, string name, int version, int minVersion,
    Action writePayload)
        {
            writer.Write((byte)5); // begin private object
            writer.Write((byte)5); // begin private object - type
            writer.Write((byte)1); // type of type
            writer.Write(version);
            writer.Write(minVersion);
            WriteInt32PrefixedUTF8String(writer, name);
            writer.Write((byte)6); // end object
            writePayload();
            writer.Write((byte)6); // end object
        }

        public static void WriteBlockV6OrGreater(this BinaryWriter writer, byte blockKind, Action<BinaryWriter> writePayload)
        {
            long blockHeaderPos = writer.BaseStream.Position;
            writer.Write((uint)0);
            writePayload(writer);
            long endBlockPos = writer.BaseStream.Position;

            // backup and fill in the block header now that the length is known
            writer.Seek((int)blockHeaderPos, SeekOrigin.Begin);
            uint size = (uint)(endBlockPos - blockHeaderPos - 4);
            uint header = size | ((uint)blockKind << 24);
            writer.Write(header);
            writer.Seek((int)endBlockPos, SeekOrigin.Begin);
        }

        public static void WriteTraceObjectV5(this BinaryWriter writer)
        {
            WriteObject(writer, "Trace", 4, 4, () =>
            {
                DateTime now = DateTime.Now;
                writer.Write((short)now.Year);
                writer.Write((short)now.Month);
                writer.Write((short)now.DayOfWeek);
                writer.Write((short)now.Day);
                writer.Write((short)now.Hour);
                writer.Write((short)now.Minute);
                writer.Write((short)now.Second);
                writer.Write((short)now.Millisecond);
                writer.Write((long)1_000_000); // syncTimeQPC
                writer.Write((long)1000); // qpcFreq
                writer.Write(8); // pointer size
                writer.Write(1); // pid
                writer.Write(4); // num procs
                writer.Write(1000); // sampling rate
            });
        }

        public static void WriteTraceBlockV6OrGreater(this BinaryWriter writer, Dictionary<string,string> keyValues, long syncTimeQpc = 0,
            long qpcFrequency = 1000)
        {
            writer.WriteTraceBlockV6OrGreater(
                new DateTime(2025, 2, 3, 4, 5, 6),
                syncTimeQpc: syncTimeQpc,
                qpcFrequency: qpcFrequency,
                pointerSize: 8,
                keyValues);
        }

        public static void WriteTraceBlockV6OrGreater(
            this BinaryWriter writer,
            DateTime startTime,
            long syncTimeQpc,
            long qpcFrequency,
            int pointerSize,
            Dictionary<string, string> keyValues)
        {
            WriteBlockV6OrGreater(writer, 1 /* BlockKind.Trace */, w =>
            {
                w.Write((short)startTime.Year);
                w.Write((short)startTime.Month);
                w.Write((short)startTime.DayOfWeek);
                w.Write((short)startTime.Day);
                w.Write((short)startTime.Hour);
                w.Write((short)startTime.Minute);
                w.Write((short)startTime.Second);
                w.Write((short)startTime.Millisecond);
                w.Write(syncTimeQpc);
                w.Write(qpcFrequency);
                w.Write(pointerSize);
                w.Write(keyValues.Count);
                foreach(var kv in keyValues)
                {
                    w.WriteVarUIntPrefixedUTF8String(kv.Key);
                    w.WriteVarUIntPrefixedUTF8String(kv.Value);
                }
            });
        }

        private static void Align(BinaryWriter writer, long previousBytesWritten)
        {
            int offset = (int)((writer.BaseStream.Position + previousBytesWritten) % 4);
            if (offset != 0)
            {
                for (int i = offset; i < 4; i++)
                {
                    writer.Write((byte)0);
                }
            }
        }

        public static void WriteBlockV5OrLess(this BinaryWriter writer, string name, Action<BinaryWriter> writeBlockData,
            long previousBytesWritten = 0)
        {
            Debug.WriteLine($"Starting block {name} position: {writer.BaseStream.Position + previousBytesWritten}");
            MemoryStream block = new MemoryStream();
            BinaryWriter blockWriter = new BinaryWriter(block);
            writeBlockData(blockWriter);
            WriteObject(writer, name, 2, 0, () =>
            {
                writer.Write((int)block.Length);
                Align(writer, previousBytesWritten);
                writer.Write(block.GetBuffer(), 0, (int)block.Length);
            });
        }

        public static void WriteMetadataBlockV6OrGreater(this BinaryWriter writer, params EventMetadata[] metadataBlobs)
        {
            WriteMetadataBlockV6OrGreater(writer, w =>
            {
                foreach (EventMetadata metadata in metadataBlobs)
                {
                    w.WriteMetadataBlobV6OrGreater(metadata);
                }
            });
        }

        public static void WriteMetadataBlockV6OrGreater(this BinaryWriter writer, Action<BinaryWriter> writeMetadataBlobs)
        {
            WriteBlockV6OrGreater(writer, 3 /* Metadata */, w =>
            {
                w.Write((UInt16)0);    // header size
                writeMetadataBlobs(w);
            });
        }

        public static void WriteMetadataBlobV6OrGreater(this BinaryWriter writer, EventMetadata metadata)
        {
            writer.WriteMetadataBlobV6OrGreater(w =>
            {
                w.WriteV6InitialMetadataBlob(metadata.MetadataId, metadata.ProviderName, metadata.EventName, metadata.EventId);
                w.WriteV6MetadataParameterList(metadata.Parameters);
                w.WriteV6OptionalMetadataList(metadata);
            });
        }

        public static void WriteMetadataBlobV6OrGreater(this BinaryWriter writer, Action<BinaryWriter> writeMetadataPayload)
        {
            MemoryStream payloadBlob = new MemoryStream();
            BinaryWriter payloadWriter = new BinaryWriter(payloadBlob);
            writeMetadataPayload(payloadWriter);
            writer.Write((UInt16)payloadBlob.Length);
            writer.Write(payloadBlob.GetBuffer(), 0, (int)payloadBlob.Length);
        }

        public static void WriteV6InitialMetadataBlob(this BinaryWriter writer, int metadataId, string providerName, string eventName, int eventId)
        {
            writer.WriteVarUInt((uint)metadataId);                // metadata id
            writer.WriteVarUIntPrefixedUTF8String(providerName);  // provider name
            writer.WriteVarUInt((uint)eventId);                   // event id
            writer.WriteVarUIntPrefixedUTF8String(eventName);     // event name
        }

        public static void WriteV6MetadataParameterList(this BinaryWriter writer, params MetadataParameter[] parameters)
        {
            writer.WriteV6MetadataParameterList(parameters.Length, w =>
            {
                foreach (var parameter in parameters)
                {
                    w.WriteV6MetadataParameter(parameter);
                }
            });
        }

        public static void WriteV6MetadataParameterList(this BinaryWriter writer, int parameterCount, Action<BinaryWriter> writeParameters)
        {
            writer.Write((UInt16)parameterCount);
            writeParameters(writer);
        }

        public static void WriteV6MetadataParameter(this BinaryWriter writer, MetadataParameter parameter)
        {
            writer.WriteV6MetadataParameter(parameter.Name, w => { w.WriteV6MetadataType(parameter.Type); });
        }

        public static void WriteV6MetadataParameter(this BinaryWriter writer, string parameterName, Action<BinaryWriter> writeType)
        {
            MemoryStream paramStream = new MemoryStream();
            BinaryWriter paramWriter = new BinaryWriter(paramStream);
            paramWriter.WriteVarUIntPrefixedUTF8String(parameterName);
            writeType(paramWriter);

            writer.Write((UInt16)paramStream.Length);
            writer.Write(paramStream.GetBuffer(), 0, (int)paramStream.Length);
        }

        public static void WriteV6MetadataType(this BinaryWriter writer, MetadataType type)
        {
            writer.Write((byte)type.TypeCode);
            if(type.TypeCode == MetadataTypeCode.Array)
            {
                writer.WriteV6MetadataType((type as ArrayMetadataType).ElementType);
            }
            else if (type.TypeCode == MetadataTypeCode.FixedLengthArray)
            {
                writer.WriteV6MetadataType((type as FixedLengthArrayMetadataType).ElementType);
                writer.Write((ushort)(type as FixedLengthArrayMetadataType).ElementCount);
            }
            else if(type.TypeCode == MetadataTypeCode.RelLoc)
            {
                writer.WriteV6MetadataType((type as RelLocMetadataType).ElementType);
            }
            else if(type.TypeCode == MetadataTypeCode.DataLoc)
            {
                writer.WriteV6MetadataType((type as DataLocMetadataType).ElementType);
            }
            else if(type.TypeCode == MetadataTypeCode.Object)
            {
                writer.WriteV6MetadataParameterList((type as ObjectMetadataType).Parameters);
            }
        }

        public static void WriteV6OptionalMetadataList(this BinaryWriter writer, EventMetadata metadata)
        {
            writer.WriteV6OptionalMetadataList(w =>
            {
                if (metadata.OpCode != 0)
                {
                    w.WriteV6OptionalMetadataOpcode(metadata.OpCode);
                }
                if (metadata.Keywords != 0)
                {
                    w.WriteV6OptionalMetadataKeyword(metadata.Keywords);
                }
                if (metadata.MessageTemplate != null)
                {
                    w.WriteV6OptionalMetadataMessageTemplate(metadata.MessageTemplate);
                }
                if (metadata.Description != null)
                {
                    w.WriteV6OptionalMetadataDescription(metadata.Description);
                }
                foreach (var kv in metadata.Attributes)
                {
                    w.WriteV6OptionalMetadataAttribute(kv.Key, kv.Value);
                }
                if (metadata.ProviderId != default)
                {
                    w.WriteV6OptionalMetadataProviderGuid(metadata.ProviderId);
                }
                if (metadata.Level != 0)
                {
                    w.WriteV6OptionalMetadataLevel(metadata.Level);
                }
                if (metadata.Version != 0)
                {
                    w.WriteV6OptionalMetadataVersion(metadata.Version);
                }
            });
        }

        public static void WriteV6OptionalMetadataList(this BinaryWriter writer, Action<BinaryWriter> writeOptionalMetadata)
        {
            MemoryStream optionalMetadata = new MemoryStream();
            BinaryWriter optionalMetadataWriter = new BinaryWriter(optionalMetadata);
            writeOptionalMetadata(optionalMetadataWriter);

            writer.Write((ushort)optionalMetadata.Length);
            writer.Write(optionalMetadata.GetBuffer(), 0, (int)optionalMetadata.Length);
        }

        public static void WriteV6OptionalMetadataOpcode(this BinaryWriter writer, byte opcode)
        {
            writer.Write((byte)1);       // OptionalMetadataKind.Opcode
            writer.Write((byte)opcode);
        }

        public static void WriteV6OptionalMetadataKeyword(this BinaryWriter writer, long keyword)
        {
            writer.Write((byte)3);       // OptionalMetadataKind.Keyword
            writer.Write(keyword);
        }

        public static void WriteV6OptionalMetadataMessageTemplate(this BinaryWriter writer, string template)
        {
            writer.Write((byte)4);       // OptionalMetadataKind.MessageTemplate
            writer.WriteVarUIntPrefixedUTF8String(template);
        }

        public static void WriteV6OptionalMetadataDescription(this BinaryWriter writer, string description)
        {
            writer.Write((byte)5);       // OptionalMetadataKind.Description
            writer.WriteVarUIntPrefixedUTF8String(description);
        }

        public static void WriteV6OptionalMetadataAttribute(this BinaryWriter writer, string key, string value)
        {
            writer.Write((byte)6);       // OptionalMetadataKind.KeyValuePair
            writer.WriteVarUIntPrefixedUTF8String(key);
            writer.WriteVarUIntPrefixedUTF8String(value);
        }

        public static void WriteV6OptionalMetadataProviderGuid(this BinaryWriter writer, Guid providerId)
        {
            writer.Write((byte)7);       // OptionalMetadataKind.ProviderGuid
            writer.Write(providerId);
        }

        public static void WriteV6OptionalMetadataLevel(this BinaryWriter writer, byte level)
        {
            writer.Write((byte)8);       // OptionalMetadataKind.Level
            writer.Write(level);
        }

        public static void WriteV6OptionalMetadataVersion(this BinaryWriter writer, byte version)
        {
            writer.Write((byte)9);       // OptionalMetadataKind.Version
            writer.Write(version);
        }

        public static void WriteMetadataBlockV5OrLess(this BinaryWriter writer, Action<BinaryWriter> writeMetadataEventBlobs, long previousBytesWritten = 0)
        {
            WriteBlockV5OrLess(writer, "MetadataBlock", w =>
            {
                // header
                w.Write((short)20); // header size
                w.Write((short)0); // flags
                w.Write((long)0);  // min timestamp
                w.Write((long)0);  // max timestamp
                writeMetadataEventBlobs(w);
            },
            previousBytesWritten);
        }

        public static void WriteMetadataBlockV5OrLess(this BinaryWriter writer, EventMetadata[] metadataBlobs, long previousBytesWritten = 0)
        {
            WriteMetadataBlockV5OrLess(writer,
                w =>
                {
                    foreach (EventMetadata blob in metadataBlobs)
                    {
                        WriteMetadataEventBlobV5OrLess(w, blob);
                    }
                },
                previousBytesWritten);
        }

        public static void WriteMetadataBlockV5OrLess(this BinaryWriter writer, params EventMetadata[] metadataBlobs)
        {
            WriteMetadataBlockV5OrLess(writer, metadataBlobs, 0);
        }

        public static void WriteMetadataEventBlobV5OrLess(this BinaryWriter writer, EventMetadata eventMetadataBlob)
        {
            writer.WriteMetadataEventBlobV5OrLess(w =>
            {
                w.WriteV5InitialMetadataBlob(eventMetadataBlob.MetadataId, eventMetadataBlob.ProviderName, eventMetadataBlob.EventName, eventMetadataBlob.EventId);
                w.WriteV5MetadataParameterList();
                if(eventMetadataBlob.OpCode != 0)
                {
                    w.WriteV5OpcodeMetadataTag(eventMetadataBlob.OpCode);
                }
            });
        }

        public static void WriteMetadataEventBlobV5OrLess(this BinaryWriter writer, Action<BinaryWriter> writeMetadataEventPayload)
        {
            writer.WriteEventBlobV4Or5(metadataId: 0, threadIndex:0, sequenceNumber: 0, w =>
            {
                writeMetadataEventPayload(w);
            });
        }

        public static void WriteV5InitialMetadataBlob(this BinaryWriter writer, int metadataId, string providerName, string eventName, int eventId)
        {
            writer.Write(metadataId);                             // metadata id
            writer.WriteNullTerminatedUTF16String(providerName);  // provider name
            writer.Write(eventId);                                // event id
            writer.WriteNullTerminatedUTF16String(eventName);     // event name
            writer.Write((long)0);                                // keywords
            writer.Write(1);                                      // version
            writer.Write(5);                                      // level
        }

        public static void WriteV5MetadataParameterList(this BinaryWriter writer)
        {
            writer.Write(0); // fieldcount
        }

        public static void WriteV5MetadataParameterList(this BinaryWriter writer, int fieldCount, Action<BinaryWriter> writeParameters)
        {
            writer.Write(fieldCount);
            writeParameters(writer);
        }

        /// <summary>
        /// The V2 here refers to fieldLayout V2, which is used in the V2Params tag area of the V5 format
        /// </summary>
        public static void WriteFieldLayoutV2MetadataParameter(this BinaryWriter writer, string parameterName, Action<BinaryWriter> writeType)
        {
            MemoryStream parameterBlob = new MemoryStream();
            BinaryWriter parameterWriter = new BinaryWriter(parameterBlob);
            parameterWriter.WriteNullTerminatedUTF16String(parameterName);
            writeType(parameterWriter);
            int payloadSize = (int)parameterBlob.Length;

            writer.Write((int)(payloadSize + 4));                              // parameter size includes the leading size field
            writer.Write(parameterBlob.GetBuffer(), 0, payloadSize);
        }

        public static void WriteV5MetadataTagBytes(this BinaryWriter writer, byte tag, Action<BinaryWriter> writeTagPayload)
        {
            MemoryStream payloadBlob = new MemoryStream();
            BinaryWriter payloadWriter = new BinaryWriter(payloadBlob);
            writeTagPayload(payloadWriter);
            int payloadSize = (int)payloadBlob.Length;

            writer.Write((int)payloadSize);
            writer.Write((byte)tag);
            writer.Write(payloadBlob.GetBuffer(), 0, payloadSize);
        }

        public static void WriteV5OpcodeMetadataTag(this BinaryWriter writer, byte opcode)
        {
            WriteV5MetadataTagBytes(writer, 1 /* OpcodeTag */, w =>
            {
                w.Write((byte)opcode);
            });
        }

        public static void WriteV5MetadataV2ParamTag(this BinaryWriter writer, int fieldCount, Action<BinaryWriter> writeFields)
        {
            WriteV5MetadataTagBytes(writer, 2 /* V2ParamTag */, w =>
            {
                w.WriteV5MetadataParameterList(fieldCount, writeFields);
            });
        }



        public static void WriteV6LabelListBlock(this BinaryWriter writer, int firstIndex, int count, Action<V6LabelListBlockWriter> writeLabelLists)
        {
            WriteBlockV6OrGreater(writer, 8 /* BlockKind.LabelList */, w =>
            {
                w.Write(firstIndex);
                w.Write(count);
                V6LabelListBlockWriter labelListWriter = new V6LabelListBlockWriter(w);
                writeLabelLists(labelListWriter);
            });
        }

        public static void WriteEventBlockV5OrLess(this BinaryWriter writer, Action<BinaryWriter> writeEventBlobs, long previousBytesWritten = 0)
        {
            WriteBlockV5OrLess(writer, "EventBlock", w =>
            {
                // header
                w.Write((short)20); // header size
                w.Write((short)0);  // flags
                w.Write((long)0);   // min timestamp
                w.Write((long)0);   // max timestamp
                writeEventBlobs(w);
            },
            previousBytesWritten);
        }


        public static void WriteEventBlobV4Or5(this BinaryWriter writer, int metadataId, long threadIndex, int sequenceNumber, byte[] payloadBytes)
        {
            WriteEventBlobV4Or5(writer, metadataId, threadIndex, sequenceNumber, w => w.Write(payloadBytes));
        }

        public static void WriteEventBlobV4Or5(this BinaryWriter writer, int metadataId, long threadIndex, int sequenceNumber, Action<BinaryWriter> writeEventPayload)
        {
            writer.WriteEventBlobV4Or5(new WriteEventOptions { MetadataId = metadataId, CaptureThreadIndexOrId = threadIndex, ThreadIndexOrId = threadIndex, SequenceNumber = sequenceNumber }, writeEventPayload);
        }

        public static void WriteEventBlobV4Or5(this BinaryWriter writer, WriteEventOptions options, Action<BinaryWriter> writeEventPayload)
        {
            MemoryStream payloadBlob = new MemoryStream();
            BinaryWriter payloadWriter = new BinaryWriter(payloadBlob);
            writeEventPayload(payloadWriter);
            int payloadSize = (int)payloadBlob.Length;

            MemoryStream eventBlob = new MemoryStream();
            BinaryWriter eventWriter = new BinaryWriter(eventBlob);
            eventWriter.Write(options.MetadataId | (int)(options.IsSorted ? 0 : 0x80000000));
            eventWriter.Write(options.SequenceNumber);
            eventWriter.Write(options.ThreadIndexOrId);
            eventWriter.Write(options.CaptureThreadIndexOrId);
            eventWriter.Write(options.ProcNumber);
            eventWriter.Write(options.StackId);
            eventWriter.Write(options.Timestamp);
            eventWriter.Write(options.ActivityId.ToByteArray());
            eventWriter.Write(options.RelatedActivityId.ToByteArray());
            eventWriter.Write(payloadSize);

            writer.Write((int)eventBlob.Length + payloadSize);
            writer.Write(eventBlob.GetBuffer(), 0, (int)eventBlob.Length);
            writer.Write(payloadBlob.GetBuffer(), 0, payloadSize);
        }

        public static void WriteThreadBlock(this BinaryWriter writer, Action<BinaryWriter> writeThreadEntries)
        {
            writer.WriteBlockV6OrGreater(6 /* Thread */, writeThreadEntries);
        }

        public static void WriteThreadEntry(this BinaryWriter writer, long threadIndex, int threadId, int processId)
        {
            writer.WriteThreadEntry(threadIndex, w =>
            {
                w.WriteThreadEntryThreadId(threadId);
                w.WriteThreadEntryProcessId(processId);
            });
        }

        public static void WriteThreadEntry(this BinaryWriter writer, long threadIndex, Action<BinaryWriter> writeThreadOptionalData)
        {
            MemoryStream threadEntry = new MemoryStream();
            BinaryWriter threadWriter = new BinaryWriter(threadEntry);
            threadWriter.WriteVarUInt((ulong)threadIndex);
            writeThreadOptionalData(threadWriter);

            writer.Write((ushort)threadEntry.Length);
            writer.Write(threadEntry.GetBuffer(), 0, (int)threadEntry.Length);
        }

        public static void WriteThreadEntryName(this BinaryWriter writer, string name)
        {
            writer.Write((byte)1 /* Name */);
            writer.WriteVarUIntPrefixedUTF8String(name);
        }

        public static void WriteThreadEntryProcessId(this BinaryWriter writer, long processId)
        {
            writer.Write((byte)2 /* OSProcessId */);
            writer.WriteVarUInt((ulong)processId);
        }

        public static void WriteThreadEntryThreadId(this BinaryWriter writer, long threadId)
        {
            writer.Write((byte)3 /* ThreadId */);
            writer.WriteVarUInt((ulong)threadId);
        }

        public static void WriteThreadEntryKeyValue(this BinaryWriter writer, string key, string value)
        {
            writer.Write((byte)4 /* KeyValue */);
            writer.WriteVarUIntPrefixedUTF8String(key);
            writer.WriteVarUIntPrefixedUTF8String(value);
        }

        public static void WriteRemoveThreadBlock(this BinaryWriter writer, Action<BinaryWriter> writeThreadEntries)
        {
            writer.WriteBlockV6OrGreater(7 /* RemoveThread */, writeThreadEntries);
        }



        public static void WriteRemoveThreadEntry(this BinaryWriter writer, long threadIndex, int sequenceNumber)
        {
            writer.WriteVarUInt((ulong)threadIndex);
            writer.WriteVarUInt((uint)sequenceNumber);
        }

        public static void WriteEndObject(this BinaryWriter writer)
        {
            writer.Write(1); // null tag
        }

        /// <summary>Writes one stack-block entry: the byte length followed by leaf-to-root instruction pointers.</summary>
        public static void WriteStackPayload(this BinaryWriter writer, params ulong[] addresses)
        {
            writer.Write(addresses.Length * sizeof(ulong));
            foreach (ulong address in addresses)
            {
                writer.Write(address);
            }
        }

        /// <summary>Writes a Universal.System ProcessMapping payload with no file offset or metadata.</summary>
        public static void WriteProcessMappingPayload(this BinaryWriter writer, ulong id, ulong startAddress, ulong endAddress, string fileName)
        {
            writer.WriteVarUInt(id);
            writer.WriteVarUInt(startAddress);
            writer.WriteVarUInt(endAddress);
            writer.WriteVarUInt(0); // FileOffset
            writer.WriteUInt16PrefixedUTF8String(fileName);
            writer.WriteVarUInt(0); // MetadataId
        }

        /// <summary>Writes a Universal.System ProcessSymbol payload covering 16 bytes starting at <paramref name="address"/>.</summary>
        public static void WriteProcessSymbolPayload(this BinaryWriter writer, ulong id, ulong mappingId, ulong address, string name)
        {
            writer.WriteVarUInt(id);
            writer.WriteVarUInt(mappingId);
            writer.WriteVarUInt(address);
            writer.WriteVarUInt(address + 0xF);
            writer.WriteUInt16PrefixedUTF8String(name);
        }

        public static void WriteUInt16PrefixedUTF8String(this BinaryWriter writer, string value)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(value);
            writer.Write((ushort)utf8.Length);
            writer.Write(utf8);
        }
    }

    public class V6LabelListBlockWriter
    {
        BinaryWriter _writer;

        public V6LabelListBlockWriter(BinaryWriter writer)
        {
            _writer = writer;
        }

        public void WriteActivityIdLabel(Guid activityId, bool isLastLabel = false)
        {
            byte kind = 1; // ActivityId
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(activityId);
        }

        public void WriteRelatedActivityIdLabel(Guid relatedActivityId, bool isLastLabel = false)
        {
            byte kind = 2; // RelatedActivityId
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(relatedActivityId);
        }

        public void WriteTraceIdLabel(byte[] traceId, bool isLastLabel = false)
        {
            Debug.Assert(traceId.Length == 16);
            byte kind = 3; // TraceId
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(traceId);
        }

        public void WriteSpanIdLabel(ulong spanId, bool isLastLabel = false)
        {
            byte kind = 4; // SpanId
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(spanId);
        }

        public void WriteNameValueStringLabel(string name, string value, bool isLastLabel = false)
        {
            byte kind = 5; // NameValueString
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.WriteVarUIntPrefixedUTF8String(name);
            _writer.WriteVarUIntPrefixedUTF8String(value);
        }

        public void WriteNameValueVarIntLabel(string name, long value, bool isLastLabel = false)
        {
            byte kind = 6; // NameValueVarint
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.WriteVarUIntPrefixedUTF8String(name);
            _writer.WriteVarInt(value);
        }

        public void WriteOpCodeLabel(byte opcode, bool isLastLabel = false)
        {
            byte kind = 7; // OpCode
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(opcode);
        }

        public void WriteKeywordsLabel(ulong keywords, bool isLastLabel = false)
        {
            byte kind = 8; // Keyword
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(keywords);
        }

        public void WriteLevelLabel(byte level, bool isLastLabel = false)
        {
            byte kind = 9; // Level
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(level);
        }

        public void WriteVersionLabel(byte version, bool isLastLabel = false)
        {
            byte kind = 10; // Version
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(version);
        }
    }

    class V6EventBlockWriter
    {
        BinaryWriter _writer;
        bool _useCompressedHeaders;
        WriteEventOptions _lastEventOptions = new WriteEventOptions();
        int _lastPayloadLength;

        public V6EventBlockWriter(BinaryWriter writer, bool useCompressedHeaders)
        {
            _writer = writer;
            _useCompressedHeaders = useCompressedHeaders;
        }

        public void WriteHeader(long minTimestamp = 0, long maxTimestamp = 0)
        {
            _writer.Write((short)20);                               // header size
            _writer.Write((short)(_useCompressedHeaders ? 1 : 0));  // flags
            _writer.Write(minTimestamp);
            _writer.Write(maxTimestamp);
        }

        public void WriteEventBlob(int metadataId, long threadIndex, int sequenceNumber, byte[] payloadBytes)
        {
            WriteEventBlob(metadataId, threadIndex, sequenceNumber, w => w.Write(payloadBytes));
        }

        public void WriteEventBlob(int metadataId, long threadIndex, int sequenceNumber, Action<BinaryWriter> writeEventPayload)
        {
            WriteEventBlob(new WriteEventOptions { MetadataId = metadataId, CaptureThreadIndexOrId = threadIndex, ThreadIndexOrId = threadIndex, SequenceNumber = sequenceNumber }, writeEventPayload);
        }

        public void WriteEventBlob(WriteEventOptions options, Action<BinaryWriter> writeEventPayload)
        {
            MemoryStream payloadBlob = new MemoryStream();
            BinaryWriter payloadWriter = new BinaryWriter(payloadBlob);
            writeEventPayload(payloadWriter);
            int payloadSize = (int)payloadBlob.Length;
            WriteEventHeader(options, payloadSize);
            _writer.Write(payloadBlob.GetBuffer(), 0, payloadSize);
        }

        public void WriteEventHeader(WriteEventOptions options, int payloadLength)
        {
            if(_useCompressedHeaders)
            {
                WriteCompressedEventHeader(options, payloadLength);
            }
            else
            {
                WriteUncompressedEventHeader(options, payloadLength);
            }
        }

        public void WriteUncompressedEventHeader(WriteEventOptions options, int payloadLength)
        {
            _writer.Write(48 /* header size not including this field */ + payloadLength);
            _writer.Write(options.MetadataId | (int)(options.IsSorted ? 0 : 0x80000000));
            _writer.Write(options.SequenceNumber);
            _writer.Write(options.ThreadIndexOrId);
            _writer.Write(options.CaptureThreadIndexOrId);
            _writer.Write(options.ProcNumber);
            _writer.Write(options.StackId);
            _writer.Write(options.Timestamp);
            _writer.Write(options.LabelListId);
            _writer.Write(payloadLength);
        }

        public void WriteCompressedEventHeader(WriteEventOptions options, int payloadLength)
        {
            byte header = 0;
            if (options.MetadataId != _lastEventOptions.MetadataId)
            {
                header |= 0x01;
            }
            if (options.CaptureThreadIndexOrId != _lastEventOptions.CaptureThreadIndexOrId ||
                options.SequenceNumber != _lastEventOptions.SequenceNumber + 1 ||
                options.ProcNumber != _lastEventOptions.ProcNumber)
            {
                header |= 0x02;
            }
            if (options.ThreadIndexOrId != _lastEventOptions.ThreadIndexOrId)
            {
                header |= 0x04;
            }
            if (options.StackId != _lastEventOptions.StackId)
            {
                header |= 0x08;
            }
            if (options.LabelListId != _lastEventOptions.LabelListId)
            {
                header |= 0x10;
            }
            if (options.IsSorted)
            {
                header |= 0x40;
            }
            if( (payloadLength != _lastPayloadLength))
            {
                header |= 0x80;
            }
            _writer.Write(header);
            if ((header & 0x01) != 0)
            {
                _writer.WriteVarUInt((ulong)options.MetadataId);
            }
            if ((header & 0x02) != 0)
            {
                // the cast to uint here is deliberate to force underflow to wrap up to 2^32 rather than 2^64
                _writer.WriteVarUInt((uint)(options.SequenceNumber - _lastEventOptions.SequenceNumber - 1));
                _writer.WriteVarUInt((ulong)options.CaptureThreadIndexOrId);
                _writer.WriteVarUInt((ulong)options.ProcNumber);
            }
            if ((header & 0x04) != 0)
            {
                _writer.WriteVarUInt((ulong)options.ThreadIndexOrId);
            }
            if ((header & 0x08) != 0)
            {
                _writer.WriteVarUInt((ulong)options.StackId);
            }
            _writer.WriteVarUInt((ulong)(options.Timestamp - _lastEventOptions.Timestamp));

            if ((header & 0x10) != 0)
            {
                _writer.WriteVarUInt((ulong)options.LabelListId);
            }
            if ((header & 0x80) != 0)
            {
                _writer.WriteVarUInt((ulong)payloadLength);
            }
            _lastEventOptions = options;
            _lastPayloadLength = payloadLength;
        }
    }
}
