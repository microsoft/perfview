namespace System.IO.Compression2
{
    using System.Diagnostics;
    using System.IO;
    using System.Security.Permissions;
    using System.Threading;

    public class DeflateStream : Stream
    {
        internal const int DefaultBufferSize = 40960;
        private const int bufferSize = DefaultBufferSize;

        internal delegate void AsyncWriteDelegate(byte[] array, int offset, int count, bool isAsync);

        private Stream _stream;
        private CompressionMode _mode;
        private bool _leaveOpen;
        private Inflater inflater;
        private Deflater deflater;
        private byte[] buffer;

        private int asyncOperations;
        private readonly AsyncCallback m_CallBack;
        private readonly AsyncWriteDelegate m_AsyncWriterDelegate;

        private IFileFormatWriter formatWriter;
        private bool wroteHeader;

        public DeflateStream(Stream stream, CompressionMode mode)
            : this(stream, mode, false)
        {
        }

        internal void Recycle()
        {
            Array.Clear(buffer, 0, buffer.Length);

            if (deflater != null)
            {
                throw new NotSupportedException("deflating recycle unsupported");
            }

            if (asyncOperations != 0 || m_AsyncWriterDelegate != null)
            {
                throw new NotSupportedException("recycle after async use unsupported");
            }

            inflater.Recycle();

            wroteHeader = false;
        }

        public DeflateStream(Stream stream, CompressionMode mode, bool leaveOpen)
        {
            _stream = stream;
            _mode = mode;
            _leaveOpen = leaveOpen;

            if (_stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            switch (_mode)
            {
                case CompressionMode.Decompress:
                    if (!(_stream.CanRead))
                    {
                        throw new ArgumentException("NotReadableStream", "stream");
                    }
                    inflater = new Inflater();

                    m_CallBack = new AsyncCallback(ReadCallback);
                    break;

                case CompressionMode.Compress:
                    if (!(_stream.CanWrite))
                    {
                        throw new ArgumentException("NotWriteableStream", "stream");
                    }

                    deflater = new Deflater();

                    m_AsyncWriterDelegate = new AsyncWriteDelegate(InternalWrite);
                    m_CallBack = new AsyncCallback(WriteCallback);
                    break;

                default:
                    throw new ArgumentException("ArgumentOutOfRange_Enum", "mode");
            }
            buffer = new byte[bufferSize];
        }

        internal void SetFileFormatReader(IFileFormatReader reader)
        {
            if (reader != null)
            {
                inflater.SetFileFormatReader(reader);
            }
        }

        internal void SetFileFormatWriter(IFileFormatWriter writer)
        {
            if (writer != null)
            {
                formatWriter = writer;
            }
        }

        public override bool CanRead
        {
            get
            {
                if (_stream == null)
                {
                    return false;
                }

                return (_mode == CompressionMode.Decompress && _stream.CanRead);
            }
        }

        public override bool CanWrite
        {
            get
            {
                if (_stream == null)
                {
                    return false;
                }

                return (_mode == CompressionMode.Compress && _stream.CanWrite);
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

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

        public override void Flush()
        {
            if (_stream == null)
            {
                throw new ObjectDisposedException(null, "ObjectDisposed_StreamClosed");
            }
            return;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("NotSupported");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("NotSupported");
        }

        public override int Read(byte[] array, int offset, int count)
        {
            EnsureDecompressionMode();
            ValidateParameters(array, offset, count);

            int bytesRead;
            int currentOffset = offset;
            int remainingCount = count;

            while (true)
            {
                bytesRead = inflater.Inflate(array, currentOffset, remainingCount);
                currentOffset += bytesRead;
                remainingCount -= bytesRead;

                if (remainingCount == 0)
                {
                    break;
                }

                if (inflater.Finished())
                {
                    // if we finished decompressing, we can't have anything left in the outputwindow.
                    Debug.Assert(inflater.AvailableOutput == 0, "We should have copied all stuff out!");
                    break;
                }

                Debug.Assert(inflater.NeedsInput(), "We can only run into this case if we are short of input");

                int bytes = _stream.Read(buffer, 0, buffer.Length);
                if (bytes == 0)
                {
                    break;      //Do we want to throw an exception here?
                }

                inflater.SetInput(buffer, 0, bytes);
            }

            return count - remainingCount;
        }

        private void ValidateParameters(byte[] array, int offset, int count)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException("offset");
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            if (array.Length - offset < count)
            {
                throw new ArgumentException("InvalidArgumentOffsetCount");
            }

            if (_stream == null)
            {
                throw new ObjectDisposedException(null, "ObjectDisposed_StreamClosed");
            }
        }

        private void EnsureDecompressionMode()
        {
            if (_mode != CompressionMode.Decompress)
            {
                throw new InvalidOperationException("CannotReadFromDeflateStream");
            }
        }

        private void EnsureCompressionMode()
        {
            if (_mode != CompressionMode.Compress)
            {
                throw new InvalidOperationException("CannotWriteToDeflateStream");
            }
        }

        [HostProtection(ExternalThreading = true)]
        public override IAsyncResult BeginRead(byte[] array, int offset, int count, AsyncCallback asyncCallback, object asyncState)
        {
            EnsureDecompressionMode();
            if (asyncOperations != 0)
            {
                throw new InvalidOperationException("InvalidBeginCall");
            }
            Interlocked.Increment(ref asyncOperations);

            try
            {
                ValidateParameters(array, offset, count);

                DeflateStreamAsyncResult userResult = new DeflateStreamAsyncResult(
                        this, asyncState, asyncCallback, array, offset, count);
                userResult.isWrite = false;

                // Try to read decompressed data in output buffer
                int bytesRead = inflater.Inflate(array, offset, count);
                if (bytesRead != 0)
                {
                    // If decompression output buffer is not empty, return immediately.
                    // 'true' means we complete synchronously.
                    userResult.InvokeCallback(true, (object)bytesRead);
                    return userResult;
                }

                if (inflater.Finished())
                {
                    // end of compression stream
                    userResult.InvokeCallback(true, (object)0);
                    return userResult;
                }

                // If there is no data on the output buffer and we are not at 
                // the end of the stream, we need to get more data from the base stream
                _stream.BeginRead(buffer, 0, buffer.Length, m_CallBack, userResult);
                userResult.m_CompletedSynchronously &= userResult.IsCompleted;

                return userResult;
            }
            catch
            {
                Interlocked.Decrement(ref asyncOperations);
                throw;
            }
        }

        // callback function for asynchronous reading on base stream
        private void ReadCallback(IAsyncResult baseStreamResult)
        {
            DeflateStreamAsyncResult outerResult = (DeflateStreamAsyncResult)baseStreamResult.AsyncState;
            outerResult.m_CompletedSynchronously &= baseStreamResult.CompletedSynchronously;
            int bytesRead = 0;

            try
            {
                bytesRead = _stream.EndRead(baseStreamResult);

                if (bytesRead <= 0)
                {
                    // This indicates the base stream has received EOF
                    outerResult.InvokeCallback((object)0);
                    return;
                }

                // Feed the data from base stream into decompression engine
                inflater.SetInput(buffer, 0, bytesRead);
                bytesRead = inflater.Inflate(outerResult.buffer, outerResult.offset, outerResult.count);
                if (bytesRead == 0 && !inflater.Finished())
                {
                    // We could have read in head information and didn't get any data.
                    // Read from the base stream again.   
                    // Need to solve recusion.
                    _stream.BeginRead(buffer, 0, buffer.Length, m_CallBack, outerResult);
                }
                else
                {
                    outerResult.InvokeCallback((object)bytesRead);
                }
            }
            catch (Exception exc)
            {
                // Defer throwing this until EndRead where we will likely have user code on the stack.
                outerResult.InvokeCallback(exc);
                return;
            }
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            EnsureDecompressionMode();

            if (asyncOperations != 1)
            {
                throw new InvalidOperationException("InvalidEndCall");
            }

            if (asyncResult == null)
            {
                throw new ArgumentNullException("asyncResult");
            }

            if (_stream == null)
            {
                throw new InvalidOperationException("ObjectDisposed_StreamClosed");
            }

            DeflateStreamAsyncResult myResult = asyncResult as DeflateStreamAsyncResult;

            if (myResult == null)
            {
                throw new ArgumentNullException("asyncResult");
            }

            try
            {
                if (!myResult.IsCompleted)
                {
                    myResult.AsyncWaitHandle.WaitOne();
                }
            }
            finally
            {
                Interlocked.Decrement(ref asyncOperations);
                // this will just close the wait handle
                myResult.Close();
            }

            if (myResult.Result is Exception)
            {
                throw (Exception)(myResult.Result);
            }

            return (int)myResult.Result;
        }

        public override void Write(byte[] array, int offset, int count)
        {
            EnsureCompressionMode();
            ValidateParameters(array, offset, count);
            InternalWrite(array, offset, count, false);
        }

        internal void InternalWrite(byte[] array, int offset, int count, bool isAsync)
        {
            DoMaintenance(array, offset, count);

            int bytesCompressed;

            // compressed the bytes we already passed to the deflater
            while (!deflater.NeedsInput())
            {
                bytesCompressed = deflater.GetDeflateOutput(buffer);
                if (bytesCompressed != 0)
                {
                    DoWrite(buffer, 0, bytesCompressed, isAsync);
                }
            }

            deflater.SetInput(array, offset, count);

            while (!deflater.NeedsInput())
            {
                bytesCompressed = deflater.GetDeflateOutput(buffer);
                if (bytesCompressed != 0)
                {
                    DoWrite(buffer, 0, bytesCompressed, isAsync);
                }
            }
        }

        private void DoWrite(byte[] array, int offset, int count, bool isAsync)
        {
            Debug.Assert(array != null);
            Debug.Assert(count != 0);

            if (isAsync)
            {
                IAsyncResult result = _stream.BeginWrite(array, offset, count, null, null);
                _stream.EndWrite(result);
            }
            else
            {
                _stream.Write(array, offset, count);
            }
        }

        private void DoMaintenance(byte[] array, int offset, int count)
        {
            if (formatWriter == null)
            {
                return;
            }

            if (!wroteHeader && count > 0)
            {
                byte[] b = formatWriter.GetHeader();
                _stream.Write(b, 0, b.Length);
                wroteHeader = true;
            }
            if (count > 0)
            {
                formatWriter.UpdateWithBytesRead(array, offset, count);
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                // Flush on the underlying stream can throw (ex., low disk space)
                if (disposing && _stream != null)
                {
                    Flush();

                    // Need to do close the output stream in compression mode
                    if (_mode == CompressionMode.Compress && _stream != null)
                    {

                        // compress any bytes left.
                        int bytesWritten;
                        while (!deflater.NeedsInput())
                        {
                            bytesWritten = deflater.GetDeflateOutput(buffer);
                            if (bytesWritten != 0)
                            {
                                _stream.Write(buffer, 0, bytesWritten);
                            }
                        }

                        bytesWritten = deflater.Finish(buffer);
                        if (bytesWritten > 0)
                        {
                            DoWrite(buffer, 0, bytesWritten, false);
                        }

                        if (formatWriter != null && wroteHeader)
                        {
                            byte[] b = formatWriter.GetFooter();
                            _stream.Write(b, 0, b.Length);
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    // Attempt to close the stream even if there was an IO error from Flushing.
                    // Note that Stream.Close() can potentially throw here (may or may not be
                    // due to the same Flush error). In this case, we still need to ensure 
                    // cleaning up internal resources, hence the finally block.  
                    if (disposing && !_leaveOpen && _stream != null)
                    {
                        _stream.Close();
                    }
                }
                finally
                {
                    _stream = null;
                    base.Dispose(disposing);
                }
            }
        }


        [HostProtection(ExternalThreading = true)]
        public override IAsyncResult BeginWrite(byte[] array, int offset, int count, AsyncCallback asyncCallback, object asyncState)
        {
            EnsureCompressionMode();
            if (asyncOperations != 0)
            {
                throw new InvalidOperationException("InvalidBeginCall");
            }
            Interlocked.Increment(ref asyncOperations);

            try
            {
                ValidateParameters(array, offset, count);

                DeflateStreamAsyncResult userResult = new DeflateStreamAsyncResult(
                        this, asyncState, asyncCallback, array, offset, count);
                userResult.isWrite = true;

                m_AsyncWriterDelegate.BeginInvoke(array, offset, count, true, m_CallBack, userResult);
                userResult.m_CompletedSynchronously &= userResult.IsCompleted;

                return userResult;
            }
            catch
            {
                Interlocked.Decrement(ref asyncOperations);
                throw;
            }
        }

        // callback function for asynchronous reading on base stream
        private void WriteCallback(IAsyncResult asyncResult)
        {
            DeflateStreamAsyncResult outerResult = (DeflateStreamAsyncResult)asyncResult.AsyncState;
            outerResult.m_CompletedSynchronously &= asyncResult.CompletedSynchronously;

            try
            {
                m_AsyncWriterDelegate.EndInvoke(asyncResult);
            }
            catch (Exception exc)
            {
                // Defer throwing this until EndXxx where we are ensured of user code on the stack.
                outerResult.InvokeCallback(exc);
                return;
            }
            outerResult.InvokeCallback(null);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            EnsureCompressionMode();

            if (asyncOperations != 1)
            {
                throw new InvalidOperationException("InvalidEndCall");
            }

            if (asyncResult == null)
            {
                throw new ArgumentNullException("asyncResult");
            }

            if (_stream == null)
            {
                throw new InvalidOperationException("ObjectDisposed_StreamClosed");
            }

            DeflateStreamAsyncResult myResult = asyncResult as DeflateStreamAsyncResult;

            if (myResult == null)
            {
                throw new ArgumentNullException("asyncResult");
            }

            try
            {
                if (!myResult.IsCompleted)
                {
                    myResult.AsyncWaitHandle.WaitOne();
                }
            }
            finally
            {
                Interlocked.Decrement(ref asyncOperations);
                // this will just close the wait handle
                myResult.Close();
            }

            if (myResult.Result is Exception)
            {
                throw (Exception)(myResult.Result);
            }
        }


        public Stream BaseStream
        {
            get
            {
                return _stream;
            }
        }
    }

}
