namespace System.IO.Compression2
{
    using Diagnostics;

    internal class DeflateInput
    {
        internal byte[] Buffer { get; set; }

        internal int Count { get; set; }

        internal int StartIndex { get; set; }

        internal void ConsumeBytes(int n)
        {
            Debug.Assert(n <= Count, "Should use more bytes than what we have in the buffer");
            StartIndex += n;
            Count -= n;
            Debug.Assert(StartIndex + Count <= Buffer.Length, "Input buffer is in invalid state!");
        }

        internal InputState DumpState()
        {
            InputState savedState;
            savedState.count = Count;
            savedState.startIndex = StartIndex;
            return savedState;
        }

        internal void RestoreState(InputState state)
        {
            Count = state.count;
            StartIndex = state.startIndex;
        }

        internal struct InputState
        {
            internal int count;
            internal int startIndex;
        }
    }
}