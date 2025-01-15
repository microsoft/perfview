using Microsoft.Diagnostics.Tracing.Stacks;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace Diagnostics.Tracing.StackSources
{
    /// <summary>
    /// Reads a format that the YourKit profile exports.  Basically it is XML where each node 
    /// is represented by a XML node.   The intent is that this can be made to read a variety 
    /// of such XML export formats (yourKit being just the first).
    /// 
    /// The logic is complicated a bit by the fact that sometimes the metric in the tree is inclusive
    /// (and PerfVIew wants only the exlusive times, it calculates the inclusive for you).  In those
    /// casese we have to 'undo' this can calculate the exclusive time by subtracting in inclusive time
    /// of all the children from its parent.  
    /// </summary>
    public class XmlTreeStackSource : InternStackSource
    {
        /// <summary>
        /// </summary>
        public XmlTreeStackSource(string fileName)
        {
            using (Stream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                Read(stream);
            }
        }

        #region private

        private void Read(Stream rawStream)
        {
            XmlReaderSettings settings = new XmlReaderSettings() { IgnoreWhitespace = true, IgnoreComments = true };
            XmlReader reader = XmlTextReader.Create(rawStream, settings);
            var stack = new GrowableArray<StackSourceSample>();
            bool metricsInclusive = false;      // If true, we need to convert them to exclusive as part of processing 

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "node")
                    {
                        var sample = new StackSourceSample(this);
                        string callTree = reader.GetAttribute("call_tree");

                        // Case for allocation stacks 
                        string sizeStr = reader.GetAttribute("size");
                        if (sizeStr != null)
                        {
                            metricsInclusive = true;        // allocation numbers are inclusive 
                            int size = 0;
                            int.TryParse(sizeStr, out size);
                            sample.Metric = size;

                            string recoredObectsStr = reader.GetAttribute("recorded_objects");
                            int recoredObects = 0;
                            if (recoredObectsStr != null)
                            {
                                int.TryParse(recoredObectsStr, out recoredObects);
                            }

                            sample.Count = recoredObects;
                        }
                        else
                        {
                            Debug.Assert(metricsInclusive == false);        // CPU time is exclusive. 
                            // For CPU
                            string own_time_msStr = reader.GetAttribute("own_time_ms");
                            if (own_time_msStr != null)
                            {
                                int own_time_ms;
                                int.TryParse(own_time_msStr, out own_time_ms);
                                sample.Metric = own_time_ms;

                                string countStr = reader.GetAttribute("count");
                                int count = 0;
                                if (countStr != null)
                                {
                                    int.TryParse(countStr, out count);
                                }

                                sample.Count = count;
                            }
                        }

                        // Get the parent stack for this line 
                        var parentStackIndex = StackSourceCallStackIndex.Invalid;
                        int depth = stack.Count;
                        if (depth > 0)
                        {
                            StackSourceSample parent = stack[depth - 1];
                            parentStackIndex = parent.StackIndex;

                            if (metricsInclusive)
                            {
                                // The values are inclusive, but StackSoruceSamples are the exclusive amounts, so remove children. 
                                parent.Count -= sample.Count;
                                parent.Metric -= sample.Metric;
                            }
                        }
                        if (callTree != null)
                        {
                            var frameIndex = Interner.FrameIntern(callTree);
                            sample.StackIndex = Interner.CallStackIntern(frameIndex, parentStackIndex);
                        }
                        stack.Add(sample);
                    }
                }
                if (reader.NodeType == XmlNodeType.EndElement || reader.IsEmptyElement)
                {
                    if (reader.Name == "node")
                    {
                        StackSourceSample sample = stack.Pop();
                        if ((sample.Count > 0 || sample.Metric > 0) && sample.StackIndex != StackSourceCallStackIndex.Invalid)
                        {
                            AddSample(sample);
                        }
                    }
                }
            }

            Debug.Assert(stack.Count == 0);
            Interner.DoneInterning();
        }
        #endregion
    }
}
