using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Diagnostics.Tracing.Ctf
{
    /// <summary>
    /// A manual parser for CtfMetadata.  Eventually this should be replaced when CtfMetadata no longer
    /// uses a custom, BNF style format.
    /// </summary>
    internal class CtfMetadataLegacyParser : CtfMetadataParser, IDisposable
    {
        private static readonly Regex s_align = new Regex(@"align\( *(\d+) *\)");
        private static readonly Regex s_integer = new Regex(@"integer (\{[^}]*\}) (\w+)");
        private static readonly Regex s_variable = new Regex(@"((\w+ +)+) *(\w+)");
        private static readonly Regex s_variant = new Regex(@"variant +< *(\w+) *>");
        private static readonly Regex s_enum = new Regex(@"enum +: +((\w+ +)+) *");
        private static readonly Regex s_range = new Regex(@"(\w+) += *(\d+)( *... (\d+))?");
        private static readonly Regex s_struct = new Regex(@"^\s*struct\s(\{|\w+)");
        private static readonly Regex s_float = new Regex(@"floating_point +\{");

        private Stream _stream;

        public CtfMetadataLegacyParser(string filename)
        {
            _stream = File.OpenRead(filename);
        }

        public CtfMetadataLegacyParser(Stream stream)
        {
            _stream = stream;
        }

        public override IEnumerable<CtfMetadataDeclaration> Parse()
        {
            string metadata = GetMetadata();
            // add new line before string in case some metadata doesn't start with \n (kernel metadata)
            metadata = "\n" + metadata;
            int index = 0;

            CtfMetadataDeclaration declaration;
            while ((declaration = ParseOneDeclaration(metadata, index, out index)) != null)
            {
                yield return declaration;
            }
        }

        private CtfMetadataDeclaration ParseOneDeclaration(string metadata, int index, out int end)
        {
            end = -1;
            int open = metadata.IndexOf('{', index);
            if (open == -1)
            {
                return null;
            }

            int start = metadata.Substring(0, open).LastIndexOf('\n') + 1;
            if (start == 0)
            {
                return null;
            }

            CtfDeclarationTypes directive = CtfDeclarationTypes.Unknown;
            string[] directiveElements = metadata.Substring(start, open - start).Trim().Split(' ');
            string directiveString = directiveElements[0];

            string name = null;
            switch (directiveString)
            {
                case "trace":
                    directive = CtfDeclarationTypes.Trace;
                    break;

                case "typealias":
                    directive = CtfDeclarationTypes.TypeAlias;

                    int closeBrace = FindCloseBrace(metadata, open) + 1;

                    CtfPropertyBag bag = GetPropertyBag(metadata, open, closeBrace);

                    CtfMetadataType t = null;
                    switch (directiveElements[1])
                    {
                        case "integer":
                            t = new CtfInteger(bag);
                            break;

                        default:
                            throw new IOException();
                    }

                    int colonEquals = metadata.IndexOf(":=", closeBrace) + 2;

                    int semi = colonEquals;
                    while (metadata[++semi] != ';')
                    {
                        ;
                    }

                    name = metadata.Substring(colonEquals, semi - colonEquals).Trim();

                    end = semi + 1;

                    return new CtfMetadataDeclaration(CtfDeclarationTypes.TypeAlias, t, name, directiveElements[1]);

                case "env":
                    directive = CtfDeclarationTypes.Environment;
                    break;

                case "clock":
                    directive = CtfDeclarationTypes.Clock;
                    break;

                case "struct":
                    directive = CtfDeclarationTypes.Struct;
                    name = directiveElements[1];
                    break;

                case "stream":
                    directive = CtfDeclarationTypes.Stream;
                    break;

                case "event":
                    directive = CtfDeclarationTypes.Event;
                    break;

                default:
                    break;
            }

            int close = FindCloseBrace(metadata, open);
            int curr = close;

            while (metadata[curr++] != ';')
            {
                ;
            }

            int nameStart = metadata.IndexOf(":=", close);
            if (name == null && nameStart != -1 && nameStart < curr)
            {
                nameStart += 2; // move past :=
                name = metadata.Substring(nameStart, curr - nameStart - 1).Trim();
            }

            Debug.Assert(metadata[open] == '{');
            Debug.Assert(metadata[close] == '}');

            end = curr;
            if (directive == CtfDeclarationTypes.Struct)
            {

                CtfPropertyBag bag = null;
                Match match = s_align.Match(metadata, close, end - close);
                if (match.Success)
                {
                    bag = new CtfPropertyBag();
                    bag.AddValue("align", match.Groups[1].ToString());
                }

                CtfField[] fields = ParseStructFields(metadata, open, close).ToArray();
                return new CtfMetadataDeclaration(directive, bag, fields, name, metadata.Substring(index, curr - index));
            }
            else
            {
                CtfPropertyBag properties = GetPropertyBag(metadata, open, close);
                return new CtfMetadataDeclaration(directive, properties, name, metadata.Substring(index, curr - index));
            }
        }

        private IEnumerable<CtfField> ParseStructFields(string metadata, int begin, int end)
        {
            Debug.Assert(metadata[begin] == '{');
            foreach (string statement in EnumerateStatements(metadata, begin + 1, end))
            {
                int index;
                string name = null;
                CtfMetadataType type = ParseOneType(statement, out index);
                name = statement.Substring(index).Trim();

                Debug.Assert(type != null);
                Debug.Assert(name != null);

                int openBracket = name.IndexOf('[') + 1;
                int closeBracket = openBracket > 0 ? name.IndexOf(']', openBracket + 1) : -1;

                if (closeBracket != -1)
                {
                    string arrayVal = name.Substring(openBracket, closeBracket - openBracket).Trim();
                    name = name.Substring(0, openBracket - 1).Trim();

                    type = new CtfArray(type, arrayVal);
                }


                yield return new CtfField(type, name);
            }
        }

        private CtfMetadataType ParseOneType(string statement, int start, out int index)
        {
            return ParseOneType(statement.Substring(start), out index);
        }

        private CtfMetadataType ParseOneType(string statement, out int index)
        {
            CtfMetadataType type = null;
            Match match;
            if ((match = s_integer.Match(statement)).Success)
            {
                Group group = match.Groups[1];
                CtfPropertyBag bag = GetPropertyBag(group.ToString());

                type = new CtfInteger(bag);
                index = group.Index + group.Length;
            }
            else if ((match = s_struct.Match(statement)).Success)
            {
                var group = match.Groups[1];

                if (group.ToString() != "{")
                {
                    throw new InvalidOperationException();
                }

                int open = group.Index;
                int close = FindCloseBrace(statement, open);

                CtfField[] fields = ParseStructFields(statement, open, close).ToArray();

                type = new CtfStruct(null, fields);
                index = close + 1;
            }
            else if ((match = s_float.Match(statement)).Success)
            {
                int open = match.Index + match.Length - 1;
                int close = FindCloseBrace(statement, open);

                CtfPropertyBag bag = GetPropertyBag(statement, open, close);
                type = new CtfFloat(bag);
                index = close + 1;
            }
            else if ((match = s_variant.Match(statement)).Success)
            {
                string switchVariable = match.Groups[1].ToString();

                int open = statement.IndexOf('{');
                int close = FindCloseBrace(statement, open);

                if (close == -1)
                {
                    throw new InvalidOperationException();
                }

                CtfField[] fields = ParseStructFields(statement, open, close).ToArray();

                type = new CtfVariant(switchVariable, fields);
                index = close + 1;
            }
            else if ((match = s_variable.Match(statement)).Success)
            {
                var typeGroup = match.Groups[1];

                string typeName = typeGroup.ToString().Trim();
                if (typeName == "string")
                {
                    type = new CtfString();
                }
                else
                {
                    type = new CtfUnresolvedType(typeName);
                }

                index = typeGroup.Index + typeGroup.Length;
            }
            else if ((match = s_enum.Match(statement)).Success)
            {
                var groups = match.Groups;
                string typeName = groups[1].ToString().Trim();

                int open = statement.IndexOf('{');
                int close = FindCloseBrace(statement, open);
                if (close == -1)
                {
                    throw new InvalidOperationException();
                }

                CtfNamedRange[] ranges = ParseNamedRanges(statement, open + 1, close).ToArray();

                // TODO: Can enums just be an inline defined integer?
                type = new CtfEnum(new CtfUnresolvedType(typeName), ranges);
                index = close + 1;
            }
            else
            {
                // TODO:  Floating point

                index = 0;
                return null;
            }

            return type;
        }

        private IEnumerable<CtfNamedRange> ParseNamedRanges(string str, int open, int close)
        {
            Debug.Assert(str[open] != '{');
            foreach (string statement in EnumerateStatements(str, open, close, ','))
            {
                Match match = s_range.Match(statement);
                if (match.Success)
                {
                    var groups = match.Groups;
                    string name = groups[1].ToString();
                    int start = int.Parse(groups[2].ToString());
                    int end = groups[4].Success ? int.Parse(groups[4].ToString()) : start;

                    yield return new CtfNamedRange(name, start, end);
                }

                Debug.Assert(match.Success);
            }
        }

        private CtfPropertyBag GetPropertyBag(string str)
        {
            Debug.Assert(str[0] == '{');
            return GetPropertyBag(str, 0, str.Length);
        }

        private CtfPropertyBag GetPropertyBag(string str, int start, int stop)
        {
            Debug.Assert(str[start] == '{');
            CtfPropertyBag result = new CtfPropertyBag();

            foreach (string rawStatement in EnumerateStatements(str, start + 1, stop))
            {
                string statement = StripComments(rawStatement);

                int i = statement.IndexOf('=');
                if (i <= 0)
                {
                    continue;
                }

                if (statement[i - 1] == ':')
                {
                    string name = statement.Substring(0, i - 1).Trim();

                    int open = statement.IndexOf('{', i + 1);
                    int close = FindCloseBrace(statement, open);

                    if (close > stop || close == -1)
                    {
                        string[] structNameParts = statement.Substring(i + 1).Trim().Split(' ');

                        if (structNameParts.Length != 2 || structNameParts[0] != "struct")
                        {
                            throw new InvalidOperationException();
                        }

                        CtfUnresolvedType unresolved = new CtfUnresolvedType(structNameParts[1]);
                        result.AddValue(name, unresolved);
                    }
                    else
                    {
                        CtfField[] fields = ParseStructFields(statement, open, close).ToArray();
                        result.AddValue(name, new CtfStruct(null, fields));
                    }
                }
                else
                {
                    string name = statement.Substring(0, i).Trim();
                    string value = statement.Substring(i + 1).Trim();

                    if (value.Length > 2 && value[0] == '\"' && value[value.Length - 1] == '\"')
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    result.AddValue(name, value);
                }
            }

            return result;
        }

        private static string StripComments(string rawStatement)
        {
            StringBuilder sb = null;
            int comment = rawStatement.IndexOf("/*");
            int index = 0;
            while (comment != -1)
            {
                int commentEnd = rawStatement.IndexOf("*/", comment + 2) + 2;
                if (commentEnd == -1)
                {
                    throw new BadImageFormatException();
                }

                if (sb == null)
                {
                    sb = new StringBuilder(rawStatement.Length);
                }

                sb.Append(rawStatement.Substring(index, comment - index));
                index = commentEnd;
                comment = rawStatement.IndexOf("/*", index);

                if (comment == -1)
                {
                    sb.Append(rawStatement.Substring(index));
                }
            }

            string statement = sb != null ? sb.ToString() : rawStatement;
            return statement;
        }

        public unsafe string GetMetadata()
        {
            // TODO:  Currently we read all of metadata and then parse it.  We could do better by
            //        only reading one packet at a time.
            byte[] headerBufer = new byte[Marshal.SizeOf(typeof(MetadataPacketHeader))];
            byte[] buffer = null;

            StringBuilder sb = new StringBuilder();
            while (true)
            {
                if (_stream.Read(headerBufer, 0, headerBufer.Length) != headerBufer.Length)
                {
                    break;
                }

                MetadataPacketHeader header;
                fixed (byte* ptr = headerBufer)
                {
                    header = *((MetadataPacketHeader*)ptr);
                }

                if (header.Magic != 0x75d11d57)
                {
                    throw new IOException();
                }

                int packetSize = (int)header.PacketSize / 8 - headerBufer.Length;

                if (buffer == null || buffer.Length < packetSize)
                {
                    buffer = new byte[packetSize];
                }

                int read = _stream.Read(buffer, 0, packetSize);
                if (read == 0)
                {
                    break;
                }

                int contentSize = (int)header.ContentSize / 8 - headerBufer.Length;
                if (contentSize < read)
                {
                    read = contentSize;
                }

                string result = Encoding.ASCII.GetString(buffer, 0, read);

                sb.Append(result);
            }

            return sb.ToString();
        }

        private static int FindCloseBrace(string str, int open)
        {
            if (open == -1)
            {
                return -1;
            }

            Debug.Assert(str[open] == '{');

            int braces = 1;
            int end = open + 1;
            while (braces != 0)
            {
                char curr = str[end++];
                if (curr == '"')
                {
                    while (str[end++] != '"')
                    {
                        curr = str[end + 1];
                    }
                }

                if (curr == '{')
                {
                    braces++;
                }
                else if (curr == '}')
                {
                    braces--;
                }

                if (curr == '/' && str[end] == '*')
                {
                    curr = str[end++];
                    while (str[end++] != '*' && str[end] != '/')
                    {
                        ;
                    }
                }
            }

            end--;

            Debug.Assert(str[end] == '}');
            return end;
        }

        private static IEnumerable<string> EnumerateStatements(string str, int start, int stop, char delimiter = ';')
        {
            Debug.Assert(str[start] != '{');
            int index = start;
            while (start <= index && index < stop)
            {
                if (str[index] == '{')
                {
                    index = FindCloseBrace(str, index);
                    continue;
                }
                else if (index + 1 < stop && str[index] == '/' && str[index + 1] == '*')
                {
                    index = str.IndexOf("*/", index) + 2;
                    continue;
                }
                else if (str[index] == delimiter)
                {
                    yield return str.Substring(start, index - start).Trim().Replace('\n', ' ');
                    index++;
                    start = index;
                }
                else
                {
                    index++;
                }
            }

            string last = str.Substring(start, stop - start);
            if (!string.IsNullOrWhiteSpace(last))
            {
                yield return last;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream.Dispose();
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MetadataPacketHeader
        {
            public uint Magic;
            public Guid Uuid;
            public uint Checksum;
            public uint ContentSize;
            public uint PacketSize;
            public byte CompressionScheme;
            public byte EncryptionScheme;
            public byte ChecksumScheme;
            public byte Major;
            public byte Minor;
        }
    }
}
