namespace System.IO2
{
    using System;
    using Runtime.Serialization;

    [Serializable]
    public sealed class InvalidDataException : SystemException
    {
        public InvalidDataException()
            : base("GenericInvalidData")
        {
        }

        public InvalidDataException(string message)
            : base(message)
        {
        }

        public InvalidDataException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        internal InvalidDataException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
