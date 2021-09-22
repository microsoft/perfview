// Tests copied from dotnet/runtime repo. Original source code can be found here:
// https://github.com/dotnet/runtime/blob/main/src/libraries/Common/tests/System/RandomExtensions.cs

using System;

namespace PerfView.Collections.Tests
{
    internal static class RandomExtensions
    {
        public static void Shuffle<T>(this Random random, T[] array)
        {
            int n = array.Length;
            while (n > 1)
            {
                int k = random.Next(n--);
                T temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
        }
    }
}
