//     Copyright (c) Microsoft Corporation.  All rights reserved.
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// 
using System.IO;

namespace Microsoft.Diagnostics.Utilities
{
    /// <summary>
    /// General utilities associated with streams. 
    /// </summary>
#if UTILITIES_PUBLIC
    public 
#endif
    internal static class StreamUtilities
    {
        /// <summary>
        /// Open the 'fromFilePath' and write its contents to 'toStream'
        /// </summary>
        public static void CopyFromFile(string fromFilePath, Stream toStream)
        {
            using (Stream fromStream = File.OpenRead(fromFilePath))
            {
                CopyStream(fromStream, toStream);
            }
        }
        /// <summary>
        /// Open the 'toFilePath' for writing and write the contents of 'fromStream' to it
        /// </summary>
        public static void CopyToFile(Stream fromStream, string toFilePath)
        {
            using (Stream toStream = File.Create(toFilePath))
            {
                CopyStream(fromStream, toStream);
            }
        }
        /// <summary>
        /// CopyStream simply copies 'fromStream' to 'toStream'
        /// </summary>
        public static int CopyStream(Stream fromStream, Stream toStream)
        {
            byte[] buffer = new byte[8192];
            int totalBytes = 0;
            for (; ; )
            {
                int count = fromStream.Read(buffer, 0, buffer.Length);
                if (count == 0)
                {
                    break;
                }

                toStream.Write(buffer, 0, count);
                totalBytes += count;
            }
            return totalBytes;
        }
    };
}
