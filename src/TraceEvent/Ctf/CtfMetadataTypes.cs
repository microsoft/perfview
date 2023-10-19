using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Microsoft.Diagnostics.Tracing.Ctf
{
    internal enum CtfTypes
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

    internal static class IntHelpers
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

        public static long AlignDown(long val, long alignment)
        {
            Debug.Assert(val >= 0 && alignment >= 0);
            return val & ~(alignment - 1);
        }
    }

    internal abstract class CtfMetadataType
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

        public abstract object Read(byte[] buffer);
    }

    /// <summary>
    /// Represents a type which has been referenced by name, but has not yet been resolved to a concrete type.
    /// </summary>
    internal class CtfUnresolvedType : CtfMetadataType
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

        public override object Read(byte[] buffer)
        {
            throw new InvalidOperationException();
        }
    }

    internal class CtfArray : CtfMetadataType
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
            {
                return CtfEvent.SizeIndeterminate;
            }

            int len;
            if (!int.TryParse(Index, out len) || len < 0)
            {
                return CtfEvent.SizeIndeterminate;
            }

            return size * len;
        }

        public override object Read(byte[] buffer)
        {
            return new byte[0];
        }
    }

    internal class CtfFloat : CtfMetadataType
    {
        private int _align;
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

        public override object Read(byte[] buffer)
        {
            // TODO: We don't support reading floats.
            return 0f;
        }
    }

    internal class CtfInteger : CtfMetadataType
    {
        private int _align;
        public override int Align
        {
            get
            {
                return _align;
            }
        }

        public CtfInteger(CtfPropertyBag bag)
            : base(CtfTypes.Integer)
        {
            Size = bag.GetShort("size");
            _align = bag.GetShortOrNull("align") ?? 8;
            Signed = bag.GetBoolean("signed");
            Encoding = bag.GetString("encoding") ?? "none";
            Base = bag.GetShortOrNull("base") ?? 10;
            Map = bag.GetString("map");
        }

        public short Size { get; private set; }
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

        public override int GetSize()
        {
            return Size;
        }

        public static T ReadInt<T>(CtfMetadataType type, byte[] buffer, int bitOffset) where T : IConvertible
        {
            if (type.CtfType == CtfTypes.Enum)
            {
                type = ((CtfEnum)type).Type;
            }

            CtfInteger intType = (CtfInteger)type;
            object result = intType.Read(buffer, bitOffset);
            object converted = ((IConvertible)result).ToType(typeof(T), null);
            return (T)converted;
        }

        public object Read(byte[] buffer, int bitOffset)
        {
            if (Size > 64)
            {
                throw new NotImplementedException();
            }

            Debug.Assert((bitOffset % Align) == 0);
            int byteOffset = bitOffset / 8;

            bool fastPath = (bitOffset % 8) == 0 && (Size % 8) == 0;
            if (fastPath)
            {
                if (Size == 32)
                {
                    if (Signed)
                    {
                        return ((IConvertible)BitConverter.ToInt32(buffer, byteOffset));
                    }

                    return BitConverter.ToUInt32(buffer, byteOffset);
                }

                if (Size == 8)
                {
                    if (Signed)
                    {
                        return (sbyte)buffer[byteOffset];
                    }

                    return buffer[byteOffset];
                }

                if (Size == 64)
                {
                    if (Signed)
                    {
                        return BitConverter.ToInt64(buffer, byteOffset);
                    }

                    return BitConverter.ToUInt64(buffer, byteOffset);
                }

                Debug.Assert(Size == 16);
                if (Signed)
                {
                    return BitConverter.ToInt16(buffer, byteOffset);
                }

                return BitConverter.ToUInt16(buffer, byteOffset);
            }


            // Sloooow path for misaligned integers
            int bits = Size;
            ulong value = 0;

            int byteLen = IntHelpers.AlignUp(bits, 8) / 8;

            for (int i = 0; i < byteLen; i++)
            {
                value = unchecked((value << 8) | buffer[byteOffset + byteLen - i - 1]);
            }

            value >>= bitOffset;
            value &= (ulong)((1 << bits) - 1);

            if (Signed)
            {
                ulong signBit = (1u << (bits - 1));

                if ((value & signBit) != 0)
                {
                    value |= ulong.MaxValue << bits;
                }
            }


            if (Size > 32)
            {
                if (Signed)
                {
                    return (long)value;
                }

                return value;
            }

            if (Size > 16)
            {
                if (Signed)
                {
                    return (int)value;
                }

                return (uint)value;
            }

            if (Size > 8)
            {
                if (Signed)
                {
                    return (short)value;
                }

                return (ushort)value;
            }

            if (Signed)
            {
                return (sbyte)value;
            }

            return (byte)value;
        }

        public override object Read(byte[] buffer)
        {
            return Read(buffer, BitOffset);
        }
    }

    internal class CtfString : CtfMetadataType
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

        public override object Read(byte[] buffer)
        {
            return Encoding.Unicode.GetString(buffer, BitOffset >> 3, Length);
        }
    }

    internal class CtfEnum : CtfMetadataType
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
            {
                if (range.Begin <= value && value <= range.End)
                {
                    return range.Name;
                }
            }

            throw new IndexOutOfRangeException();
        }

        public override int GetSize()
        {
            return Type.GetSize();
        }

        internal CtfNamedRange GetValue(string name)
        {
            return Values.Where(p => p.Name == name).Single();
        }

        public override object Read(byte[] buffer)
        {
            int value = CtfInteger.ReadInt<int>(Type, buffer, BitOffset);
            return GetName(value);
        }
    }

    internal class CtfStruct : CtfMetadataType
    {
        private int _align;
        public override int Align
        {
            get
            {
                return _align;
            }
        }

        private bool _resolved = false;

        public CtfField[] Fields { get; private set; }

        public CtfStruct(CtfPropertyBag props, CtfField[] fields)
            : base(CtfTypes.Struct)
        {
            _align = props?.GetIntOrNull("align") ?? 1;
            Fields = fields;
        }

        internal override CtfMetadataType ResolveReference(Dictionary<string, CtfMetadataType> typealias)
        {
            if (!_resolved)
            {
                foreach (CtfField field in Fields)
                {
                    field.ResolveReference(typealias);
                }
            }

            _resolved = true;
            return this;
        }

        internal CtfField GetField(string name)
        {
            for (int index = 0; index < Fields.Length; index++)
            {
                if (Fields[index].Name == name)
                {
                    return Fields[index];
                }
            }

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
                {
                    return CtfEvent.SizeIndeterminate;
                }
            }

            return size;
        }

        internal int GetFieldOffset(string name)
        {
            int offset = 0;
            foreach (CtfField field in Fields)
            {
                if (field.Name == name)
                {
                    return offset;
                }

                int tmp = field.Type.GetSize();
                offset += tmp;

                if (tmp == CtfEvent.SizeIndeterminate)
                {
                    return CtfEvent.SizeIndeterminate;
                }
            }

            throw new ArgumentException();
        }

        public override object Read(byte[] buffer)
        {
            throw new InvalidOperationException();
        }
    }

    internal class CtfVariant : CtfMetadataType
    {
        public override int Align
        {
            get
            {
                return 1;
            }
        }

        private bool _resolved = false;
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
            {
                foreach (CtfField field in Union)
                {
                    field.ResolveReference(typealias);
                }
            }

            _resolved = true;
            return this;
        }

        internal CtfField GetVariant(string name)
        {
            return Union.Where(f => f.Name == name).Single();
        }

        public override int GetSize()
        {
            int size = int.MinValue;

            foreach (CtfField field in Union)
            {
                int curr = field.Type.GetSize();
                if (curr == CtfEvent.SizeIndeterminate)
                {
                    return CtfEvent.SizeIndeterminate;
                }

                if (size == int.MinValue)
                {
                    size = curr;
                }
                else if (size != curr)
                {
                    return CtfEvent.SizeIndeterminate;
                }
            }

            return size;
        }

        public override object Read(byte[] buffer)
        {
            throw new NotImplementedException();
        }
    }

    internal class CtfField
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

        internal object Read(byte[] buffer)
        {
            return Type.Read(buffer);
        }
    }

    internal struct CtfNamedRange
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
            {
                return string.Format("{0} = {1}", Name, Begin);
            }

            return string.Format("{0} = {1} ... {2}", Name, Begin, End);
        }
    }
}
