using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Stacks
{
    /// <summary>
    /// A WTReader knows how to read a text file from the cdb (windbg) WT command output
    /// </summary>
    class WTStackSource : InternStackSource
    {
        public WTStackSource(string fileName)
        {
            using (var file = File.OpenText(fileName))
                Read(file);
        }
        public WTStackSource(TextReader reader)
        {
            Read(reader);
        }

        #region private
        struct WTStackElem
        {
            public StackSourceCallStackIndex FirstCallStackIndex;
            public StackSourceCallStackIndex CallStackIndex;
            public int ExclInstrSoFar;

#if DEBUG
            public override string ToString()
            {
                return string.Format("<Elem CallStack=\"{0}\" FirstCallStack=\"{0}\"ExclInstrSoFar=\"{1}\"/>",
                    CallStackIndex, FirstCallStackIndex, ExclInstrSoFar);
            }
#endif
        }

        void Read(TextReader reader)
        {
            // TODO this is relatively inefficient.  
            var regEx = new Regex(@"^\s*(\d+)\s*(\d+)\s*\[\s*(\d+)\s*\]\s*(\S*?)!?(.*)");

            var stack = new GrowableArray<WTStackElem>();
            WTStackElem elem = new WTStackElem();
            long time = 0;

            for (; ; )
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;
                var match = regEx.Match(line);
                if (match.Success)
                {
                    int excInstrSoFar = int.Parse(match.Groups[1].Value);
                    int depth = int.Parse(match.Groups[3].Value);
                    string module = match.Groups[4].Value;
                    string method = match.Groups[5].Value;

                    var moduleIndex = ModuleIntern(module);
                    var frameIndex = FrameIntern(method, moduleIndex);

                    var parent = StackSourceCallStackIndex.Invalid;
                    if (depth > 0)
                        parent = stack[depth - 1].FirstCallStackIndex;    // TODO handle out of range

                    var callStackIndex = CallStackIntern(frameIndex, parent);

                    int extra = stack.Count - depth;
                    int exclInstr;
                    if (extra > 0)
                    {
                        elem = stack[depth];

                        if (callStackIndex == elem.CallStackIndex)
                            exclInstr = excInstrSoFar - elem.ExclInstrSoFar;
                        else
                        {
                            exclInstr = excInstrSoFar;
                            elem.CallStackIndex = callStackIndex;
                        }
                        Debug.Assert(exclInstr >= 0);
                        stack.RemoveRange(depth, extra);
                    }
                    else
                    {
                        elem.CallStackIndex = callStackIndex;
                        elem.FirstCallStackIndex = callStackIndex;
                        Debug.Assert(extra == 0);
                        exclInstr = excInstrSoFar;
                    }
                    elem.ExclInstrSoFar = excInstrSoFar;
                    stack.Add(elem);

                    time += exclInstr;

                    var sample = new StackSourceSample(this);
                    sample.SampleIndex = (StackSourceSampleIndex)m_samples.Count;
                    sample.Metric = exclInstr;
                    sample.TimeRelMSec = time - exclInstr;
                    sample.StackIndex = elem.FirstCallStackIndex;

                    // Break long sequences of instructions into individual samples.   This
                    // makes timeline work well.   
                    // TODO this bloats the data, not clear if this is the right tradeoff ....
                    const int maxSize = 20;
                    while (sample.Metric > maxSize)
                    {
                        var subSample = new StackSourceSample(sample);
                        subSample.Metric = maxSize;
                        sample.Metric -= maxSize;
                        sample.TimeRelMSec += maxSize;
                        m_samples.Add(subSample);
                    }

                    m_samples.Add(sample);
#if DEBUG
                    var sampleStr = this.ToString(sample);
                    Debug.WriteLine(sampleStr);
#endif
                }
            }
            m_sampleTimeRelMSecLimit = time;
            CompletedReading();
        }
        #endregion
    }
}