using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.Ctf
{
    enum CtfTypes
    {
        Unknown,
        Unresolved,
        String,
        Integer,
        Struct,
        Array,
        Enum,
        Variant,
        Float
    }

    abstract class CtfMetadataType
    {
        public CtfTypes CtfType { get; protected set; }

        public CtfMetadataType(CtfTypes type)
        {
            CtfType = type;
        }

        internal abstract CtfMetadataType ResolveReference(Dictionary<string, CtfMetadataType> typealias);
    }

    class CtfUnresolvedType : CtfMetadataType
    {
        public string Name { get; private set; }

        public CtfUnresolvedType(string name)
            : base(CtfTypes.Unresolved)
        {
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }

        internal override CtfMetadataType ResolveReference(Dictionary<string, CtfMetadataType> typealias)
        {
            return typealias[Name];
        }
    }

    class CtfArray : CtfMetadataType
    {
        public CtfArray(CtfMetadataType type, string index)
            : base(CtfTypes.Array)
        {
            Type = type;
            Index = index;
        }

        public CtfMetadataType Type { get; private set; }
        public string Index { get; private set; }

        public override string ToString()
        {
            return string.Format("{0}[{1}]", Type, Index);
        }

        internal override CtfMetadataType ResolveReference(Dictionary<string, CtfMetadataType> typealias)
        {
            Type = Type.ResolveReference(typealias);
            return this;
        }
    }

    class CtfFloat : CtfMetadataType
    {
        public CtfFloat(CtfPropertyBag bag)
            : base(CtfTypes.Float)
        {
            Exp = bag.GetInt("exp_dig");
            Mant = bag.GetInt("mant_dig");
            ByteOrder = bag.GetString("byte_order");
            Align = bag.GetShortOrNull("align");
        }

        public short? Align { get; private set; }
        public string ByteOrder { get; private set; }
        public int Exp { get; private set; }
        public int Mant { get; private set; }

        internal override CtfMetadataType ResolveReference(Dictionary<string, CtfMetadataType> typealias)
        {
            return this;
        }
    }

    class CtfInteger : CtfMetadataType
    {
        public CtfInteger(CtfPropertyBag bag)
            : base(CtfTypes.Integer)
        {
            Size = bag.GetShort("size");
            Align = bag.GetShortOrNull("align") ?? 8;
            Signed = bag.GetBoolean("signed");
            Encoding = bag.GetString("encoding") ?? "none";
            Base = bag.GetShortOrNull("base") ?? 10;
            Map = bag.GetString("map");
        }

        public short Size { get; private set; }
        public short Align { get; private set; }
        public bool Signed { get; private set; }
        public string Encoding { get; private set; }
        public short Base { get; private set; }
        public string Map { get; private set; }

        public override string ToString()
        {
            return (Signed ? "int" : "uint") + Size.ToString();
        }

        internal override CtfMetadataType ResolveReference(Dictionary<string, CtfMetadataType> typealias)
        {
            return this;
        }
    }

    class CtfString : CtfMetadataType
    {
        public CtfString()
            : base(CtfTypes.String)
        {
        }

        public override string ToString()
        {
            return "string";
        }

        internal override CtfMetadataType ResolveReference(Dictionary<string, CtfMetadataType> typealias)
        {
            return this;
        }
    }

    class CtfEnum : CtfMetadataType
    {
        public CtfEnum(CtfMetadataType type, CtfNamedRange[] ranges)
            : base(CtfTypes.Enum)
        {
            Type = type;
            Values = ranges;
        }

        public CtfMetadataType Type { get; private set; }
        public CtfNamedRange[] Values { get; private set; }

        internal override CtfMetadataType ResolveReference(Dictionary<string, CtfMetadataType> typealias)
        {
            Type = Type.ResolveReference(typealias);
            return this;
        }
    }

    class CtfStruct : CtfMetadataType
    {
        bool _resolved = false;

        public CtfField[] Fields { get; private set; }

        public CtfStruct(CtfField[] fields)
            : base(CtfTypes.Struct)
        {
            Fields = fields;
        }

        internal override CtfMetadataType ResolveReference(Dictionary<string, CtfMetadataType> typealias)
        {
            if (!_resolved)
                foreach (CtfField field in Fields)
                    field.ResolveReference(typealias);

            _resolved = true;
            return this;
        }
    }

    class CtfVariant : CtfMetadataType
    {
        bool _resolved = false;
        public CtfVariant(string switchName, CtfField[] union)
            : base(CtfTypes.Variant)
        {
            Switch = switchName;
            Union = union;
        }

        public string Switch { get; private set; }
        public CtfField[] Union { get; private set; }

        internal override CtfMetadataType ResolveReference(Dictionary<string, CtfMetadataType> typealias)
        {
            if (!_resolved)
                foreach (CtfField field in Union)
                    field.ResolveReference(typealias);

            _resolved = true;
            return this;
        }
    }

    class CtfField
    {
        public CtfMetadataType Type { get; private set; }
        public string Name { get; private set; }

        public CtfField(CtfMetadataType type, string name)
        {
            Type = type;
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }

        internal void ResolveReference(Dictionary<string, CtfMetadataType> typealias)
        {
            Type = Type.ResolveReference(typealias);
        }
    }

    struct CtfNamedRange
    {
        public string Name;
        public int Begin;
        public int End;

        public CtfNamedRange(string name, int begin, int end)
        {
            Name = name;
            Begin = begin;
            End = end;
        }

        public override string ToString()
        {
            if (Begin == End)
                return string.Format("{0} = {1}", Name, Begin);

            return string.Format("{0} = {1} ... {2}", Name, Begin, End);
        }
    }
}
