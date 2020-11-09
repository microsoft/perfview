using Microsoft.Diagnostics.Tracing.Stacks;
using System;
using System.IO;

namespace PerfView.OtherSources
{
    /// <summary>
    /// Takes an arbitrary text file and turns it into a stack source where each line is a frame (and there is no depth to the stacks).  
    /// Useful for histograms and diffs.  
    /// </summary>
    public class TextStackSource : InternStackSource
    {
        public TextStackSource() { }

        /// <summary>
        /// Allows more complete control over what gets emitted for a particular line.   You get the line
        /// as well as an Interner (so you can created Frames and callStacks, and you just return what you want)
        /// </summary>
        public Func<StackSourceInterner, string, StackSourceCallStackIndex> StackForLine;

        public void Read(string fileName)
        {
            using (var file = File.OpenText(fileName))
            {
                Read(file);
            }
        }

        public void Read(TextReader reader)
        {
            var sample = new StackSourceSample(this);
            sample.Metric = 1;
            for (; ; )
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }

                if (StackForLine != null)
                {
                    sample.StackIndex = StackForLine(Interner, line);
                }
                else
                {
                    // Form the stack for this entry (trivial one element stack)
                    var frameIndex = Interner.FrameIntern(line);
                    sample.StackIndex = Interner.CallStackIntern(frameIndex, StackSourceCallStackIndex.Invalid);
                }
                if (sample.StackIndex != StackSourceCallStackIndex.Invalid)
                {
                    AddSample(sample);
                }
            }
            Interner.DoneInterning();
        }
    }
}
