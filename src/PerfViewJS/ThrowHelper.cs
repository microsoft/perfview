// <copyright file="ThrowHelper.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System;
    using System.Runtime.CompilerServices;

    internal static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentNullException(string argument)
        {
            throw new ArgumentNullException(argument);
        }
    }
}