/*  Copyright (c) Microsoft Corporation.  All rights reserved. */
/* AUTHOR: Vance Morrison   
 * Date  : 10/20/2007  */
using System;
using System.IO;

static class StreamUtilities
{

    /// <summary>
    /// CopyStream simply copies 'fromStream' to 'toStream'
    /// </summary>
    static public int CopyStream(Stream fromStream, Stream toStream)
    {
        byte[] buffer = new byte[8192];
        int totalBytes = 0;
        for (; ; )
        {
            int count = fromStream.Read(buffer, 0, buffer.Length);
            if (count == 0)
                break;
            toStream.Write(buffer, 0, count);
            totalBytes += count;
        }
        return totalBytes;
    }
};
