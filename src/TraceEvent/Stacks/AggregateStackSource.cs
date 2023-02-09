using Microsoft.Diagnostics.Tracing.Stacks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Utilities;

namespace Diagnostics.Tracing.StackSources
{
    /// <summary>
    /// A StackSource that aggregates information from other StackSources into a single unified view.
    /// </summary>
    /// <remarks>
    /// Each StackSource has a name associated with it. The stacks for each StackSource will be grouped under
    /// a pseudo-frame named the same as the source name. Source names are specified on initialization.
    /// </remarks>
    public class AggregateStackSource : StackSource
    {
        /// <summary>
        /// Initialize a new AggregateStackSource.
        /// </summary>
        /// <param name="sources">An IEnumerable of KeyValuePairs mapping source names to StackSources.</param>
        public AggregateStackSource(IEnumerable<KeyValuePair<string, StackSource>> sources)
        {
            m_sourceCount = sources.Count() + 1; // +1 for the pseudo-source.

            m_sourceNames = new string[m_sourceCount];
            m_sources = new StackSource[m_sourceCount];

            // We initialize this when we see the first sample
            m_firstSampleTime = new double[m_sourceCount];
            for (int j = 0; j < m_firstSampleTime.Length; j++)
            {
                m_firstSampleTime[j] = double.NegativeInfinity;
            }

            // True if all sub-sources support retrieving samples by index.
            bool supportsSamples = true;

            // The time limit for this source.
            m_RelativeMSecLimit = 0.0f;
            int i = 1;

            // Unpack sources.
            foreach (var pair in sources)
            {
                var name = pair.Key;
                var source = pair.Value;

                m_sourceNames[i] = name;
                m_sources[i] = source;
                i++;

                m_RelativeMSecLimit = Math.Max(m_RelativeMSecLimit, source.SampleTimeRelativeMSecLimit);

                if (source.SampleIndexLimit == 0)
                {
                    supportsSamples = false;
                }
            }

            // Set up pseudo-source.
            m_sources[0] = m_pseudo = new PseudoStackSource(m_sourceNames);

            // Set up our returned sample.
            m_sampleStorage = new StackSourceSample(this);

            // Set up index maps.
            m_stackMap = new IndexMap(m_sources.Select(s => s.CallStackIndexLimit));
            m_frameMap = new IndexMap(m_sources.Select(s => s.CallFrameIndexLimit));

            if (supportsSamples)
            {
                // The sampleMap has size (m_sourceCount - 1) because m_pseudo doesn't have samples.
                m_sampleMap = new IndexMap(m_sources.Skip(1).Select(s => s.SampleIndexLimit));
            }
            else
            {
                m_sampleMap = null;
            }
        }

        /// <summary>
        /// Enumerate samples with a callback function.
        /// </summary>
        /// <param name="callback">The function to call on each sample.</param>
        public override void ForEach(Action<StackSourceSample> callback)
        {
            ForEach(callback, null);
        }

        /// <summary>
        /// override
        /// </summary>
        public override bool SamplesImmutable { get { return false; } }

        /// <summary>
        /// Enumerate samples for a given set of scenarios with a callback function.
        /// </summary>
        /// <param name="callback">The function to call on each sample.</param>
        /// <param name="scenariosIncluded">An array of length ScenarioCount. If scenariosIncluded[i] == true, include scenario i.</param>
        public void ForEach(Action<StackSourceSample> callback, bool[] scenariosIncluded)
        {
            for (int src = 0; src < ScenarioCount; src++)
            {
                if (scenariosIncluded == null || scenariosIncluded[src])
                {
                    m_sources[src + 1].ForEach(sample => callback(ConvertSample(sample, m_sampleStorage, src + 1)));
                }
            }
        }

        /// <summary>
        /// Override
        /// </summary>
        public void ParallelForEach(Action<StackSourceSample> callback, bool[] scenariosIncluded, int desiredParallelism = 0)
        {
            Parallel.For(0, ScenarioCount, delegate (int src, ParallelLoopState state)
            {
                var sampleStorage = new StackSourceSample(this);
                if (scenariosIncluded == null || scenariosIncluded[src])
                {
                    m_sources[src + 1].ForEach(sample => callback(ConvertSample(sample, sampleStorage, src + 1)));
                }
            });
        }


        /// <summary>
        /// Look up a sample by index.
        /// </summary>
        /// <param name="sampleIndex">The index of the sample to look up.</param>
        /// <returns>
        /// The sample, if it can be found and all sub-sources support indexing; null otherwise.
        /// </returns>
        public override StackSourceSample GetSampleByIndex(StackSourceSampleIndex sampleIndex)
        {
            if (m_sampleMap != null)
            {
                Debug.Assert((int)sampleIndex >= 0 && (int)sampleIndex < SampleIndexLimit);
                int source = m_sampleMap.SourceOf(sampleIndex);
                var offset = m_sampleMap.OffsetOf(source, sampleIndex);
                return ConvertSample(m_sources[source + 1].GetSampleByIndex(offset), m_sampleStorage, source + 1);
            }
            else
            {
                return base.GetSampleByIndex(sampleIndex);
            }
        }

        /// <summary>
        /// Gets the index of the caller of a given call stack.
        /// </summary>
        /// <param name="callStackIndex">The call stack to look up.</param>
        /// <returns>The caller, if it exists, <see cref="StackSourceCallStackIndex.Invalid"/> otherwise.</returns>
        public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
        {
            Debug.Assert(callStackIndex >= StackSourceCallStackIndex.Start && (int)callStackIndex < CallStackIndexLimit);

            int source = m_stackMap.SourceOf(callStackIndex);
            Debug.Assert(source >= 0);
            var offset = m_stackMap.OffsetOf(source, callStackIndex);

            var caller = m_sources[source].GetCallerIndex(offset);

            // If we've run out of stack, try to find the parent stack in the pseudo-source.
            if (caller == StackSourceCallStackIndex.Invalid)
            {
                return m_stackMap.IndexOf(0, m_pseudo.GetStackForSource(source));
            }
            else
            {
                return m_stackMap.IndexOf(source, caller);
            }
        }

        /// <summary>
        /// Get the frame index of a given call stack.
        /// </summary>
        /// <param name="callStackIndex">The call stack to look up.</param>
        /// <returns>The frame index of the call stack, if it exists, <see cref="StackSourceFrameIndex.Invalid"/> otherwise.</returns>
        public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
        {
            Debug.Assert(callStackIndex >= StackSourceCallStackIndex.Start && (int)callStackIndex < CallStackIndexLimit);

            int source = m_stackMap.SourceOf(callStackIndex);
            Debug.Assert(source >= 0);
            var offset = m_stackMap.OffsetOf(source, callStackIndex);

            var frame = m_sources[source].GetFrameIndex(offset);

            return m_frameMap.IndexOf(source, frame);
        }

        /// <summary>
        /// Gets the name of a frame.
        /// </summary>
        /// <param name="frameIndex">The frame to look up.</param>
        /// <param name="verboseName">Whether to include full module paths.</param>
        /// <returns>The name of the frame.</returns>
        public override string GetFrameName(StackSourceFrameIndex frameIndex, bool verboseName)
        {
            Debug.Assert((int)frameIndex >= 0 && (int)frameIndex < CallFrameIndexLimit);

            int source = m_frameMap.SourceOf(frameIndex);
            Debug.Assert(source >= 0);
            var offset = m_frameMap.OffsetOf(source, frameIndex);

            return m_sources[source].GetFrameName(offset, verboseName);
        }

        /// <summary>
        /// The total number of call stacks in this source.
        /// </summary>
        public override int CallStackIndexLimit
        {
            get { return m_stackMap.Count; }
        }

        /// <summary>
        /// The total number of frames in this source.
        /// </summary>
        public override int CallFrameIndexLimit
        {
            get { return m_frameMap.Count; }
        }

        /// <summary>
        /// The total number of samples in this source.
        /// </summary>
        public override int SampleIndexLimit
        {
            get { return (m_sampleMap != null) ? m_sampleMap.Count : 0; }
        }

        /// <summary>
        /// The names for the scenarios.
        /// </summary>
        public string[] ScenarioNames
        {
            get
            {
                return m_sourceNames.Skip(1).ToArray();
            }
        }

        /// <summary>
        /// override
        /// </summary>
        public override double SampleTimeRelativeMSecLimit
        {
            get { return m_RelativeMSecLimit; }
        }

        /// <summary>
        /// override
        /// </summary>
        public override int ScenarioCount
        {
            get { return m_sourceCount - 1; }
        }

        #region Private members

        /// <summary>
        /// Convert a StackSourceSample produced by a sub-source into one suitable for the aggregate source.
        /// </summary>
        /// <param name="input">The StackSourceSample to convert.</param>
        /// <param name="storage">A place to but the returned sampled (will become the return value).</param>
        /// <param name="sourceIdx">The index of the source from which the sample came.</param>
        /// <returns>The converted sample.</returns>
        /// <remarks>
        /// If ConvertSample is called again, all previous samples produced by ConvertSample may no longer be used.
        /// </remarks>
        private StackSourceSample ConvertSample(StackSourceSample input, StackSourceSample storage, int sourceIdx)
        {
            storage.Metric = input.Metric;

            // We normalize all the scenarios so that they start on their first sample time.   
            var timeOrigin = m_firstSampleTime[sourceIdx];
            if (timeOrigin < 0)
            {
                timeOrigin = m_firstSampleTime[sourceIdx] = input.TimeRelativeMSec;
            }

            storage.TimeRelativeMSec = input.TimeRelativeMSec - timeOrigin;

            storage.StackIndex = m_stackMap.IndexOf(sourceIdx, input.StackIndex);
            storage.Scenario = sourceIdx - 1;

            if (m_sampleMap != null)
            {
                storage.SampleIndex = m_sampleMap.IndexOf(sourceIdx - 1, input.SampleIndex);
            }
            else
            {
                storage.SampleIndex = StackSourceSampleIndex.Invalid;
            }

            return storage;
        }
        /// <summary>
        /// Friendly names of sources.
        /// </summary>
        /// <remarks>
        /// Name 0 is the name of the pseudo-source, which should not be used.
        /// </remarks>
        private readonly string[] m_sourceNames;

        /// <summary>
        /// The list of sources.
        /// </summary>
        /// <remarks>
        /// Source 0 is the pseudo-source (identical to m_pseudo).
        /// </remarks>
        private readonly StackSource[] m_sources;
        /// <summary>
        /// THis is the time of the first sample.  It lets us normalize the time in the sample to be relative to this.
        /// </summary>
        private double[] m_firstSampleTime;
        private readonly PseudoStackSource m_pseudo;

        private readonly IndexMap m_stackMap;
        private readonly IndexMap m_frameMap;
        private readonly IndexMap m_sampleMap;

        private readonly int m_sourceCount;
        private readonly double m_RelativeMSecLimit;

        private readonly StackSourceSample m_sampleStorage;
        #endregion

        /// <summary>
        /// A StackSource to generate the pseudo-frames needed to group scenarios.
        /// </summary>
        private class PseudoStackSource : StackSource
        {
            /// <summary>
            /// Initialize a new PseudoStackSource.
            /// </summary>
            /// <param name="names">The names of the frames.</param>
            internal PseudoStackSource(string[] names)
            {
                // Make a copy, dropping the first name.
                this.names = new string[names.Length - 1];
                Array.Copy(names, 1, this.names, 0, this.names.Length);
            }

            /// <summary>
            /// Gets the CallStackIndex of the call stack corresponding to a given source.
            /// </summary>
            /// <param name="sourceIdx">The index of the source to look up.</param>
            /// <returns>The StackSourceCallStackIndex of a stack under which to group all call stacks for that source.</returns>
            public StackSourceCallStackIndex GetStackForSource(int sourceIdx)
            {
                Debug.Assert(sourceIdx >= 0 && sourceIdx <= names.Length);
                // Parent of our own invalid stacks is invalid.
                if (sourceIdx == 0)
                {
                    return StackSourceCallStackIndex.Invalid;
                }
                // Parent of other sources' invalid stacks is ours.
                else
                {
                    return sourceIdx - 1 + StackSourceCallStackIndex.Start;
                }
            }

            public override void ForEach(Action<StackSourceSample> callback)
            {
                // We don't have any samples to produce.
                return;
            }
            public override bool SamplesImmutable { get { return true; } }

            /// <summary>
            /// Gets the index of the caller of a given call stack.
            /// </summary>
            /// <param name="callStackIndex">The call stack to look up.</param>
            /// <returns>The caller, if it exists, <see cref="StackSourceCallStackIndex.Invalid"/> otherwise.</returns>
            public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
            {
                // All pseudo-frames are top-level frames.
                Debug.Assert((int)callStackIndex >= 0 && (int)callStackIndex < names.Length);
                return StackSourceCallStackIndex.Invalid;
            }

            /// <summary>
            /// Get the frame index of a given call stack.
            /// </summary>
            /// <param name="callStackIndex">The call stack to look up.</param>
            /// <returns>The frame index of the call stack, if it exists, <see cref="StackSourceFrameIndex.Invalid"/> otherwise.</returns>
            public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
            {
                return (callStackIndex - StackSourceCallStackIndex.Start) + StackSourceFrameIndex.Start;
            }

            /// <summary>
            /// Gets the name of a frame.
            /// </summary>
            /// <param name="frameIndex">The frame to look up.</param>
            /// <param name="verboseName">Whether to include full module paths.</param>
            /// <returns>The name of the frame.</returns>
            public override string GetFrameName(StackSourceFrameIndex frameIndex, bool verboseName)
            {
                if (frameIndex < StackSourceFrameIndex.Start)
                {
                    switch (frameIndex)
                    {
                        case StackSourceFrameIndex.Broken:
                            return "BROKEN";
                        case StackSourceFrameIndex.Overhead:
                            return "OVERHEAD";
                        case StackSourceFrameIndex.Root:
                            return "ROOT";
                        default:
                            return "?!?";
                    }
                }
                else
                {
                    return names[frameIndex - StackSourceFrameIndex.Start];
                }
            }

            /// <summary>
            /// The total number of call stacks in this source.
            /// </summary>
            public override int CallStackIndexLimit
            {
                get { return names.Length + (int)StackSourceCallStackIndex.Start; }
            }

            /// <summary>
            /// The total number of frames in this source.
            /// </summary>
            public override int CallFrameIndexLimit
            {
                get { return names.Length + (int)StackSourceFrameIndex.Start; }
            }

            /// <summary>
            /// The names of the frames that this source generates.
            /// </summary>
            private readonly string[] names;
        }
    }

    /// <summary>
    /// Extension methods for type-safe IndexMap operations on StackSource*Index enums.
    /// </summary>
    internal static class IndexMapExtensions
    {
        #region StackSourceCallStackIndex
        public static int SourceOf(this IndexMap map, StackSourceCallStackIndex aggregate)
        {
            return map.SourceOf((int)aggregate);
        }

        public static StackSourceCallStackIndex OffsetOf(this IndexMap map, int source, StackSourceCallStackIndex aggregate)
        {
            return (StackSourceCallStackIndex)map.OffsetOf(source, (int)aggregate);
        }

        public static StackSourceCallStackIndex IndexOf(this IndexMap map, int source, StackSourceCallStackIndex offset)
        {
            return (StackSourceCallStackIndex)map.IndexOf(source, (int)offset);
        }
        #endregion

        #region StackSourceFrameIndex
        public static int SourceOf(this IndexMap map, StackSourceFrameIndex aggregate)
        {
            return map.SourceOf((int)aggregate);
        }

        public static StackSourceFrameIndex OffsetOf(this IndexMap map, int source, StackSourceFrameIndex aggregate)
        {
            return (StackSourceFrameIndex)map.OffsetOf(source, (int)aggregate);
        }

        public static StackSourceFrameIndex IndexOf(this IndexMap map, int source, StackSourceFrameIndex offset)
        {
            return (StackSourceFrameIndex)map.IndexOf(source, (int)offset);
        }
        #endregion

        #region StackSourceSampleIndex
        public static int SourceOf(this IndexMap map, StackSourceSampleIndex aggregate)
        {
            return map.SourceOf((int)aggregate);
        }

        public static StackSourceSampleIndex OffsetOf(this IndexMap map, int source, StackSourceSampleIndex aggregate)
        {
            return (StackSourceSampleIndex)map.OffsetOf(source, (int)aggregate);
        }

        public static StackSourceSampleIndex IndexOf(this IndexMap map, int source, StackSourceSampleIndex offset)
        {
            return (StackSourceSampleIndex)map.IndexOf(source, (int)offset);
        }
        #endregion
    }
}
