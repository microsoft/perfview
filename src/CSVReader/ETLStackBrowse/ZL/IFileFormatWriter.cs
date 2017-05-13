namespace System.IO.Compression2
{
    internal interface IFileFormatWriter
    {
        byte[] GetHeader();
        void UpdateWithBytesRead(byte[] buffer, int offset, int bytesToCopy);
        byte[] GetFooter();
    }
}
