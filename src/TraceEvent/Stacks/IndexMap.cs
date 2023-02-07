using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Diagnostics.Utilities
{
    /// <summary>
    /// A class that maps contiguous indices from various sources from and to a single range of contiguous indices.
    /// </summary>
    /// <remarks>
    /// This is useful for aggregating indices used, for instance, in the interface for StackSource (StackSourceCallStackIndex /
    /// StackSourceFrameIndex) in AggregateStackSource. This is an easy way, given the incoming StackSource*Index, to find the
    /// aggregated source to query, and the corresponding StackSource*Index to send to the source.
    /// </remarks>
    /// <example>
    /// With counts [3, 7, 5]:
    ///                     1 1 1 1 1  
    /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4  = Incoming index
    /// __0__ ______1______ ____2____  = Source number
    /// 0 1 2|0 1 2 3 4 5 6|0 1 2 3 4  = Offset
    /// </example>
    internal sealed class IndexMap
    {
        /// <summary>
        /// Initialize a new IndexMap with the specified counts.
        /// </summary>
        /// <param name="counts">A list mapping an index to its corresponding count.</param>
        public IndexMap(IEnumerable<int> counts)
        {
            if (counts == null)
            {
                throw new ArgumentNullException("counts");
            }

            int count = counts.Count();
            if (count == 0)
            {
                throw new ArgumentException("Count list cannot be empty.");
            }

            m_lookup = new int[count + 1];
            m_lookup[0] = 0;

            int i = 0;
            // Set up cumulative counts.
            foreach (int c in counts)
            {
                if (c <= 0)
                {
                    throw new ArgumentException("All counts must be positive.");
                }

                m_lookup[i + 1] = m_lookup[i] + c;
                i++;
            }
            m_range = m_lookup[i];
        }

        /// <summary>
        /// Find the source for an index.
        /// </summary>
        /// <param name="aggregate">The aggregate index to look up.</param>
        /// <returns>The source that <paramref name="aggregate"/> belongs to.</returns>
        public int SourceOf(int aggregate)
        {
            Debug.Assert(aggregate >= 0 && aggregate < m_range);

            // Optimization.  See if it was the same as the last one we retruned.  
            var last = m_lastSourceLookedUp;
            if (m_lookup[last] <= aggregate && aggregate < m_lookup[last + 1])
            {
                return last;
            }

            /*
             * Array.BinarySearch finds the first item in the array that is greater than or 
             * equal to its search parameter, and returns its index (possibly complemented).
             * 
             * We are interested in the index of the last item less than or equal to idx.
             * 
             * We can use Array.BinarySearch to find this index by:
             * 
             * - Searching for idx+1 to find the first item strictly greater than idx.
             * - Complementing it if necessary.
             * - Subtracting 1 from that number to find the last item less than or equal to idx.
             *   This works because m_lookup is monotone increasing, which prevents duplicates.
             */
            int src = Array.BinarySearch(m_lookup, aggregate + 1);
            if (src < 0)
            {
                src = ~src;
            }

            src--;

            m_lastSourceLookedUp = src;
            return src;
        }

        /// <summary>
        /// Find the offset into a given source of a given aggregate index.
        /// </summary>
        /// <param name="aggregate">The aggregate index to look up.</param>
        /// <param name="source">The source to find the offset into.</param>
        /// <returns>The offset of <paramref name="aggregate"/> into <paramref name="source"/>.</returns>
        public int OffsetOf(int source, int aggregate)
        {
            Debug.Assert(source >= -1 && source < m_lookup.Length);
            if (source == -1)
            {
                return aggregate;
            }
            else
            {
                int off = aggregate - m_lookup[source];
                Debug.Assert(off >= 0);
                return off;
            }
        }

        /// <summary>
        /// Finds the index for a given source/offset pair.
        /// </summary>
        /// <param name="source">The source number of the item.</param>
        /// <param name="offset">The offset into the corresponding source for the item.</param>
        /// <returns>The index corresponding to the pair of <paramref name="source"/> and <paramref name="offset"/>.</returns>
        public int IndexOf(int source, int offset)
        {
            Debug.Assert(source >= -1 && source < m_lookup.Length);

            if (offset < 0)
            {
                return offset;
            }
            else
            {
                int idx = offset + m_lookup[source];
                Debug.Assert(source == m_lookup.Length - 1 || idx < m_lookup[source + 1]);
                return idx;
            }
        }

        /// <summary>
        /// The total number of indices in the map.
        /// </summary>
        public int Count
        {
            get { return m_range; }
        }

        #region private 
        /// <summary>
        /// The lookup table to convert indices to source/offset pairs.
        /// </summary>
        /// <remarks>
        /// This contains the cumulative count of indices that occurred before each source.
        /// The last element is the total number of indices (equal to m_range).
        /// </remarks>
        private readonly int[] m_lookup;
        /// <summary>
        /// The total number of indices in the map.
        /// </summary>
        private readonly int m_range;

        /// <summary>
        /// We remember the last source we looked up and check there first very likely they are next to one another. 
        /// </summary>
        private int m_lastSourceLookedUp;
        #endregion
    }
}
