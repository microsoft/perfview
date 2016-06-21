#if CROSS_GENERATION_LIVENESS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Diagnostics.Runtime.Interop;

namespace Microsoft.Diagnostics.CrossGenerationLiveness
{
    /// <summary>
    /// Implements debugger callbacks for various events.
    /// </summary>
    internal sealed class DebuggerCallbacks : IDebugEventCallbacks
    {
        private Debugger m_Debugger;

        public DebuggerCallbacks(
            Debugger debugger)
        {
            m_Debugger = debugger;
        }

        /// <summary>
        /// An event that is fired whenever an exception in the debuggee needs to be handled.
        /// </summary>
        public event EventHandler<HandleExceptionEventArgs> HandleExceptionEvent;

        public int GetInterestMask(out DEBUG_EVENT Mask)
        {
            Mask = DEBUG_EVENT.EXCEPTION;
            return 0; // S_OK
        }

        public int Breakpoint(IDebugBreakpoint Bp)
        {
            return (int) DEBUG_STATUS.GO;
        }

        public int Exception(ref EXCEPTION_RECORD64 Exception, uint FirstChance)
        {
            EventHandler<HandleExceptionEventArgs> exceptionHandler = HandleExceptionEvent;
            if (HandleExceptionEvent != null)
            {
                exceptionHandler(this, new HandleExceptionEventArgs(m_Debugger, Exception, FirstChance));
            }

            return (FirstChance == 1) ? (int)DEBUG_STATUS.GO : (int)DEBUG_STATUS.BREAK;
        }

        public int ChangeDebuggeeState(DEBUG_CDS Flags, ulong Argument)
        {
            throw new NotImplementedException();
        }

        public int ChangeEngineState(DEBUG_CES Flags, ulong Argument)
        {
            throw new NotImplementedException();
        }

        public int ExitProcess(uint ExitCode)
        {
            throw new NotImplementedException();
        }

        public int SystemError(uint Error, uint Level)
        {
            throw new NotImplementedException();
        }

        public int ChangeSymbolState(DEBUG_CSS Flags, ulong Argument)
        {
            throw new NotImplementedException();
        }

        public int CreateProcess(ulong ImageFileHandle, ulong Handle, ulong BaseOffset, uint ModuleSize, string ModuleName, string ImageName, uint CheckSum, uint TimeDateStamp, ulong InitialThreadHandle, ulong ThreadDataOffset, ulong StartOffset)
        {
            throw new NotImplementedException();
        }

        public int CreateThread(ulong Handle, ulong DataOffset, ulong StartOffset)
        {
            throw new NotImplementedException();
        }

        public int ExitThread(uint ExitCode)
        {
            throw new NotImplementedException();
        }

        public int LoadModule(ulong ImageFileHandle, ulong BaseOffset, uint ModuleSize, string ModuleName, string ImageName, uint CheckSum, uint TimeDateStamp)
        {
            throw new NotImplementedException();
        }

        public int SessionStatus(DEBUG_SESSION Status)
        {
            throw new NotImplementedException();
        }

        public int UnloadModule(string ImageBaseName, ulong BaseOffset)
        {
            throw new NotImplementedException();
        }
    }
}
#endif