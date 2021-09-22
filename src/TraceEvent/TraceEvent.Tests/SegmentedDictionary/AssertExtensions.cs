// Tests copied from dotnet/runtime repo. Original source code can be found here:
// https://github.com/dotnet/runtime/blob/main/src/libraries/Common/tests/TestUtilities/System/AssertExtensions.cs

using System;
using Xunit;

namespace PerfView.Collections.Tests
{
    internal static class AssertExtensions
    {
        public static T Throws<T>(string expectedParamName, Action action)
            where T : ArgumentException
        {
            T exception = Assert.Throws<T>(action);

            Assert.Equal(expectedParamName, exception.ParamName);

            return exception;
        }
    }
}
