using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Tracing.Ctf
{
    /// <summary>
    /// The abstract metadata parser class.
    /// </summary>
    internal abstract class CtfMetadataParser
    {
        public abstract IEnumerable<CtfMetadataDeclaration> Parse();
    }

    /// <summary>
    /// The types that may be declared in CtfMetatdata.
    /// </summary>
    internal enum CtfDeclarationTypes
    {
        Unknown,
        TypeAlias,
        Trace,
        Environment,
        Clock,
        Struct,
        Stream,
        Event,
    }

    /// <summary>
    /// This class represents the top level entry 
    /// </summary>
    internal class CtfMetadataDeclaration
    {
        public CtfDeclarationTypes Definition { get; private set; }
        public CtfPropertyBag Properties { get; private set; }
        public CtfMetadataType Type { get; private set; }
        public string Name { get; private set; }
        public string RawText { get; private set; }
        public CtfField[] Fields { get; private set; }

        public CtfMetadataDeclaration(CtfDeclarationTypes declaration, CtfMetadataType type, string name, string text)
        {
            Definition = declaration;
            Type = type;
            Name = name;
            RawText = text;

            Debug.Assert(name == null || name == name.Trim());
        }

        public CtfMetadataDeclaration(CtfDeclarationTypes declaration, CtfPropertyBag bag, string name, string text)
        {
            Definition = declaration;
            Properties = bag;
            Name = name;
            RawText = text;

            Debug.Assert(name == null || name == name.Trim());
        }

        public CtfMetadataDeclaration(CtfDeclarationTypes declaration, CtfPropertyBag bag, CtfField[] fields, string name, string text)
        {
            Definition = declaration;
            Fields = fields;
            Name = name;
            RawText = text;
            Properties = bag;

            Debug.Assert(name == null || name == name.Trim());
        }
    }

    /// <summary>
    /// A simple class to make parsing out properties easier.
    /// </summary>
    internal class CtfPropertyBag
    {
        private Dictionary<string, string> _properties = new Dictionary<string, string>();
        private Dictionary<string, CtfMetadataType> _typeProperties = new Dictionary<string, CtfMetadataType>();

        public CtfPropertyBag()
        {
        }

        public void Clear()
        {
            _properties.Clear();
            _typeProperties.Clear();
        }

        public bool GetBoolean(string name)
        {
            string value = _properties[name];

            switch (value)
            {
                case "0":
                    return false;

                case "1":
                    return true;

                default:
                    return bool.Parse(value);
            }
        }

        public short GetShort(string name)
        {
            string value = _properties[name];
            return short.Parse(value);
        }

        public short? GetShortOrNull(string name)
        {
            string value;
            _properties.TryGetValue(name, out value);

            if (value == null)
            {
                return null;
            }

            return short.Parse(value);
        }

        public string GetString(string name)
        {
            string value;
            _properties.TryGetValue(name, out value);
            return value;
        }

        public int? GetIntOrNull(string name)
        {
            string value;
            _properties.TryGetValue(name, out value);

            if (value == null)
            {
                return null;
            }

            return int.Parse(value);
        }

        public int GetInt(string name)
        {
            return int.Parse(_properties[name]);
        }

        public ulong GetUlong(string name)
        {
            return ulong.Parse(_properties[name]);
        }

        public CtfMetadataType GetType(string name)
        {
            CtfMetadataType value;
            _typeProperties.TryGetValue(name, out value);
            return value;
        }

        public CtfStruct GetStruct(string name)
        {
            CtfMetadataType value;
            _typeProperties.TryGetValue(name, out value);
            return (CtfStruct)value;
        }

        internal void AddValue(string name, string value)
        {
            _properties.Add(name, value);
        }

        internal void AddValue(string name, CtfMetadataType value)
        {
            _typeProperties[name] = value;
        }

        internal uint? GetUIntOrNull(string name)
        {
            string value;
            _properties.TryGetValue(name, out value);

            if (value == null)
            {
                return null;
            }

            return uint.Parse(value);
        }

        internal uint GetUInt(string name)
        {
            return uint.Parse(_properties[name]);
        }
    }
}
