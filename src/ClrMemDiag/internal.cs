using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime
{
    class ReadVirtualStream : Stream
    {
        byte[] m_tmp;
        long m_pos;
        long m_disp;
        long m_len;
        IDataReader m_dataReader;

        public ReadVirtualStream(IDataReader dataReader, long displacement, long len)
        {
            m_dataReader = dataReader;
            m_disp = displacement;
            m_len = len;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position
        {
            get
            {
                return m_pos;
            }
            set
            {
                m_pos = value;
                if (m_pos > m_len)
                    m_pos = m_len;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset == 0)
            {
                int read;
                if (m_dataReader.ReadMemory((ulong)(m_pos + m_disp), buffer, count, out read))
                    return read;

                return 0;
            }
            else
            {
                if (m_tmp == null || m_tmp.Length < count)
                    m_tmp = new byte[count];

                int read;
                if (!m_dataReader.ReadMemory((ulong)(m_pos + m_disp), m_tmp, count, out read))
                    return 0;

                Buffer.BlockCopy(m_tmp, 0, buffer, offset, read);
                return read;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    m_pos = offset;
                    break;

                case SeekOrigin.End:
                    m_pos = m_len + offset;
                    if (m_pos > m_len)
                        m_pos = m_len;
                    break;

                case SeekOrigin.Current:
                    m_pos += offset;
                    if (m_pos > m_len)
                        m_pos = m_len;
                    break;
            }

            return m_pos;
        }

        public override void SetLength(long value)
        {
            m_len = value;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }
    }

    class NullSymbolNotification : ISymbolNotification
    {
        public void FoundSymbolInCache(string localPath)
        {
        }

        public void ProbeFailed(string url)
        {
        }

        public void FoundSymbolOnPath(string url)
        {
        }

        public void DownloadProgress(int bytesDownloaded)
        {
        }

        public void DownloadComplete(string localPath, bool requiresDecompression)
        {
        }

        public void DecompressionComplete(string localPath)
        {
        }
    }


#if _TRACING

    class TraceDataReader : IDataReader
    {
        private IDataReader m_reader;
        private StreamWriter m_file;

        public TraceDataReader(IDataReader reader)
        {
            m_reader = reader;
            m_file = File.CreateText("datareader.txt");
            m_file.AutoFlush = true;
            m_file.WriteLine(reader.GetType().ToString());
        }

        public void Close()
        {
            m_file.WriteLine("Close");
            m_reader.Close();
        }

        public void Flush()
        {
            m_file.WriteLine("Flush");
            m_reader.Flush();
        }

        public Architecture GetArchitecture()
        {
            var arch = m_reader.GetArchitecture();
            m_file.WriteLine("GetArchitecture - {0}", arch);
            return arch;
        }

        public uint GetPointerSize()
        {
            var ptrsize = m_reader.GetPointerSize();
            m_file.WriteLine("GetPointerSize - {0}", ptrsize);
            return ptrsize;
        }

        public IList<ModuleInfo> EnumerateModules()
        {
            var modules = m_reader.EnumerateModules();

            int hash = 0;
            foreach (var module in modules)
                hash ^= module.FileName.ToLower().GetHashCode();

            m_file.WriteLine("EnumerateModules - {0} {1:x}", modules.Count, hash);
            return modules;
        }

        public void GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            m_reader.GetVersionInfo(baseAddress, out version);
            m_file.WriteLine("GetVersionInfo - {0:x} {1}", baseAddress, version.ToString());
        }

        public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            bool result = m_reader.ReadMemory(address, buffer, bytesRequested, out bytesRead);

            StringBuilder sb = new StringBuilder();
            int count = bytesRead > 8 ? 8 : bytesRead;
            for (int i = 0; i < count; ++i)
                sb.Append(buffer[i].ToString("x"));

            m_file.WriteLine("ReadMemory {0}- {1:x} {2} {3}", result ? "" : "failed ", address, bytesRead, sb.ToString());

            return result;
        }

        public ulong GetThreadTeb(uint thread)
        {
            ulong teb = m_reader.GetThreadTeb(thread);
            m_file.WriteLine("GetThreadTeb - {0:x} {1:x}", thread, teb);
            return teb;
        }

        public IEnumerable<uint> EnumerateAllThreads()
        {
            List<uint> threads = new List<uint>(m_reader.EnumerateAllThreads());

            bool first = true;
            StringBuilder sb = new StringBuilder();
            foreach (uint id in threads)
            {
                if (!first)
                    sb.Append(", ");
                first = false;
                sb.Append(id.ToString("x"));
            }

            m_file.WriteLine("Threads: {0} {1}", threads.Count, sb.ToString());
            return threads;
        }

        public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
        {
            bool result = m_reader.VirtualQuery(addr, out vq);
            m_file.WriteLine("VirtualQuery {0}: {1:x} {2:x} {3}", result ? "" : "failed ", addr, vq.BaseAddress, vq.Size);
            return result;
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
        {
            bool result = m_reader.GetThreadContext(threadID, contextFlags, contextSize, context);
            m_file.WriteLine("GetThreadContext - {0}", result);
            return result;
        }
    }
#endif



    unsafe class MemoryReader
    {
        #region Variables
        protected ulong m_currPageStart;
        protected int m_currPageSize;
        protected int m_ptrSize;
        protected byte[] m_data;
        byte[] m_ptr;
        protected IDataReader m_dataReader;
        protected int m_cacheSize;
        #endregion

        public MemoryReader(IDataReader dataReader, int cacheSize)
        {
            m_data = new byte[cacheSize];
            m_dataReader = dataReader;
            m_ptrSize = (int)m_dataReader.GetPointerSize();
            m_ptr = new byte[m_ptrSize];
            m_cacheSize = cacheSize;
        }

        public bool ReadDword(ulong addr, out uint value)
        {
            uint size = 4;
            // Is addr on the current page?  If not read the page of memory addr is on.
            // If this fails, we will fall back to a raw read out of the process (which
            // is what MisalignedRead does).
            if ((addr < m_currPageStart) || (addr >= m_currPageStart + (uint)m_currPageSize))
                if (!MoveToPage(addr))
                    return MisalignedRead(addr, out value);

            // If MoveToPage succeeds, we MUST be on the right page.
            Debug.Assert(addr >= m_currPageStart);

            // However, the amount of data requested may fall off of the page.  In that case,
            // fall back to MisalignedRead.
            ulong offset = addr - m_currPageStart;
            if (offset + size > (uint)m_currPageSize)
                return MisalignedRead(addr, out value);

            // If we reach here we know we are on the right page of memory in the cache, and
            // that the read won't fall off of the end of the page.
            value = BitConverter.ToUInt32(m_data, (int)offset);
            return true;
        }

        public bool ReadDword(ulong addr, out int value)
        {
            uint tmp = 0;
            bool res = ReadDword(addr, out tmp);

            value = (int)tmp;
            return res;
        }

        internal bool TryReadPtr(ulong addr, out ulong value)
        {
            if ((m_currPageStart <= addr) && (addr - m_currPageStart < (uint)m_currPageSize))
            {
                ulong offset = addr - m_currPageStart;
                fixed (byte* b = &m_data[offset])
                    if (m_ptrSize == 4)
                        value = *((uint*)b);
                    else
                        value = *((ulong*)b);

                return true;
            }

            return MisalignedRead(addr, out value);
        }

        internal bool TryReadDword(ulong addr, out uint value)
        {
            if ((m_currPageStart <= addr) && (addr - m_currPageStart < (uint)m_currPageSize))
            {
                ulong offset = addr - m_currPageStart;
                value = BitConverter.ToUInt32(m_data, (int)offset);
                fixed (byte* b = &m_data[offset])
                    value = *((uint*)b);
                return true;
            }

            return MisalignedRead(addr, out value);
        }

        internal bool TryReadDword(ulong addr, out int value)
        {
            if ((m_currPageStart <= addr) && (addr - m_currPageStart < (uint)m_currPageSize))
            {
                ulong offset = addr - m_currPageStart;
                fixed (byte* b = &m_data[offset])
                    value = *((int*)b);

                return true;
            }

            return MisalignedRead(addr, out value);
        }

        public bool ReadPtr(ulong addr, out ulong value)
        {
            // Is addr on the current page?  If not read the page of memory addr is on.
            // If this fails, we will fall back to a raw read out of the process (which
            // is what MisalignedRead does).
            if ((addr < m_currPageStart) || (addr - m_currPageStart > (uint)m_currPageSize))
                if (!MoveToPage(addr))
                    return MisalignedRead(addr, out value);

            // If MoveToPage succeeds, we MUST be on the right page.
            Debug.Assert(addr >= m_currPageStart);

            // However, the amount of data requested may fall off of the page.  In that case,
            // fall back to MisalignedRead.
            ulong offset = addr - m_currPageStart;
            if (offset + (uint)m_ptrSize >= (uint)m_currPageSize)
            {
                if (!MoveToPage(addr))
                    return MisalignedRead(addr, out value);

                offset = 0;
            }

            // If we reach here we know we are on the right page of memory in the cache, and
            // that the read won't fall off of the end of the page.
            fixed (byte* b = &m_data[offset])
                if (m_ptrSize == 4)
                    value = *((uint*)b);
                else
                    value = *((ulong*)b);

            return true;
        }

        virtual public void EnsureRangeInCache(ulong addr)
        {
            if (!Contains(addr))
            {
                MoveToPage(addr);
            }
        }


        public bool Contains(ulong addr)
        {
            return ((m_currPageStart <= addr) && (addr - m_currPageStart < (uint)m_currPageSize));
        }

        #region Private Functions
        private bool MisalignedRead(ulong addr, out ulong value)
        {
            int size = 0;
            bool res = m_dataReader.ReadMemory(addr, m_ptr, m_ptrSize, out size);
            fixed (byte* b = m_ptr)
                if (m_ptrSize == 4)
                    value = *((uint*)b);
                else
                    value = *((ulong*)b);
            return res;
        }

        private bool MisalignedRead(ulong addr, out uint value)
        {
            byte[] tmp = new byte[4];
            int size = 0;
            bool res = m_dataReader.ReadMemory(addr, tmp, tmp.Length, out size);
            value = BitConverter.ToUInt32(tmp, 0);
            return res;
        }

        private bool MisalignedRead(ulong addr, out int value)
        {
            byte[] tmp = new byte[4];
            int size = 0;
            bool res = m_dataReader.ReadMemory(addr, tmp, tmp.Length, out size);
            value = BitConverter.ToInt32(tmp, 0);
            return res;
        }

        virtual protected bool MoveToPage(ulong addr)
        {
            return ReadMemory(addr);
        }

        protected virtual bool ReadMemory(ulong addr)
        {
            m_currPageStart = addr;
            bool res = m_dataReader.ReadMemory(m_currPageStart, m_data, m_cacheSize, out m_currPageSize);

            if (!res)
            {
                m_currPageStart = 0;
                m_currPageSize = 0;
            }

            return res;
        }
        #endregion
    }

    class AsyncMemoryReader : MemoryReader
    {
        protected AsyncMemoryReadResult m_result;

        
        public AsyncMemoryReader(IDataReader dataReader, int cacheSize)
            :base(dataReader, cacheSize)
        {
        }

        public override void EnsureRangeInCache(ulong addr)
        {
            if (!Contains(addr))
            {
                if (!m_dataReader.CanReadAsync)
                {
                    ReadMemory(addr);
                }
                else if (!Contains(addr, m_result))
                {
                    ReadMemory(addr);
                    ReadAsync(addr+(uint)m_cacheSize);
                }
            }
        }

        protected override bool MoveToPage(ulong addr)
        {
            if (Contains(addr, m_result))
            {
                m_result.Complete.WaitOne();

                m_data = m_result.Result;
                m_currPageSize = m_result.BytesRead;
                m_currPageStart = m_result.Address;

                if (m_result.BytesRequested == m_result.BytesRead)
                    ReadAsync(m_result.Address + (uint)m_result.BytesRead);
                else
                    m_result = null;

                return true;
            }

            bool res = ReadMemory(addr);
            if (m_cacheSize == m_currPageSize)
                ReadAsync(addr + (uint)m_cacheSize);
            return res;
        }


        private void ReadAsync(ulong addr)
        {
            m_result = m_dataReader.ReadMemoryAsync(addr, m_cacheSize * 10);
        }

        protected override bool ReadMemory(ulong addr)
        {
            return base.ReadMemory(addr);
        }


        private bool Contains(ulong addr, AsyncMemoryReadResult m_result)
        {
            return m_result != null && Contains(addr, m_result.Address, m_result.BytesRequested);
        }

        private bool Contains(ulong addr, ulong start, int len)
        {
            return start <= addr && addr < start + (uint)len;
        }
    }


    class GCDesc
    {
        static readonly int GCDescSize = IntPtr.Size * 2;

        #region Variables
        byte[] m_data;
        #endregion

        #region Functions
        public GCDesc(byte[] data)
        {
            m_data = data;
        }

        public void WalkObject(ulong addr, ulong size, MemoryReader cache, Action<ulong, int> refCallback)
        {
            Debug.Assert(size >= (ulong)IntPtr.Size);

            int series = GetNumSeries();
            int highest = GetHighestSeries();
            int curr = highest;

            if (series > 0)
            {
                int lowest = GetLowestSeries();
                do
                {
                    ulong ptr = addr + GetSeriesOffset(curr);
                    ulong stop = (ulong)((long)ptr + (long)GetSeriesSize(curr) + (long)size);

                    while (ptr < stop)
                    {
                        ulong ret;
                        if (cache.ReadPtr(ptr, out ret) && ret != 0)
                            refCallback(ret, (int)(ptr - addr));

                        ptr += (ulong)IntPtr.Size;
                    }

                    curr -= GCDescSize;
                } while (curr >= lowest);
            }
            else
            {
                ulong ptr = addr + GetSeriesOffset(curr);

                while (ptr < (addr + size - (ulong)IntPtr.Size))
                {
                    for (int i = 0; i > series; i--)
                    {
                        uint nptrs = GetPointers(curr, i);
                        uint skip = GetSkip(curr, i);

                        ulong stop = ptr + (ulong)(nptrs * IntPtr.Size);
                        do
                        {
                            ulong ret;
                            if (cache.ReadPtr(ptr, out ret) && ret != 0)
                                refCallback(ret, (int)(ptr - addr));

                            ptr += (ulong)IntPtr.Size;
                        } while (ptr < stop);

                        ptr += skip;
                    }
                }
            }
        }
        #endregion

        #region Private Functions
        private uint GetPointers(int curr, int i)
        {
            int offset = i * IntPtr.Size;
            if (IntPtr.Size == 4)
                return BitConverter.ToUInt16(m_data, curr + offset);
            else
                return BitConverter.ToUInt32(m_data, curr + offset);
        }

        private uint GetSkip(int curr, int i)
        {
            int offset = i * IntPtr.Size + IntPtr.Size / 2;
            if (IntPtr.Size == 4)
                return BitConverter.ToUInt16(m_data, curr + offset);
            else
                return BitConverter.ToUInt32(m_data, curr + offset);
        }

        private int GetSeriesSize(int curr)
        {
            if (IntPtr.Size == 4)
                return (int)BitConverter.ToInt32(m_data, curr);
            else
                return (int)BitConverter.ToInt64(m_data, curr);
        }

        private ulong GetSeriesOffset(int curr)
        {
            ulong offset;
            if (IntPtr.Size == 4)
                offset = BitConverter.ToUInt32(m_data, curr + IntPtr.Size);
            else
                offset = BitConverter.ToUInt64(m_data, curr + IntPtr.Size);

            return offset;
        }

        int GetHighestSeries()
        {
            return m_data.Length - IntPtr.Size * 3;
        }

        int GetLowestSeries()
        {
            return m_data.Length - ComputeSize(GetNumSeries());
        }

        static private int ComputeSize(int series)
        {
            return IntPtr.Size + series * IntPtr.Size * 2;
        }

        private int GetNumSeries()
        {
            if (IntPtr.Size == 4)
                return (int)BitConverter.ToInt32(m_data, m_data.Length - IntPtr.Size);
            else
                return (int)BitConverter.ToInt64(m_data, m_data.Length - IntPtr.Size);
        }
        #endregion
    }
}