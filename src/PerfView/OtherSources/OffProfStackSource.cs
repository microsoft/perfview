using Microsoft.Diagnostics.Tracing.Stacks;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Diagnostics.Tracing.StackSources
{
    /// <summary>
    /// A WTReader knows how to read a text file from the cdb (windbg) WT command output
    /// </summary>
    internal class OffProfStackSource : InternStackSource
    {
        public OffProfStackSource(string fileName)
        {
            using (var file = File.OpenText(fileName))
            {
                Read(file);
            }
        }
        public OffProfStackSource(TextReader reader)
        {
            Read(reader);
        }

        #region private
        private void Read(TextReader reader)
        {
            var stack = new GrowableArray<StackSourceCallStackIndex>();


            var line = reader.ReadLine(); // Skip the first line, which is column headers. 
            var sample = new StackSourceSample(this);
            for (; ; )
            {
                line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }
                //   0       1           2             3          4       5        6         7          8
                // Order, # of Calls, % Incl Time, % Excl Time, Depth, Function, Module, Incl Time, Excl Time,% Sw. Out, Incl Switched Out, Type, Comments	Min	Avg	Max	Excl Switched Out

                int idx = 0;
                int depth = 0;
                string method = null;
                string module = null;
                int intVal;
                long longVal;
                for (int col = 0; col <= 8; col++)
                {
                    var newIdx = line.IndexOf('\t', idx);
                    Debug.Assert(0 < newIdx);
                    if (newIdx < 0)
                    {
                        goto SKIP;
                    }

                    switch (col)
                    {
                        case 1:
                            int.TryParse(line.Substring(idx, newIdx - idx), System.Globalization.NumberStyles.Number, null, out intVal);
                            sample.Count = intVal;
                            break;
                        case 4:
                            int.TryParse(line.Substring(idx, newIdx - idx), System.Globalization.NumberStyles.Number, null, out depth);
                            break;
                        case 5:
                            while (idx < newIdx)
                            {
                                if (line[idx] != ' ')
                                {
                                    break;
                                }

                                idx++;
                            }
                            method = line.Substring(idx, newIdx - idx);
                            method = method.Replace((char)0xFFFD, '@');      // They used this character to separate the method name from signature.  
                            break;
                        case 6:
                            module = "";
                            if (depth != 0)
                            {
                                module = line.Substring(idx, newIdx - idx);
                            }

                            break;
                        case 8:
                            long.TryParse(line.Substring(idx, newIdx - idx), System.Globalization.NumberStyles.Number, null, out longVal);
                            sample.Metric = longVal / 1000000; // TODO what is the metric?
                            break;
                    }
                    idx = newIdx + 1;
                }
                var moduleIdx = Interner.ModuleIntern(module);
                var frameIdx = Interner.FrameIntern(method, moduleIdx);
                var prevFrame = StackSourceCallStackIndex.Invalid;
                if (0 < depth && depth <= stack.Count)
                {
                    prevFrame = stack[depth - 1];
                }

                var callStackIdx = Interner.CallStackIntern(frameIdx, prevFrame);

                if (depth < stack.Count)
                {
                    stack.Count = depth;
                }

                stack.Add(callStackIdx);

                sample.StackIndex = callStackIdx;
                AddSample(sample);
                SKIP:;
            }
            Interner.DoneInterning();
        }
        #endregion
    }
}
