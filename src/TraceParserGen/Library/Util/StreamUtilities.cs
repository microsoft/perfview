using System.IO;

internal static class StreamUtilities
{

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
