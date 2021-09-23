using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FastSerialization
{
    public class SegmentedMemoryStreamReader
    {
         const int BlockCopyCapacity = 10 * 1024 * 1024;

        /// <summary>
        /// Create a IStreamReader (reads binary data) from a given byte buffer
        /// </summary>
        public SegmentedMemoryStreamReader(SegmentedList<byte> data, SerializationConfiguration config = null) : this(data, 0, data.Count, config) { }
        /// <summary>
        /// Create a IStreamReader (reads binary data) from a given subregion of a byte buffer 
        /// </summary>
        public SegmentedMemoryStreamReader(SegmentedList<byte> data, long start, long length, SerializationConfiguration config = null)
        {
            SerializationConfiguration = config ?? new SerializationConfiguration();

            if (SerializationConfiguration.StreamLabelWidth == StreamLabelWidth.FourBytes)
            {
                readLabel = () =>
                {
                    return (StreamLabel)(uint)ReadInt32();
                };
                sizeOfSerializedStreamLabel = 4;
            }
            else
            {
                readLabel = () =>
                {
                    return (StreamLabel)(ulong)ReadInt64();
                };
                sizeOfSerializedStreamLabel = 8;
            }

            bytes = new SegmentedList<byte>(65_536, length);
            bytes.AppendFrom(data, start, length);
            position = start;
            endPosition = length;
        }

        /// <summary>
        /// The total length of bytes that this reader can read.  
        /// </summary>
        public virtual long Length { get { return endPosition; } }

        #region implemenation of IStreamReader
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public byte ReadByte()
        {
            if (position >= endPosition)
            {
                Fill(1);
            }

            return bytes[position++];
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public short ReadInt16()
        {
            if (position + sizeof(short) > endPosition)
            {
                Fill(sizeof(short));
            }

            int ret = bytes[position] + (bytes[position + 1] << 8);
            position += sizeof(short);
            return (short)ret;
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public int ReadInt32()
        {
            if (position + sizeof(int) > endPosition)
            {
                Fill(sizeof(int));
            }

            int ret = bytes[position] + ((bytes[position + 1] + ((bytes[position + 2] + (bytes[position + 3] << 8)) << 8)) << 8);
            position += sizeof(int);
            return ret;
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public long ReadInt64()
        {
            uint low = (uint)ReadInt32();
            uint high = (uint)ReadInt32();
            return (long)((((ulong)high) << 32) + low);        // TODO find the most efficient way of doing this. 
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public string ReadString()
        {
            int len = ReadInt32();          // Expect first a character inclusiveCountRet.  -1 means null.
            if (len < 0)
            {
                Debug.Assert(len == -1);
                return null;
            }

            if (sb == null)
            {
                sb = new StringBuilder(len);
            }

            sb.Length = 0;

            Debug.Assert(len < Length);
            while (len > 0)
            {
                int b = ReadByte();
                if (b < 0x80)
                {
                    sb.Append((char)b);
                }
                else if (b < 0xE0)
                {
                    // TODO test this for correctness
                    b = (b & 0x1F);
                    b = b << 6 | (ReadByte() & 0x3F);
                    sb.Append((char)b);
                }
                else
                {
                    // TODO test this for correctness
                    b = (b & 0xF);
                    b = b << 6 | (ReadByte() & 0x3F);
                    b = b << 6 | (ReadByte() & 0x3F);

                    sb.Append((char)b);
                }
                --len;
            }
            return sb.ToString();
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public StreamLabel ReadLabel() => readLabel();

        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public virtual void Goto(StreamLabel label)
        {
            Debug.Assert(label != StreamLabel.Invalid);

            if (SerializationConfiguration.StreamLabelWidth == StreamLabelWidth.FourBytes)
            {
                Debug.Assert((long)label <= int.MaxValue);
                position = (uint)label;
            }
            else
            {
                position = (long)label;
            }
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public virtual StreamLabel Current
        {
            get
            {
                if (SerializationConfiguration.StreamLabelWidth == StreamLabelWidth.FourBytes)
                {
                    return (StreamLabel)(uint)position;
                }
                else
                {
                    return (StreamLabel)position;
                }
            }
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public virtual void GotoSuffixLabel()
        {
            Goto((StreamLabel)(Length - sizeOfSerializedStreamLabel));
            Goto(ReadLabel());
        }
        /// <summary>
        /// Dispose pattern
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Dispose pattern
        /// </summary>
        protected virtual void Dispose(bool disposing) { }

        /// <summary>
        /// Returns the SerializationConfiguration for this stream reader.
        /// </summary>
        internal SerializationConfiguration SerializationConfiguration { get; private set; }
        #endregion

        #region private 
        internal /*protected*/ virtual void Fill(int minBytes)
        {
            throw new Exception("Streamreader read past end of buffer");
        }
        internal /*protected*/  SegmentedList<byte> bytes;
        internal /*protected*/  long position;
        internal /*protected*/  long endPosition;
        private StringBuilder sb;
        private Func<StreamLabel> readLabel;
        private readonly int sizeOfSerializedStreamLabel;
        #endregion
    }
}

