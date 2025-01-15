using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression2;
using System.Text;

namespace ETLStackBrowse
{
    [Serializable]
    public class ByteWindow
    {
        public static Encoder en = System.Text.ASCIIEncoding.ASCII.GetEncoder();
        public static Decoder de = System.Text.ASCIIEncoding.ASCII.GetDecoder();

        public byte[] buffer;   // buffer where the data actually lives.  
        public int[] fields;    // start positions of all fields in the line
        public int fieldsLen;   // Number of valid entries in 'fields'
        public int ib;          // pointer into 'buffer' at start of string.
        public int len;         // length of string

        public ByteWindow()
        {
            buffer = null;
            fields = null;
            ib = 0;
            len = 0;
        }

        public ByteWindow(string s)
        {
            buffer = ByteWindow.MakeBytes(s);
            fields = null;
            ib = 0;
            len = buffer.Length;
        }

        public ByteWindow(byte[] bytes)
        {
            buffer = bytes;
            fields = null;
            ib = 0;
            len = buffer.Length;
        }

        public ByteWindow(ByteWindow b)
        {
            buffer = b.buffer;
            fields = b.fields;
            ib = b.ib;
            len = b.len;
        }

        public ByteWindow(ByteWindow b, int fld)
        {
            buffer = b.buffer;
            fields = b.fields;
            ib = b.ib;
            len = b.len;

            Field(fld);
        }

        public ByteWindow Assign(ByteWindow b)
        {
            buffer = b.buffer;
            fields = b.fields;
            ib = b.ib;
            len = b.len;

            return this;
        }

        public ByteWindow Assign(ByteWindow b, int fld)
        {
            buffer = b.buffer;
            fields = b.fields;
            ib = fields[fld];
            len = fields[fld + 1] - ib - 1;

            return this;
        }

        public ByteWindow Assign(byte[] b)
        {
            buffer = b;
            fields = null;
            ib = 0;
            len = b.Length;

            return this;
        }

        public ByteWindow Assign(String s)
        {
            return Assign(MakeBytes(s));
        }

        public ByteWindow Field(int fld)
        {
            ib = fields[fld];
            len = fields[fld + 1] - ib - 1;
            return this;
        }

        public ByteWindow Truncate(byte b)
        {
            for (int i = 0; i < len; i++)
            {
                if (buffer[ib + i] == b)
                {
                    len = i;
                    return this;
                }
            }

            return this;
        }

        public long GetLong(int fld)
        {
            byte[] buffer = this.buffer;
            int ib = fields[fld];

            while (buffer[ib] == (byte)' ')
            {
                ib++;
            }

            long t = 0;

            if (buffer[ib] == '0' && buffer[ib + 1] == 'x')
            {
                ib += 2;
                for (; ; )
                {
                    if (buffer[ib] >= '0' && buffer[ib] <= '9')
                    {
                        t *= 16;
                        t += (uint)(buffer[ib] - '0');
                        ib++;
                        continue;
                    }

                    if (buffer[ib] >= 'a' && buffer[ib] <= 'f')
                    {
                        t *= 16;
                        t += (uint)(buffer[ib] - 'a' + 10);
                        ib++;
                        continue;
                    }

                    return t;
                }
            }

            while (buffer[ib] >= '0' && buffer[ib] <= '9')
            {
                t *= 10;
                t += buffer[ib] - '0';
                ib++;
            }

            return t;
        }

        public long GetLong()
        {
            byte[] buffer = this.buffer;
            int ib = this.ib;
            int i = 0;

            while (buffer[ib + i] == (byte)' ')
            {
                i++;
            }

            long t = 0;

            if (i + 2 < len && buffer[ib + i] == '0' && buffer[ib + i + 1] == 'x')
            {
                i += 2;
                for (; ; )
                {
                    if (buffer[ib + i] >= '0' && buffer[ib + i] <= '9')
                    {
                        t *= 16;
                        t += (uint)(buffer[ib] - '0');
                        ib++;
                        continue;
                    }

                    if (buffer[ib + i] >= 'a' && buffer[ib + i] <= 'f')
                    {
                        t *= 16;
                        t += (uint)(buffer[ib + i] - 'a' + 10);
                        ib++;
                        continue;
                    }

                    return t;
                }
            }

            for (; i < len; i++)
            {
                byte b = buffer[ib + i];
                if (b >= '0' && b <= '9')
                {
                    t *= 10;
                    t += b - '0';
                }
                else if (b == ',')
                {
                    continue;
                }
                else
                {
                    break;
                }
            }

            return t;
        }

        public ulong GetHex(int fld)
        {
            byte[] buffer = this.buffer;
            int ib = fields[fld];

            while (buffer[ib] == (byte)' ')
            {
                ib++;
            }

            if (buffer[ib] == '0' && buffer[ib + 1] == 'x')
            {
                ib += 2;
            }

            ulong t = 0;

            for (; ; )
            {
                if (buffer[ib] >= '0' && buffer[ib] <= '9')
                {
                    t *= 16;
                    t += (uint)(buffer[ib] - '0');
                    ib++;
                    continue;
                }

                if (buffer[ib] >= 'a' && buffer[ib] <= 'f')
                {
                    t *= 16;
                    t += (uint)(buffer[ib] - 'a' + 10);
                    ib++;
                    continue;
                }

                break;
            }


            return t;
        }

        public int GetInt(int fld)
        {
            byte[] buffer = this.buffer;
            int ib = fields[fld];

            while (buffer[ib] == (byte)' ')
            {
                ib++;
            }

            int t = 0;

            while (buffer[ib] >= '0' && buffer[ib] <= '9')
            {
                t *= 10;
                t += buffer[ib] - '0';
                ib++;
            }

            return t;
        }

        public int GetInt()
        {
            byte[] buffer = this.buffer;
            int ib = this.ib;

            while (buffer[ib] == (byte)' ')
            {
                ib++;
            }

            int t = 0;

            while (buffer[ib] >= '0' && buffer[ib] <= '9')
            {
                t *= 10;
                t += buffer[ib] - '0';
                ib++;
            }

            return t;
        }

        public ByteWindow Trim()
        {
            while (len > 0 && buffer[ib] == ' ')
            {
                ib++;
                len--;
            }

            while (len > 0 && buffer[ib + len - 1] == ' ')
            {
                len--;
            }

            return this;
        }

        public static char[] chars = new char[10240];

        public string GetString()
        {
            int cch = de.GetChars(buffer, ib, len, chars, 0);
            return new String(chars, 0, cch);
        }

        public static string MakeString(byte[] bytes)
        {
            int cch = de.GetChars(bytes, 0, bytes.Length, chars, 0);
            return new String(chars, 0, cch);
        }

        public static byte[] MakeBytes(String s)
        {
            char[] ca = s.ToCharArray();
            int byteCount = en.GetByteCount(ca, 0, ca.Length, true);
            byte[] bytes = new byte[byteCount];
            en.GetBytes(ca, 0, ca.Length, bytes, 0, true);
            return bytes;
        }

        public static int CompareBytes(byte[] b1, byte[] b2, bool caseInsensitive)
        {
            int i = 0;
            for (i = 0; i < b1.Length; i++)
            {
                if (i == b2.Length)
                {
                    return 1;
                }

                byte c1 = b1[i];
                byte c2 = b2[i];
                if (caseInsensitive)
                {
                    if ('a' <= c1 && c1 <= 'z')
                    {
                        c1 -= ('a' - 'A');
                    }

                    if ('a' <= c2 && c2 <= 'z')
                    {
                        c2 -= ('a' - 'A');
                    }
                }

                if (c1 < c2)
                {
                    return -1;
                }

                if (c1 > c2)
                {
                    return 1;
                }
            }

            if (i < b2.Length)
            {
                return -1;
            }

            return 0;
        }

        public bool Contains(byte[] substr)
        {
            if (substr.Length == 0)
            {
                return true;
            }

            if (substr.Length > len)
            {
                return false;
            }

            for (int i = 0; i < len; i++)
            {
                int j = 0;
                for (; j < substr.Length; j++)
                {
                    if (buffer[i + ib + j] != substr[j])
                    {
                        break;
                    }
                }

                if (j == substr.Length)
                {
                    return true;
                }
            }

            return false;
        }

        public bool StartsWith(byte[] prefix)
        {
            if (prefix.Length > len)
            {
                return false;
            }

            for (int i = prefix.Length - 1; i >= 0; i--)
            {
                if (buffer[i + ib] != prefix[i])
                {
                    return false;
                }
            }

            return true;
        }

        public byte[] Clone()
        {
            byte[] bytes = new byte[len];
            for (int i = 0; i < len; i++)
            {
                bytes[i] = buffer[ib + i];
            }

            return bytes;
        }

        public int Compare(ByteWindow t)
        {
            int i = 0;
            for (i = 0; i < len; i++)
            {
                if (i == t.len)
                {
                    return 1;
                }

                if (buffer[ib + i] < t.buffer[t.ib + i])
                {
                    return -1;
                }

                if (buffer[ib + i] > t.buffer[t.ib + i])
                {
                    return 1;
                }
            }

            if (i < t.len)
            {
                return -1;
            }

            return 0;
        }

        /// <summary>
        /// Useful in debugging.  Not intended for normal use
        /// </summary>
        public override string ToString()
        {
            return GetString();
        }

        /// <summary>
        /// Useful in debugging.  Not intended for normal use
        /// </summary>
        public string Dump
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.Append('(');
                ByteWindow b = this;
                for (int i = 0; i < fieldsLen && i < fields.Length - 1; i++)
                {
                    b.Field(i);
                    if (b.len < 0)
                    {
                        sb.Append("ERROR_FIELD");
                        break;
                    }
                    sb.Append(i).Append("='").Append(b.GetString()).Append("' ");
                }
                sb.Append(')');
                return sb.ToString();
            }
        }
    }

    public class BigStream
    {
        private BackgroundReader bgReader = null;
        private const int cbBufferSize = 1 << 16;  // 64k

        private FileStream src;
        private int cb;
        private int ib = 0;
        private byte[] stm_buffer = new byte[cbBufferSize];
        private short[] stm_offsets = new short[cbBufferSize / 2];
        private int stm_c_offsets = 0;
        private int stm_i_offset = 0;
        private int[] fields = new int[100];
        private System.Threading.Thread bgThread = null;


        ~BigStream()
        {
            Close();
        }

        public BigStream(string filename)
        {
            src = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

            if (filename.EndsWith(".csvz"))
            {
                InitArchiveSettings();
            }

            bgReader = new BackgroundReader(src, offsets);

            bgThread = new System.Threading.Thread(() =>
            {
                bgReader.DoWork();
            });

            bgThread.Start();

            bgReader.Seek(0);

            cb = ReadBuffer(stm_buffer, 0, stm_buffer.Length);
            ib = 0;
        }

        public long Position
        {
            get
            {
                return pos;
            }
            set
            {
                bgReader.Seek(value);

                pos = value;
                cb = ReadBuffer(stm_buffer, 0, stm_buffer.Length);

                ib = 0;
                posNext = pos;
            }
        }

        private List<long> offsets = new List<long>();

        private int ReadBuffer(byte[] buffer, int ib, int len)
        {
            var b = bgReader.ReadBlock();

            // for the gzip case, the main thread should do the line breaking
            if (offsets.Count != 0)
            {
                LineBreaker.ComputeLineAndFieldOffsets(b);
            }

            int cb = b.cb;
            posNext = b.position;

            if (b.cb > 0)
            {
                Array.Copy(b.buffer, 0, buffer, ib, b.cb);
                Array.Copy(b.offsets, 0, stm_offsets, 0, b.cFields);

                stm_c_offsets = b.cFields;
                stm_i_offset = 0;
            }

            bgReader.Free(b);

            return cb;
        }

        private void InitArchiveSettings()
        {
            var stmlen = src.Length;
            var r0 = MakeReader(src, stmlen - 8, 8);
            var pos = r0.ReadInt64();

            var r1 = MakeReader(src, pos, (int)(stmlen - pos));

            var c = r1.ReadInt32();

            for (int i = 0; i < c; i++)
            {
                var offset = r1.ReadInt64();
                offsets.Add(offset);
            }
        }

        private BinaryReader MakeReader(FileStream stm, long offset, int count)
        {
            var b = new byte[count];
            stm.Seek(offset, SeekOrigin.Begin);
            int c = stm.Read(b, 0, count);
            var m = new MemoryStream(b, 0, c);
            return new BinaryReader(m);
        }

        private long pos = 0;
        private long posNext = 0;

        public bool ReadLine(ByteWindow b)
        {
            pos = posNext;

            for (; ; )
            {
                int fld = 0;

                while (stm_i_offset < stm_c_offsets)
                {
                    ib = stm_offsets[stm_i_offset];

                    if (ib >= 0)
                    {
                        if (fld < fields.Length)
                        {
                            fields[fld++] = ib;
                        }

                        stm_i_offset++;
                    }
                    else
                    {
                        ib = -ib;

                        b.ib = fields[0];
                        b.len = (ib - b.ib);
                        b.fields = fields;
                        b.buffer = stm_buffer;
                        b.fieldsLen = fld;

                        // note the fields offset array goes one past the number of fields so that length can always be computed
                        // for a legal field by taking the offset of field[n+1] minus field[n]

                        // include just one terminator character, either the /r or the /n
                        if (ib > 0 && stm_buffer[ib - 1] == '\r')
                        {
                            if (fld < fields.Length)
                            {
                                fields[fld++] = ib;  // use the /r if there is one
                            }
                        }
                        else
                        {
                            if (fld < fields.Length)
                            {
                                fields[fld++] = ib + 1; // else use the /n
                            }

                            b.len++;
                        }

                        stm_i_offset++;

                        posNext += b.len + 1;
                        return true;
                    }
                }

                int cb = ReadBuffer(stm_buffer, 0, stm_buffer.Length);

                if (cb == 0)
                {
                    b.fields = null;
                    b.buffer = null;
                    b.ib = 0;
                    b.len = 0;
                    b.fieldsLen = 0;

                    return false;
                }
            }
        }

        // For debugging only gets 256 characters at a particular position.
        private string ToString(int pos)
        {
            int cch = ByteWindow.de.GetChars(stm_buffer, pos, Math.Min(256, stm_buffer.Length - pos), ByteWindow.chars, 0);
            return new String(ByteWindow.chars, 0, cch);
        }

        internal void Close()
        {
            if (bgReader != null)
            {
                bgReader.Exit();
            }

            bgReader = null;

            if (src != null)
            {
                src.Close();
            }

            src = null;
        }
    }

    internal class BackgroundReader
    {
        private const int buffersize = 32000;
        private FileStream src = null;
        private List<long> offsets = null;

        public BackgroundReader(FileStream src, List<long> offsets)
        {
            this.src = src;
            this.offsets = offsets;
        }

        public class Block
        {
            public long position;
            public int cb;
            public int cFields;
            public short[] offsets = new short[buffersize / 2];
            public byte[] buffer = new byte[buffersize];
        }

        private enum CmdCode
        {
            Seek,
            Stop,
            Free,
            Exit
        }

        private class Cmd
        {
            public CmdCode code;
            public long position;
            public Block block;
        }

        private LinkedList<Cmd> cmds = new LinkedList<Cmd>();
        private LinkedList<Block> blocksReady = new LinkedList<Block>();
        private LinkedList<Block> blocksFree = new LinkedList<Block>();
        private System.Threading.Semaphore semaphoreToBg = new System.Threading.Semaphore(0, 10000);
        private System.Threading.Semaphore semaphoreFromBg = new System.Threading.Semaphore(0, 10000);
        private const int blocks_readahead = 16;
        private byte[] bufferScratch = new byte[buffersize];
        private int cbScratch = 0;
        private long currentSegmentOffset = 0;
        private int currentSegment = 0;

        public void DoWork()
        {
            for (int i = 0; i < blocks_readahead; i++)
            {
                blocksFree.AddLast(new Block());
            }

            bool fReading = false;
            GZipStream gstm = null;

            for (; ; )
            {
                semaphoreToBg.WaitOne();

                Cmd cmd = null;

                lock (cmds)
                {
                    cmd = cmds.First.Value;
                    cmds.RemoveFirst();
                }

                switch (cmd.code)
                {

                    case CmdCode.Exit:
                        if (gstm != null)
                        {
                            gstm.Close();
                        }
                        return;

                    case CmdCode.Free:
                        blocksFree.AddLast(cmd.block);
                        break;

                    case CmdCode.Seek:
                        if (offsets.Count == 0)
                        {
                            src.Seek(cmd.position, SeekOrigin.Begin);
                            currentSegment = 0;
                            currentSegmentOffset = cmd.position;
                            cbScratch = 0;
                        }
                        else
                        {
                            int seg = (int)(cmd.position >> 32);
                            int offset = (int)(cmd.position & 0xffffffff);

                            if (gstm != null)
                            {
                                gstm.Close();
                                gstm = null;
                            }

                            currentSegment = (int)seg;
                            src.Seek(offsets[currentSegment], SeekOrigin.Begin);
                            currentSegmentOffset = offset;
                            cbScratch = 0;

                            gstm = new GZipStream(src, CompressionMode.Decompress, true);

                            while (offset > 0)
                            {
                                int cb = Math.Min(offset, bufferScratch.Length);
                                cb = gstm.Read(bufferScratch, 0, cb);
                                offset -= cb;
                            }
                        }
                        fReading = true;
                        break;

                    case CmdCode.Stop:
                        fReading = false;
                        break;
                }

                if (!fReading)
                {
                    continue;
                }

                if (offsets.Count == 0)
                {
                    // this is not an archive
                    while (blocksFree.Count > 0)
                    {
                        Block b = blocksFree.First.Value;
                        blocksFree.RemoveFirst();

                        int cbBuffer = 0;
                        int cbRead = 0;

                        if (cbScratch > 0)
                        {
                            Array.Copy(bufferScratch, b.buffer, cbScratch);
                            cbBuffer = cbScratch;
                            cbScratch = 0;
                        }

                        cbRead = src.Read(b.buffer, cbBuffer, b.buffer.Length - cbBuffer);

                        cbBuffer += cbRead;

                        ProcessAndTransfer(b, cbBuffer);

                        if (cbRead == 0)
                        {
                            fReading = false;
                            break;
                        }
                    }
                }
                else
                {
                    // this is an archive...

                    while (blocksFree.Count > 0)
                    {
                        Block b = blocksFree.First.Value;
                        blocksFree.RemoveFirst();

                        if (gstm == null)
                        {
                            ProcessAndTransfer(b, 0);
                            fReading = false;
                            break;
                        }

                        int cbBuffer = 0;
                        int cbRead = 0;

                        if (cbScratch > 0)
                        {
                            Array.Copy(bufferScratch, b.buffer, cbScratch);
                            cbBuffer = cbScratch;
                            cbScratch = 0;
                        }

                        while (cbBuffer < b.buffer.Length)
                        {
                            cbRead = gstm.Read(b.buffer, cbBuffer, b.buffer.Length - cbBuffer);

                            cbBuffer += cbRead;

                            if (cbBuffer == b.buffer.Length)
                            {
                                break;
                            }

                            if (cbRead == 0)
                            {
                                break;
                            }
                        }

                        if (cbBuffer > 0)
                        {
                            ProcessAndTransfer(b, cbBuffer);
                        }
                        else
                        {
                            // put it back, since we didn't use it
                            blocksFree.AddFirst(b);
                        }

                        if (cbRead != 0)
                        {
                            continue;
                        }

                        currentSegment++;
                        currentSegmentOffset = 0;
                        if (currentSegment < offsets.Count)
                        {
                            src.Seek(offsets[currentSegment], SeekOrigin.Begin);
                            gstm.Recycle();
                        }
                        else
                        {
                            gstm.Close();
                            gstm = null;
                        }
                    }
                }
            }
        }

        private long seekPosition = -1;

        public void Seek(long position)
        {
            var cmd = new Cmd();
            cmd.code = CmdCode.Seek;
            cmd.position = position;
            seekPosition = position;

            lock (cmds)
            {
                cmds.AddLast(cmd);
            }

            semaphoreToBg.Release();
        }

        public void Stop()
        {
            var cmd = new Cmd();
            cmd.code = CmdCode.Stop;

            lock (cmds)
            {
                cmds.AddLast(cmd);
            }

            semaphoreToBg.Release();
        }

        public void Exit()
        {
            var cmd = new Cmd();
            cmd.code = CmdCode.Exit;

            lock (cmds)
            {
                cmds.AddLast(cmd);
            }

            semaphoreToBg.Release();
        }

        public void Free(Block b)
        {
            var cmd = new Cmd();
            cmd.code = CmdCode.Free;
            cmd.block = b;

            lock (cmds)
            {
                cmds.AddLast(cmd);
            }

            semaphoreToBg.Release();
        }

        private long lengthStatistic;
        private long countStatistic;

        public Block ReadBlock()
        {
            for (; ; )
            {
                Block b;

                semaphoreFromBg.WaitOne();

                lock (blocksReady)
                {
                    b = blocksReady.First.Value;
                    blocksReady.RemoveFirst();
                }

                if (seekPosition == -1)
                {
                    countStatistic++;
                    lengthStatistic += blocksReady.Count;
                    return b;
                }

                if (b.position != seekPosition)
                {
                    Free(b);
                    continue;
                }

                countStatistic++;
                lengthStatistic += blocksReady.Count;

                seekPosition = -1;
                return b;
            }
        }

        private void ProcessAndTransfer(Block b, int cbBuffer)
        {
            if (cbBuffer > 0)
            {
                byte[] buffer = b.buffer;
                int ibLastNewline = cbBuffer - 1;

                while (buffer[ibLastNewline] != '\n' && ibLastNewline > 0)
                {
                    ibLastNewline--;
                }

                int ibCopy = ibLastNewline + 1;
                int ib = 0;
                while (ibCopy < cbBuffer)
                {
                    bufferScratch[ib++] = buffer[ibCopy++];
                }

                cbScratch = ib;

                b.position = (((long)currentSegment) << 32) + currentSegmentOffset;
                b.cb = ibLastNewline + 1;
                currentSegmentOffset += b.cb;

                // for the non-gzip case, the background thread should do the line breaking
                if (offsets.Count == 0)
                {
                    LineBreaker.ComputeLineAndFieldOffsets(b);
                }
            }
            else
            {
                b.position = -1;
                b.cb = 0;
                b.offsets[0] = 0;
            }

            lock (blocksReady)
            {
                blocksReady.AddLast(b);
            }

            semaphoreFromBg.Release(1);
        }
    }

    internal static class LineBreaker
    {
        public static void ComputeLineAndFieldOffsets(BackgroundReader.Block b)
        {
            int cb = b.cb;
            int fld = 0;
            int ib = 0;
            byte[] buffer = b.buffer;
            short[] fields = b.offsets;

            fields[fld++] = (short)ib;

            // there must still be a newline or we wouldn't be here
            while (ib < cb)
            {
                switch ((char)buffer[ib])
                {
                    case ',':
                        fields[fld++] = (short)(ib + 1);
                        ib++;
                        break;


                    case '\n':
                        fields[fld++] = (short)-ib;
                        fields[fld++] = (short)(ib + 1);
                        ib++;
                        break;

                    case '!':
                    case '"':
                        // If we see a ! assume it is the ! in Image!Function
                        // Since Function can itself contain commas (C++ templates, for example)
                        // we need to parse it more carefully

                        ib = ParseCarefully(buffer, ib);
                        break;

                    default:
                        ib++;
                        break;
                }
            }

            b.cFields = fld;
        }

        // On entry, buffer[ib] points to the ! in Image!Function
        // Need to parse through Function, matching angle brackets and parentheses as we go,
        // until we reach the end. The point is that the undecordated
        // function name may contain a comma and we don't want to be fooled into thinking
        // it's a field delimiter.
        // Returns true if there is more to go on this line. false if '\n' was reached.
        private static int ParseCarefully(byte[] buffer, int ib)
        {
            // This is pretty naive. No attempt made to pair up brackets.
            int nOpenAngleBrackets = 0;
            int nOpenParentheses = 0;
            bool bInQuote = (buffer[ib] == '"');

            for (++ib; ; ++ib)
            {
                switch ((char)buffer[ib])
                {
                    case '\n':
                        return ib;

                    case '"':           // Quotes don't nest for XPERF (no way to escape them)
                        if (bInQuote)
                        {
                            ib++;
                            return ib;
                        }
                        break;

                    case '<':
                        nOpenAngleBrackets++;
                        break;

                    case '>':
                        nOpenAngleBrackets--;
                        break;

                    case '(':
                        nOpenParentheses++;
                        break;

                    case ')':
                        nOpenParentheses--;
                        break;

                    case ',':
                    case ' ':
                        if (nOpenParentheses == 0 && nOpenAngleBrackets == 0 && !bInQuote)
                        {
                            return ib;
                        }
                        break;
                }
            }
        }
    }
}

