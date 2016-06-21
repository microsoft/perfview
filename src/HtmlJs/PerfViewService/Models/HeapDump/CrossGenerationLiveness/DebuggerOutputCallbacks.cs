#if CROSS_GENERATION_LIVENESS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Diagnostics.Runtime.Interop;

namespace Microsoft.Diagnostics.CrossGenerationLiveness
{
    /// <summary>
    /// Implements debugger callbacks needed to support getting the output of debugger commands.
    /// </summary>
    internal sealed class DebuggerOutputCallbacks : IDebugOutputCallbacks
    {
        public const DEBUG_OUTPUT DefaultOutputMask = 
            DEBUG_OUTPUT.NORMAL |
            DEBUG_OUTPUT.SYMBOLS |
            DEBUG_OUTPUT.ERROR | 
            DEBUG_OUTPUT.WARNING | 
            DEBUG_OUTPUT.VERBOSE;

        private DEBUG_OUTPUT _OutputMask = DefaultOutputMask;

        private StringBuilder _Output = new StringBuilder();

        public DEBUG_OUTPUT OutputMask
        {
            get { return _OutputMask; }
            set { _OutputMask = value; }
        }

        public StringBuilder OutputText
        {
            get { return _Output; }
        }

        public int Output(DEBUG_OUTPUT Mask, string Text)
        {
            if (_Output != null && ((Mask & _OutputMask) != 0))
            {
                _Output.Append(Text);
            }

            return (int)DEBUG_STATUS.GO;
        }
    }
}
#endif