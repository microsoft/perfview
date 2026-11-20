// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FastSerialization;

using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>Identifies a frame list interned in an <see cref="AsyncProfilerCallStacksIndex"/>.</summary>
    internal enum AsyncProfilerCallStackFramesIndex
    {
        /// <summary>No interned frame list.</summary>
        Invalid = -1,
    }

    /// <summary>An interned, leaf-to-root list of captured async frames.</summary>
    /// <remarks>
    /// V1 frames contain state-machine MoveNext body instruction pointers and compiler states.
    /// V2 frames contain continuation instruction pointers without compiler states.
    /// </remarks>
    internal sealed class AsyncProfilerCallStackFrames
    {
        /// <summary>The instrumentation family that emitted these frames.</summary>
        public AsyncCallstackKind Kind { get; }

        /// <summary>The number of captured frames.</summary>
        public int FrameCount
        {
            get
            {
                return _instructionPointers.Length;
            }
        }

        /// <summary>The captured native instruction pointer; zero is a valid captured value.</summary>
        public ulong InstructionPointerAt(int index)
        {
            return _instructionPointers[index];
        }

        /// <summary>The captured V1 compiler state, or zero when no state array is available.</summary>
        public int FrameStateAt(int index)
        {
            if (_frameStates == null)
            {
                return 0;
            }

            return _frameStates[index];
        }

        /// <summary>The registered address in the originating TraceLog, or <see cref="CodeAddressIndex.Invalid"/>
        /// when unavailable or out of range. A valid index does not guarantee a resolved method or module.</summary>
        public CodeAddressIndex CodeAddressAt(int index)
        {
            if (_codeAddresses == null || (uint)index >= (uint)_codeAddresses.Length)
            {
                return CodeAddressIndex.Invalid;
            }

            return _codeAddresses[index];
        }

        #region private

        /// <summary>Whether the slot holds a real frame rather than a zero-IP placeholder.</summary>
        internal bool IsCapturedFrameAt(int index) =>
            CodeAddressAt(index) != CodeAddressIndex.Invalid || InstructionPointerAt(index) != 0;

        internal AsyncProfilerCallStackFrames(
            AsyncCallstackKind kind, ulong[] instructionPointers, int[] frameStates, ProcessIndex processIndex = 0)
        {
            Kind = kind;
            _instructionPointers = instructionPointers;
            _frameStates = frameStates;
            ProcessIndex = processIndex;
        }

        /// <summary>The capture process, used only during conversion and not serialized.</summary>
        internal ProcessIndex ProcessIndex { get; }

        /// <summary>Stores a frame's code-address index during trace conversion.</summary>
        internal void SetCodeAddressAt(int index, CodeAddressIndex codeAddress)
        {
            if (_codeAddresses == null)
            {
                _codeAddresses = new CodeAddressIndex[_instructionPointers.Length];
                for (int i = 0; i < _codeAddresses.Length; i++)
                {
                    _codeAddresses[i] = CodeAddressIndex.Invalid;
                }
            }

            _codeAddresses[index] = codeAddress;
        }

        internal void Write(Serializer serializer)
        {
            serializer.Write((byte)Kind);
            serializer.Write(_instructionPointers.Length);

            for (int i = 0; i < _instructionPointers.Length; i++)
            {
                serializer.Write((long)_instructionPointers[i]);
            }

            if (_frameStates == null)
            {
                serializer.Write(-1);
            }
            else
            {
                serializer.Write(_frameStates.Length);

                for (int i = 0; i < _frameStates.Length; i++)
                {
                    serializer.Write(_frameStates[i]);
                }
            }

            if (_codeAddresses == null)
            {
                serializer.Write(-1);
            }
            else
            {
                serializer.Write(_codeAddresses.Length);

                for (int i = 0; i < _codeAddresses.Length; i++)
                {
                    serializer.Write((int)_codeAddresses[i]);
                }
            }
        }

        internal static AsyncProfilerCallStackFrames Read(Deserializer deserializer)
        {
            var kind = (AsyncCallstackKind)deserializer.ReadByte();
            int frameCount = deserializer.ReadInt();
            var instructionPointers = new ulong[frameCount];

            for (int i = 0; i < frameCount; i++)
            {
                instructionPointers[i] = (ulong)deserializer.ReadInt64();
            }

            int stateCount = deserializer.ReadInt();
            int[] frameStates = null;
            if (stateCount >= 0)
            {
                frameStates = new int[stateCount];
                for (int i = 0; i < stateCount; i++)
                {
                    frameStates[i] = deserializer.ReadInt();
                }
            }

            var frames = new AsyncProfilerCallStackFrames(kind, instructionPointers, frameStates);
            int codeAddressCount = deserializer.ReadInt();
            if (codeAddressCount >= 0)
            {
                var codeAddresses = new CodeAddressIndex[codeAddressCount];

                for (int i = 0; i < codeAddressCount; i++)
                {
                    codeAddresses[i] = (CodeAddressIndex)deserializer.ReadInt();
                }

                frames._codeAddresses = codeAddresses;
            }

            return frames;
        }

        private readonly ulong[] _instructionPointers;
        private readonly int[] _frameStates; // Optional V1 compiler states.
        private CodeAddressIndex[] _codeAddresses; // null until the first address is registered

        #endregion
    }
}
