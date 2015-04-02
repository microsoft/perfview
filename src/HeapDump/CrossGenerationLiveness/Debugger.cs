#if CROSS_GENERATION_LIVENESS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;

using DebugClient = Microsoft.Diagnostics.CrossGenerationLiveness.IDebugClient;

namespace Microsoft.Diagnostics.CrossGenerationLiveness
{
    /// <summary>
    /// Thin wrapper over the native debugger interface.
    /// </summary>
    internal sealed class Debugger
    {
        private DebuggerCallbacks _Callbacks;
        private DebuggerOutputCallbacks _OutputCallbacks;
        private DataTarget _Target;
        private TextWriter _Log;

        public Debugger(
            DataTarget target,
            TextWriter log)
        {
            _Target = target;
            _Callbacks = new DebuggerCallbacks(this);
            _Log = log;
            _OutputCallbacks = new DebuggerOutputCallbacks();
            DebugClient.SetEventCallbacks(_Callbacks);
            DebugClient.SetOutputCallbacks(_OutputCallbacks);
        }

        /// <summary>
        /// The attached target process.
        /// </summary>
        public DataTarget Target
        {
            get { return _Target; }
        }

        public DebugClient DebugClient
        {
            get { return (DebugClient)_Target.DebuggerInterface; }
        }

        public IDebugControl DebugControl
        {
            get { return (IDebugControl)DebugClient; }
        }

        public IDebugDataSpaces DebugDataSpaces
        {
            get { return (IDebugDataSpaces)DebugClient; }
        }

        public IDebugSymbols DebugSymbols
        {
            get { return (IDebugSymbols)DebugClient; }
        }

        public DebuggerCallbacks Callbacks
        {
            get { return _Callbacks; }
        }

        /// <summary>
        /// Execute a debugger command.
        /// </summary>
        public string Execute(string command)
        {
            StringBuilder outputText = _OutputCallbacks.OutputText;
            DEBUG_OUTPUT mask = _OutputCallbacks.OutputMask;
            outputText.Clear();
            _OutputCallbacks.OutputMask = DEBUG_OUTPUT.NORMAL | DEBUG_OUTPUT.SYMBOLS | DEBUG_OUTPUT.ERROR | DEBUG_OUTPUT.WARNING;

            _Log.WriteLine("Executing Debugger Command: " + command);

            string result = null;
            try
            {
                int hr = DebugControl.Execute(DEBUG_OUTCTL.ALL_CLIENTS, command, DEBUG_EXECUTE.DEFAULT);
                if (hr < 0)
                {
                    outputText.Append(string.Format("Command encountered an error.  HRESULT={0:X8}", hr));
                }

                result = _OutputCallbacks.OutputText.ToString();
            }
            finally
            {
                _OutputCallbacks.OutputMask = mask;
                outputText.Clear();
            }

            _Log.WriteLine("Debugger Command Result:");
            _Log.WriteLine(result);

            return result;
        }

        /// <summary>
        /// Evaluate the input expression into a memory address.
        /// </summary>
        private bool EvaluateExpression(string expression, out ulong result)
        {
            result = 0;
            DEBUG_VALUE debugValue;
            uint remainderIndex;
            int hr = DebugControl.Evaluate(expression, IntPtr.Size == 4 ? DEBUG_VALUE_TYPE.INT32 : DEBUG_VALUE_TYPE.INT64, out debugValue, out remainderIndex);
            if (hr == 0)
            {
                result = IntPtr.Size == 4 ? debugValue.I32 : debugValue.I64;
            }

            return hr == 0;
        }

        /// <summary>
        /// Read memory starting at the specified address until the buffer is full.
        /// </summary>
        private bool ReadVirtualMemory(ulong address, Byte[] buffer)
        {
            uint bytesRead;
            int hr = DebugDataSpaces.ReadVirtual(address, buffer, (uint)buffer.Length, out bytesRead);
            if (hr == 0 && bytesRead == buffer.Length)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Evaluate the input expression and read out the result as an int.
        /// </summary>
        public bool Evaluate(string expression, out int result)
        {
            result = 0;
            ulong exprResult;
            if (!EvaluateExpression(expression, out exprResult))
            {
                return false;
            }
            

            // Allocate a buffer to read into.
            Byte[] buffer = new Byte[4];
            bool readResult = ReadVirtualMemory(exprResult, buffer);
            if (!readResult)
            {
                return false;
            }

            result = BitConverter.ToInt32(buffer, 0);
            return true;
        }

        /// <summary>
        /// Evaluate the input expression and read out the result as a uint.
        /// </summary>
        public bool Evaluate(string expression, bool isPointer, out uint result)
        {
            return Evaluate(expression, isPointer, 0, out result);
        }

        /// <summary>
        /// Evaluate the input expression and read out the result as a uint.
        /// </summary>
        public bool Evaluate(string expression, bool isPointer, uint offset, out uint result)
        {
            result = 0;
            ulong exprResult;

            // Evaluate the expression.  NOTE: If the expression is a pointer to a uint,
            // then the result is a uint**.  Otherwise, it is a uint*.
            if(!EvaluateExpression(expression, out exprResult))
            {
                return false;
            }

            Byte[] buffer = new Byte[IntPtr.Size];
            bool readResult;

            // Determine if we need to treat the value of exprResult as a uint**.
            if (isPointer)
            {
                // Deference exprResult once, so that it is now uint*.
                readResult = ReadVirtualMemory(exprResult, buffer);
                if (!readResult)
                {
                    return false;
                }
                exprResult = IntPtr.Size == 4 ? BitConverter.ToUInt32(buffer, 0) : BitConverter.ToUInt64(buffer, 0);
                exprResult += offset;
            }

            // Dereference the pointer so that we get the uint value.
            Byte[] uintBuffer = new Byte[4];
            readResult = ReadVirtualMemory(exprResult, uintBuffer);
            if (!readResult)
            {
                return false;
            }
            result = BitConverter.ToUInt32(uintBuffer, 0);
            return true;
        }
    }

    internal class HandleExceptionEventArgs : EventArgs
    {
        private Debugger _Debugger;
        private EXCEPTION_RECORD64 _Exception;
        private uint _FirstChance;

        public HandleExceptionEventArgs(
            Debugger debugger,
            EXCEPTION_RECORD64 exception,
            uint firstChance)
        {
            _Debugger = debugger;
            _Exception = exception;
            _FirstChance = firstChance;
        }

        public Debugger Debugger
        {
            get { return _Debugger; }
        }

        public EXCEPTION_RECORD64 Exception
        {
            get { return _Exception; }
        }

        public uint FirstChance
        {
            get { return _FirstChance; }
        }
    }
}
#endif