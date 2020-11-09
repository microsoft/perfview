namespace System.IO.Compression2
{
    internal interface IFileFormatWriter
    {
        byte[] GetHeader();
        void UpdateWithBytesRead(byte[] buffer, int offset, int bytesToCopy);
        byte[] GetFooter();
    }

    internal interface IFileFormatReader
    {
        bool ReadHeader(InputBuffer input);
        bool ReadFooter(InputBuffer input);
        void UpdateWithBytesRead(byte[] buffer, int offset, int bytesToCopy);
        void Validate();
    }

}
