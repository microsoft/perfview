using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.Ctf.CtfMetadataTypes
{
    internal class CtfInteger : CtfMetadataType
    {
        private readonly ICtfIntegerReader _ctfIntegerReaderImpl;
        readonly int _align;

        public override int Align => _align;

        public CtfInteger(CtfPropertyBag bag)
            : base(CtfTypes.Integer)
        {
            Size = bag.GetShort("size");
            _align = bag.GetShortOrNull("align") ?? 8;
            Signed = bag.GetBoolean("signed");
            Encoding = bag.GetString("encoding") ?? "none";
            Base = bag.GetShortOrNull("base") ?? 10;
            Map = bag.GetString("map");

            _ctfIntegerReaderImpl = CtfIntegerReaderFactory.Create(this);
        }

        public short Size { get; }
        public bool Signed { get; }
        public string Encoding { get; }
        public short Base { get; }
        public string Map { get; }

        public override string ToString()
        {
            return (Signed ? "int" : "uint") + Size.ToString();
        }

        internal override CtfMetadataType ResolveReference(Dictionary<string, CtfMetadataType> typealias)
        {
            return this;
        }

        public override int GetSize()
        {
            return Size;
        }

        public static T ReadInt<T>(CtfMetadataType type, byte[] buffer, int bitOffset) where T : IConvertible
        {
            if (type.CtfType == CtfTypes.Enum)
                type = ((CtfEnum)type).Type;

            CtfInteger intType = (CtfInteger)type;
            return intType._ctfIntegerReaderImpl.ReadAndConvert<T>(buffer, bitOffset);
        }
    }
}
