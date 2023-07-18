using PerfView.Utilities;
using Xunit;

namespace PerfViewTests.Utilities
{
    public static class RandomNumberTests
    {
        [Theory]
        [InlineData(10000)]
        public static void GenerateManyDoubles(int numIterations)
        {
            for (int i = 0; i < numIterations; i++)
            {
                double val = RandomNumberGenerator.GetDouble();
                Assert.InRange(val, 0.0, 1.0);
            }
        }
    }
}