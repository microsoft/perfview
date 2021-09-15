// Tests copied from dotnet/roslyn repo. Original source code can be found here:
// https://github.com/dotnet/roslyn/blob/main/src/Dependencies/Collections/Internal/ThrowHelper.cs

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.Diagnostics.FastSerialization
{
    /// <summary>
    /// Utility class for exception throwing for the SegmentedDictionary.
    /// </summary>
    internal static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void IfNullAndNullsAreIllegalThenThrow<T>(object value, string argName)
        {
            // Note that default(T) is not equal to null for value types except when T is Nullable<U>.
            if (!(default(T) == null) && value == null)
                throw new ArgumentNullException(argName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ThrowKeyNotFoundException<T>(T key)
        {
            throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
        }

        internal static void ThrowIndexArgumentOutOfRange_NeedNonNegNumException()
        {
            throw GetArgumentOutOfRangeException("index",
                                                 CommonStrings.ArgumentOutOfRange_NeedNonNegNum);
        }

        internal static void ThrowWrongTypeArgumentException<T>(T value, Type targetType)
        {
            throw GetWrongTypeArgumentException(value, targetType);
        }

        private static ArgumentOutOfRangeException GetArgumentOutOfRangeException(string argument, string message)
        {
            return new ArgumentOutOfRangeException(argument, message);
        }

        private static ArgumentException GetWrongTypeArgumentException(object value, Type targetType)
        {
            return new ArgumentException($"The value '{value}' is not of type '{targetType}' and cannot be used in this generic collection.", 
                                         nameof(value));
        }

        internal static class CommonStrings
        {
            public static readonly string Arg_ArrayPlusOffTooSmall = "Destination array is not long enough to copy all the items in the collection. Check array index and length.";
            public static readonly string ArgumentOutOfRange_NeedNonNegNum = "Non-negative number required.";
            public static readonly string Arg_RankMultiDimNotSupported = "Only single dimensional arrays are supported for the requested action.";
            public static readonly string Arg_NonZeroLowerBound = "The lower bound of target array must be zero.";
            public static readonly string Argument_InvalidArrayType = "Target array type is not compatible with the type of items in the collection.";
            public static readonly string InvalidOperation_ConcurrentOperationsNotSupported = "Operations that change non-concurrent collections must have exclusive access. A concurrent update was performed on this collection and corrupted its state. The collection's state is no longer correct.";
            public static readonly string InvalidOperation_EnumFailedVersion = "Collection was modified; enumeration operation may not execute.";
            public static readonly string InvalidOperation_EnumOpCantHappen = "Enumeration has either not started or has already finished.";
        }
    }
}
