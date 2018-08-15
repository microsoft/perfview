using System;

/// <summary>
/// Things that really should be in System.Array but aren't
/// </summary>
public static class ArrayUtil
{
    /// <summary>
    /// Concatinate two arrays and return the new array result.
    /// </summary>
    /// <returns>Concatinated array.</returns>
    public static T[] Concat<T>(T[] array1, T[] array2)
    {
        T[] ret = new T[array1.Length + array2.Length];
        Array.Copy(array1, ret, array1.Length);
        Array.Copy(array2, 0, ret, array1.Length, array2.Length);
        return ret;
    }
    /// <summary>
    /// Return a new array that has a range of elements removed.  
    /// </summary>
    /// <param name="array">The array to removed elements from.   It is NOT modified.</param>
    /// <param name="index">The index of the first element that will be removed.</param>
    /// <param name="count">The number of elements to be removed.</param>
    /// <returns>The shorted array.</returns>
    public static T[] RemoveRange<T>(T[] array, int index, int count)
    {
        T[] ret = new T[array.Length - count];
        if (index > 0)
        {
            Array.Copy(array, ret, index);
        }

        int tailCount = ret.Length - index;
        if (tailCount > 0)
        {
            Array.Copy(array, index + count, ret, index, tailCount);
        }

        return ret;
    }
    /// <summary>
    /// Return a new array where a range as been removed and replace with a range from another array.
    /// </summary>
    /// <param name="sourceArray">The source array to be manipulated.  It is NOT modified.</param>
    /// <param name="removalIndex">The first element of sourceArray to remove.</param>
    /// <param name="removalCount">The number of elements to remove.  Elements after this exist in the
    /// return array</param>
    /// <param name="insertArray">The array that will provide the elements to splice into soruceArray.</param>
    /// <param name="insertIndex">The first element in insertArray to splice in.</param>
    /// <param name="insertCount">The number of elements from insertArray to splice in.</param>
    /// <returns>The new array that has had the removal range replaced with the insert range.</returns>
    public static T[] Splice<T>(T[] sourceArray, int removalIndex, int removalCount, T[] insertArray, int insertIndex, int insertCount)
    {
        T[] ret = new T[sourceArray.Length - removalCount + insertCount];
        Array.Copy(sourceArray, ret, removalIndex);
        Array.Copy(insertArray, insertIndex, ret, removalIndex, insertCount);
        int tailDestIndex = removalIndex + insertCount;
        Array.Copy(sourceArray, removalIndex + removalCount, ret, tailDestIndex, ret.Length - tailDestIndex);
        return ret;
    }
}
