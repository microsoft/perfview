namespace System.IO2
{

    using System;
    using System.Runtime.Serialization;

    [Serializable()]
    public sealed class InvalidDataException : SystemException
    {
        public InvalidDataException()
            : base("GenericInvalidData")
        {
        }

        public InvalidDataException(String message)
            : base(message)
        {
        }

        public InvalidDataException(String message, Exception innerException)
            : base(message, innerException)
        {
        }

        internal InvalidDataException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

    }
}
