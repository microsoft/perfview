namespace System.IO.Compression2
{
    using IO;
    using Security.Permissions;

    public class GZipStream : Stream
    {
        private DeflateStream deflateStream;
        private readonly CompressionMode mode;

        public GZipStream(Stream stream, CompressionMode mode)
            : this(stream, mode, false)
        {
        }

        public GZipStream(Stream stream, CompressionMode mode, bool leaveOpen)
        {
            this.mode = mode;

            deflateStream = new DeflateStream(stream, mode, leaveOpen);

            if (mode == CompressionMode.Compress) {
                IFileFormatWriter writeCommand = new GZipFormatter();
                deflateStream.SetFileFormatWriter(writeCommand);
            }
            else {
                IFileFormatReader readCommand = new GZipDecoder();
                deflateStream.SetFileFormatReader(readCommand);
            }
        }

        public override bool CanRead => deflateStream != null && deflateStream.CanRead;

        public override bool CanWrite => deflateStream != null && deflateStream.CanWrite;

        public override bool CanSeek => deflateStream != null && deflateStream.CanSeek;

        public Stream BaseStream => deflateStream?.BaseStream;

        public override long Length
        {
            get
            {
                throw new NotSupportedException("NotSupported");
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException("NotSupported");
            }

            set
            {
                throw new NotSupportedException("NotSupported");
            }
        }

        public void Recycle()
        {
            deflateStream.Recycle();

            if (mode == CompressionMode.Compress)
            {
                IFileFormatWriter writeCommand = new GZipFormatter();
                deflateStream.SetFileFormatWriter(writeCommand);
            }
            else
            {
                IFileFormatReader readCommand = new GZipDecoder();
                deflateStream.SetFileFormatReader(readCommand);
            }
        }

        public override void Flush()
        {
            if (deflateStream == null) {
                throw new ObjectDisposedException(null, "ObjectDisposed_StreamClosed");
            }

            deflateStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("NotSupported");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("NotSupported");
        }

        [HostProtection(ExternalThreading = true)]
        public override IAsyncResult BeginRead(byte[] array, int offset, int count, AsyncCallback asyncCallback, object asyncState)
        {
            if (deflateStream == null) {
                throw new InvalidOperationException("ObjectDisposed_StreamClosed");
            }
            return deflateStream.BeginRead(array, offset, count, asyncCallback, asyncState);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            if (deflateStream == null) {
                throw new InvalidOperationException("ObjectDisposed_StreamClosed");
            }
            return deflateStream.EndRead(asyncResult);
        }

        [HostProtection(ExternalThreading = true)]
        public override IAsyncResult BeginWrite(byte[] array, int offset, int count, AsyncCallback asyncCallback, object asyncState)
        {
            if (deflateStream == null) {
                throw new InvalidOperationException("ObjectDisposed_StreamClosed");
            }
            return deflateStream.BeginWrite(array, offset, count, asyncCallback, asyncState);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            if (deflateStream == null) {
                throw new InvalidOperationException("ObjectDisposed_StreamClosed");
            }
            deflateStream.EndWrite(asyncResult);
        }
        public override int Read(byte[] array, int offset, int count)
        {
            if (deflateStream == null) {
                throw new ObjectDisposedException(null, "ObjectDisposed_StreamClosed");
            }

            return deflateStream.Read(array, offset, count);
        }

        public override void Write(byte[] array, int offset, int count)
        {
            if (deflateStream == null) {
                throw new ObjectDisposedException(null, "ObjectDisposed_StreamClosed");
            }

            deflateStream.Write(array, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            try {
                if (disposing) {
                    deflateStream?.Close();
                }

                deflateStream = null;
            }
            finally {
                base.Dispose(disposing);
            }
        }
    }
}
