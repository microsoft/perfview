using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;

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

    static class IntHelpers
    {
        public static int AlignUp(int val, int alignment)
        {
            Debug.Assert(val >= 0 && alignment >= 0);

            // alignment must be a power of 2 for this implementation to work (need modulo otherwise)
            Debug.Assert(0 == (alignment & (alignment - 1)));
            int result = (val + (alignment - 1)) & ~(alignment - 1);
            Debug.Assert(result >= val);      // check for overflow

            return result;
        }

        public static long AlignUp(long val, int alignment)
        {
            Debug.Assert(val >= 0 && alignment >= 0);

            // alignment must be a power of 2 for this implementation to work (need modulo otherwise)
            Debug.Assert(0 == (alignment & (alignment - 1)));
            long result = (val + (alignment - 1)) & ~(alignment - 1);
            Debug.Assert(result >= val);      // check for overflow

            return result;
        }

        public static int AlignDown(int val, int alignment)
        {
            Debug.Assert(val >= 0 && alignment >= 0);
            return val & ~(alignment - 1);
        }
    }

    abstract class CtfMetadataType
    {
        public int BitOffset { get; set; }
        public abstract int Align { get; }

        public CtfTypes CtfType { get; protected set; }

        public CtfMetadataType(CtfTypes type)
        {
            CtfType = type;
        }

        internal abstract CtfMetadataType ResolveReference(Dictionary<string, CtfMetadataType> typealias);
        public abstract int GetSize();
    }

    /// <summary>
    /// Represents a type which has been referenced by name, but has not yet been resolved to a concrete type.
    /// </summary>
    class CtfUnresolvedType : CtfMetadataType
    {
        public string Name { get; private set; }

        public override int Align
        {
            get
            {
                throw new InvalidOperationException();
            }
        }

        public CtfUnresolvedType(string name)
            : base(CtfTypes.Unresolved)
        {
            Name = name;
        }

        public CtfUnresolvedType(CtfTypes type) : base(type)
        {
        }

        public override string ToString()
        {
            return Name;
        }

        internal override CtfMetadataType ResolveReference(Dictionary<string, CtfMetadataType> typealias)
        {
            return typealias[Name];
        }

        public override int GetSize()
        {
            throw new InvalidOperationException();
        }
    }

    class CtfArray : CtfMetadataType
    {
        public override int Align
        {
            get
            {
                return Type.Align;
            }
        }

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

        public override int GetSize()
        {
            int size = Type.GetSize();
            if (size == CtfEvent.SizeIndeterminate)
                return CtfEvent.SizeIndeterminate;

            int len;
            if (!int.TryParse(Index, out len) || len < 0)
                return CtfEvent.SizeIndeterminate;

            return size * len;
        }
    }

    class CtfFloat : CtfMetadataType
    {
        int _align;
        public override int Align
        {
            get
            {
                return _align;
            }
        }

        public CtfFloat(CtfPropertyBag bag)
            : base(CtfTypes.Float)
        {
            Exp = bag.GetInt("exp_dig");
            Mant = bag.GetInt("mant_dig");
            ByteOrder = bag.GetString("byte_order");
            _align = bag.GetInt("align");
        }

        public string ByteOrder { get; private set; }
        public int Exp { get; private set; }
        public int Mant { get; private set; }
        internal override CtfMetadataType ResolveReference(Dictionary<string, CtfMetadataType> typealias)
        {
            return this;
        }

        public override int GetSize()
        {
            return Exp + Mant;
        }
    }

    class CtfString : CtfMetadataType
    {
        public int Length { get; set; }
        public override int Align
        {
            get
            {
                return 8;
            }
        }

        public bool IsAscii { get { return false; } }

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

        public override int GetSize()
        {
            return CtfEvent.SizeIndeterminate;
        }
    }

    class CtfEnum : CtfMetadataType
    {
        public override int Align
        {
            get
            {
                return Type.Align;
            }
        }

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

        internal string GetName(int value)
        {
            foreach (CtfNamedRange range in Values)
                if (range.Begin <= value && value <= range.End)
                    return range.Name;

            throw new IndexOutOfRangeException();
        }

        public override int GetSize()
        {
            return Type.GetSize();
        }

        internal CtfNamedRange GetValue(string name)
        {
            return Values.Single(p => p.Name == name);
        }
    }

    class CtfStruct : CtfMetadataType
    {
        int _align;
        public override int Align
        {
            get
            {
                return _align;
            }
        }


        bool _resolved = false;

        public CtfField[] Fields { get; private set; }

        public CtfStruct(CtfPropertyBag props, CtfField[] fields)
            : base(CtfTypes.Struct)
        {
            int alignment = 1;
            if (props != null)
                alignment = props.GetIntOrNull("align") ?? 1;

            _align = alignment;
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

        internal CtfField GetField(string name)
        {
            for (int index = 0; index < Fields.Length; index++)
                if (Fields[index].Name == name)
                    return Fields[index];

            return null;
        }

        public override int GetSize()
        {
            int size = 0;
            foreach (CtfField field in Fields)
            {
                int tmp = field.Type.GetSize();
                size += tmp;

                if (tmp == CtfEvent.SizeIndeterminate)
                    return CtfEvent.SizeIndeterminate;
            }

            return size;
        }

        internal int GetFieldOffset(string name)
        {
            int offset = 0;
            foreach (CtfField field in Fields)
            {
                if (field.Name == name)
                    return offset;

                int tmp = field.Type.GetSize();
                offset += tmp;

                if (tmp == CtfEvent.SizeIndeterminate)
                    return CtfEvent.SizeIndeterminate;
            }

            throw new ArgumentException();
        }
    }

    class CtfVariant : CtfMetadataType
    {
        public override int Align
        {
            get
            {
                return 1;
            }
        }

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

        internal CtfField GetVariant(string name)
        {
            // PERF: this method is on the critical path.
            // Prefer inlining Single code to benefit from array/foreach optim and avoid
            // allocating enumerator.
            CtfField currentField = null;
            int nbFields = 0;
            foreach (var ctfField in Union)
            {
                if (ctfField.Name == name)
                {
                    currentField = ctfField;
                    nbFields++;
                }
            }

            if (nbFields == 0)
                throw new InvalidOperationException($"No field was found for the name {name}");

            if (nbFields > 1)
                throw new InvalidOperationException($"more than one CtfField was found for the name {name}");
            return currentField;
        }

        public override int GetSize()
        {
            int size = int.MinValue;

            foreach (CtfField field in Union)
            {
                int curr = field.Type.GetSize();
                if (curr == CtfEvent.SizeIndeterminate)
                    return CtfEvent.SizeIndeterminate;

                if (size == int.MinValue)
                    size = curr;
                else if (size != curr)
                    return CtfEvent.SizeIndeterminate;
            }

            return size;
        }
    }

    class CtfField
    {
        public int BitOffset { get; set; }
        public CtfMetadataType Type { get; private set; }
        public string Name { get; private set; }

        public CtfField(CtfMetadataType type, string name)
        {
            Type = type;
            Name = name;
        }

        public override string ToString()
        {
            return Type.ToString() + " " + Name;
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
