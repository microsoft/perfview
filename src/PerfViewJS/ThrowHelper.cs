// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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