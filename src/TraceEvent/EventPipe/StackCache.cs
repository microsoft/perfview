using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public void ProcessStackBlock(byte[] stackBlock)
        {
            PinnedBuffer buffer = new PinnedBuffer(stackBlock);
            if(stackBlock.Length < 8)
            {
                Debug.Assert(false, "Bad stack block size");
                return;
            }
            int cursor = 0;
            int firstStackId = BitConverter.ToInt32(stackBlock, cursor);
            cursor += 4;
            int countStackIds = BitConverter.ToInt32(stackBlock, cursor);
            cursor += 4;
            int nextStackId = firstStackId;
            while (cursor < stackBlock.Length)
            {
                StackMarker marker = new StackMarker();
                marker.BackingBuffer = buffer;
                int stackId = nextStackId++;
                marker.StackBytesSize = BitConverter.ToInt32(stackBlock, cursor);
                cursor += 4;
                if (cursor + marker.StackBytesSize <= stackBlock.Length)
                {
                    marker.StackBytes = buffer.PinningHandle.AddrOfPinnedObject() + cursor;
                    cursor += marker.StackBytesSize;
                    _stacks.Add(stackId, marker);
                }
                else
                {
                    Debug.Assert(false, "Stack size exceeds stack block region");
                    break;
                }
            }
            Debug.Assert(nextStackId == firstStackId + countStackIds);
        }

        public void Flush()
        {
            _stacks.Clear();
        }

        struct StackMarker
        {
            public int StackBytesSize;
            public IntPtr StackBytes;
            public PinnedBuffer BackingBuffer;
        }

        Dictionary<int, StackMarker> _stacks = new Dictionary<int, StackMarker>();
    }
}
