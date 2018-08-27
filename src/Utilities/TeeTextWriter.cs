using System.IO;

namespace Microsoft.Diagnostics.Utilities
{
    /// <summary>
    /// This class returns a stream, which when written to, will write to two other streams.  
    /// </summary>
    public sealed class TeeTextWriter : TextWriter
    {
        public TeeTextWriter(TextWriter stream1, TextWriter stream2)
        {
            m_stream1 = stream1;
            m_stream2 = stream2;
        }
        public TextWriter Stream1 { get { return m_stream1; } }
        public TextWriter Stream2 { get { return m_stream2; } }

        public override System.Text.Encoding Encoding { get { return m_stream1.Encoding; } }
        public override void Write(char value)
        {
            lock (this)
            {
                m_stream1.Write(value); m_stream2.Write(value);
            }
        }
        public override void Write(char[] buffer, int index, int count)
        {
            lock (this)
            {
                Write(new string(buffer, index, count));
            }
        }
        public override void Write(string value)
        {
            lock (this)
            {
                m_stream1.Write(value); m_stream2.Write(value);
            }
        }
        public override void Flush()
        {
            lock (this)
            {
                m_stream1.Flush(); m_stream2.Flush();
            }
        }
        protected override void Dispose(bool disposing)
        {
            lock (this)
            {
                if (disposing) { m_stream1.Dispose(); m_stream2.Dispose(); }
            }
        }

        private TextWriter m_stream1;
        private TextWriter m_stream2;
    }
}