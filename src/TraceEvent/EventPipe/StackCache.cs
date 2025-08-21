using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    class StackCache
    {
        public bool TryGetStack(int stackId, out int stackBytesSize, out IntPtr stackBytes)
        {
            if(_stacks.TryGetValue(stackId, out StackMarker stack))
            {
                stackBytesSize = stack.StackBytesSize;
                stackBytes = stack.StackBytes;
                return true;
            }
            else
            {
                stackBytesSize = 0;
                stackBytes = IntPtr.Zero;
                return false;
            }
        }

        public void ProcessStackBlock(Block block)
        {
            _buffers.Add(block.TakeOwnership());
            SpanReader reader = block.Reader;
            int firstStackId = reader.ReadInt32();
            int countStackIds = reader.ReadInt32();
            int nextStackId = firstStackId;
            while (reader.RemainingBytes.Length > 0)
            {
                StackMarker marker = new StackMarker();
                int stackId = nextStackId++;
                marker.StackBytesSize = reader.ReadInt32();
                // This is safe because the span is backed by FixedBuffer and it can't move
                // The StackCache will keep ownership of the buffer until it is flushed
                marker.StackBytes = reader.UnsafeGetFixedReadPointer();
                reader.ReadBytes(marker.StackBytesSize);
                _stacks.Add(stackId, marker);
            }
            Debug.Assert(nextStackId == firstStackId + countStackIds);
        }

        public void Flush()
        {
            _stacks.Clear();
            foreach(FixedBuffer buffer in _buffers)
            {
                ((IDisposable)buffer).Dispose();
            }
            _buffers.Clear();
        }

        struct StackMarker
        {
            public int StackBytesSize;
            public IntPtr StackBytes;
        }

        Dictionary<int, StackMarker> _stacks = new Dictionary<int, StackMarker>();
        List<FixedBuffer> _buffers = new List<FixedBuffer>();
    }
}
