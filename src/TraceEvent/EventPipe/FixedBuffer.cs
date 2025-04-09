using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    internal sealed class FixedBuffer : MemoryManager<byte>
    {
        private IntPtr _pointer;
        private int _length;

        internal FixedBuffer(int length)
        {
            _pointer = Marshal.AllocHGlobal(length);
            _length = length;
        }

        protected override void Dispose(bool disposing)
        {
            Marshal.FreeHGlobal(_pointer);
            _pointer = IntPtr.Zero;
            _length = 0;
        }

        public unsafe override Span<byte> GetSpan()
        {
            return new Span<byte>(_pointer.ToPointer(), _length);
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            throw new NotSupportedException();
        }

        public override void Unpin()
        {
        }
    }
}
