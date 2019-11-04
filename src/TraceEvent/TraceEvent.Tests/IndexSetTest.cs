using System;
using System.Collections.Generic;
using Xunit;
using Microsoft.Diagnostics.Tracing.Utilities;

namespace TraceEventTests
{
    public class IndexSetTest
    {
        [Fact]
        public void BasicTest()
        {
            HashSet<uint> intSet = new HashSet<uint>();

            IndexSet oddSet = new IndexSet();

            int limit = 1024 * 1024;

            Random random = new Random();

            for (int i = 0; i < limit; i++)
            {
                int val = random.Next(limit);

                if ((val % 2) == 0)
                {
                    val++;
                }

                intSet.Add((uint)val);
                oddSet.Add((uint)val);
            }

            int error = 0;

            for (int i = 0; i < limit; i+= 2)
            {
                if (oddSet.Contains((uint)i))
                {
                    error++;
                }
            }

            Assert.True(error == 0);

            foreach (uint v in intSet)
            {
                if (!oddSet.Contains(v))
                {
                    error++;
                }
            }

            Assert.True(error == 0);
        }
    }
}
