using Microsoft.Diagnostics.Tracing.Stacks;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Diagnostics.Tracing.StackSources
{
    /// <summary>
    /// A WTReader knows how to read a text file from the cdb (windbg) WT command output
    /// </summary>
    internal class WTStackSource : InternStackSource
    {
        public WTStackSource(string fileName)
        {
            using (var file = File.OpenText(fileName))
            {
                Read(file);
            }
        }
        public WTStackSource(TextReader reader)
        {
            Read(reader);
        }

        #region private
        private struct WTStackElem
        {
            // to do the transformation we need to remember the complete stack for each call depth as well as
            // the number of instructions so far we have for that call depth.   
            public StackSourceCallStackIndex CallStackIndex;
            public int ExclInstrSoFar;

            /// <summary>
            /// When WT encounters tail call stubs it keeps the depth the same but changes the name.   
            /// we need the original call stack for to keep track of 'ExclInstrSoFar' but from a 
            /// users's perspective we clump the instructions into the original routine (before the tail call).
            /// FirstCallStackIndex is this 'original routine'.   TODO: Not clear this is the right answer....
            /// </summary>
            public StackSourceCallStackIndex FirstCallStackIndex;

#if DEBUG
            public override string ToString()
            {
                return string.Format("<Elem CallStack=\"{0}\" FirstCallStack=\"{1}\"ExclInstrSoFar=\"{2}\"/>",
                    CallStackIndex, FirstCallStackIndex, ExclInstrSoFar);
            }
#endif
        }

        private void Read(TextReader reader)
        {
            // TODO this is relatively inefficient.  
            var regEx = new Regex(@"^\s*(\d+)\s*(\d+)\s*\[\s*(\d+)\s*\]\s*(\S*?)!?(.*)");

            var stack = new GrowableArray<WTStackElem>();
            WTStackElem elem = new WTStackElem();
            long time = 0;
            var sample = new StackSourceSample(this);

            for (; ; )
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }

                var match = regEx.Match(line);
                if (match.Success)
                {
                    // Parse the line. 
                    int excInstrSoFar = int.Parse(match.Groups[1].Value);
                    int depth = int.Parse(match.Groups[3].Value);
                    string module = match.Groups[4].Value;
                    string method = match.Groups[5].Value;

                    // Form the name for this line 
                    var moduleIndex = Interner.ModuleIntern(module);
                    var frameIndex = Interner.FrameIntern(method, moduleIndex);

                    // Get the parent stack for this line 
                    var parent = StackSourceCallStackIndex.Invalid;
                    if (depth > 0)
                    {
                        parent = stack[depth - 1].FirstCallStackIndex;    // TODO handle out of range
                    }

                    // Form the stack for this entry 
                    var callStackIndex = Interner.CallStackIntern(frameIndex, parent);

                    int exclInstr;                              // Number of instructions executed on this line 
                    int extra = stack.Count - depth;            // The number of frames we need to pop off (including me)
                    if (extra > 0)
                    {
                        // We returned from one or more methods OR we have not left the current method
                        // 
                        elem = stack[depth];

                        // We expect to return to the same method we were at at this depth.  
                        if (callStackIndex == elem.CallStackIndex)
                        {
                            exclInstr = excInstrSoFar - elem.ExclInstrSoFar;    // We are continuing the function 
                        }
                        else
                        {
                            // We are tail-calling to another routine.   
                            exclInstr = excInstrSoFar;
                            elem.CallStackIndex = callStackIndex;
                        }

                        // Pop off all the frames we returned from
                        Debug.Assert(exclInstr >= 0);
                        stack.RemoveRange(depth, extra);
                    }
                    else
                    {
                        // Means we are adding a new frame (we called someone) 
                        Debug.Assert(extra == 0);       // We always add only one more frame (e.g. we never go from depth 2 to 4) 
                        elem.CallStackIndex = callStackIndex;
                        elem.FirstCallStackIndex = callStackIndex;
                        exclInstr = excInstrSoFar;
                    }
                    elem.ExclInstrSoFar = excInstrSoFar;
                    stack.Add(elem);

                    time += exclInstr;

                    sample.Metric = exclInstr;
                    sample.TimeRelativeMSec = time - exclInstr;
                    sample.StackIndex = elem.FirstCallStackIndex;
                    AddSample(sample);
                }
            }
            Interner.DoneInterning();
        }
        #endregion
    }
}