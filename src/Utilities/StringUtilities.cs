using System;

namespace Microsoft.Diagnostics.Utilities
{
#if UTILITIES_PUBLIC
    public 
#endif
    static class StringUtilities
    {
        public static bool IsNullOrWhiteSpace(string value)
        {
            // Implementation copied from /ndp/clr/src/bcl/System/string.cs

            if (value == null)
            {
                return true;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (!Char.IsWhiteSpace(value[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static string QuotePadLeft(string str, int totalSize)
        {
            int spaces = totalSize - 2 - str.Length;
            if (spaces < 0)
            {
                spaces = 0;
            }

            return new string(' ', spaces) + '"' + str + '"';
        }
    }
}
