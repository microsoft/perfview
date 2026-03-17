using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Symbols
{
    /// <summary>
    /// Demangles Rust symbol names that use the v0 mangling convention (RFC 2603).
    /// Handles symbols that start with the "_R" prefix.
    /// </summary>
    internal sealed class RustDemangler
    {
        private readonly Parser _parser;

        /// <summary>
        /// Creates a new RustDemangler instance. The instance can be reused
        /// to demangle multiple symbols without allocating a new parser each time.
        /// </summary>
        public RustDemangler()
        {
            _parser = new Parser();
        }

        /// <summary>
        /// Attempts to demangle a Rust v0 mangled name.
        /// Returns the demangled name on success, or null if the input is not a valid
        /// Rust v0 mangled symbol.
        /// </summary>
        public string Demangle(string mangledName)
        {
            if (mangledName == null || mangledName.Length < 3 || mangledName[0] != '_' || mangledName[1] != 'R')
            {
                return null;
            }

            // Compute end boundary to strip vendor-specific suffix (RFC 2603 §5.1).
            // LLVM appends suffixes like ".llvm.17209" that must be removed before parsing.
            // We pass endOffset to the parser instead of allocating a Substring.
            int endOffset = mangledName.Length;
            int dotIndex = mangledName.IndexOf('.', 2);
            if (dotIndex >= 0)
            {
                endOffset = dotIndex;
            }

            // The parser operates on the substring after "_R".
            // All backref positions are 0-indexed relative to this substring.
            _parser.Reset(mangledName, startOffset: 2, endOffset);

            try
            {
                string result = _parser.ParseSymbolName();
                if (_parser.HasMore) return null;
                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Recursive-descent parser for Rust v0 mangled symbol names.
        /// Operates on the full mangled string but uses <see cref="_baseOffset"/> to
        /// translate between absolute string positions and the backref coordinate space.
        /// </summary>
        private sealed class Parser
        {
            private string _input;
            private int _baseOffset;
            private int _pos;
            private int _endOffset;
            private int _depth;
            private const int MaxDepth = 200;

            // Reusable scratch buffer to avoid repeated StringBuilder allocations.
            private readonly StringBuilder _scratch = new StringBuilder(128);

            // Cached delegates for ParseBackref to avoid per-call Func allocation.
            private readonly Func<bool, string> _parsePathDelegate;
            private readonly Func<string> _parseTypeDelegate;
            private readonly Func<string> _parseConstDelegate;

            internal Parser()
            {
                _parsePathDelegate = ParsePath;
                _parseTypeDelegate = ParseType;
                _parseConstDelegate = ParseConst;
            }

            /// <summary>
            /// Clears and returns the shared scratch StringBuilder for building a result string.
            /// Callers must call ToString() and stop using the builder before calling any method
            /// that might also use _scratch (i.e. before recursive parsing calls).
            /// For methods that recurse (ParsePath, ParseType, etc.), create a local StringBuilder
            /// or use string.Concat — _scratch is only safe for leaf-level formatting.
            /// </summary>
            private StringBuilder Scratch()
            {
                _scratch.Clear();
                return _scratch;
            }

            public void Reset(string input, int startOffset, int endOffset)
            {
                _input = input;
                _baseOffset = startOffset;
                _pos = startOffset;
                _endOffset = endOffset;
                _depth = 0;
            }

            #region Low-level helpers

            internal bool HasMore => _pos < _endOffset;

            private char Peek()
            {
                if (_pos >= _endOffset)
                {
                    throw new InvalidOperationException("Unexpected end of input");
                }

                return _input[_pos];
            }

            private char Next()
            {
                if (_pos >= _endOffset)
                {
                    throw new InvalidOperationException("Unexpected end of input");
                }

                return _input[_pos++];
            }

            private bool Eat(char expected)
            {
                if (_pos < _endOffset && _input[_pos] == expected)
                {
                    _pos++;
                    return true;
                }

                return false;
            }

            private void EnterRecursion()
            {
                if (++_depth > MaxDepth)
                {
                    throw new InvalidOperationException("Recursion depth exceeded");
                }
            }

            private void LeaveRecursion()
            {
                _depth--;
            }

            private static bool IsHexDigit(char c)
            {
                return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
            }

            #endregion

            #region Number parsing

            /// <summary>
            /// Parses a base-62 encoded number.
            /// "_" = 0, single digit + "_" = digit_value + 1, multi-digit + "_" = base62(digits) + 1.
            /// Uses unchecked arithmetic because disambiguator hashes from real crate
            /// symbols can exceed <see cref="long.MaxValue"/>. Callers that need the
            /// numeric value (e.g. backrefs) validate the result independently.
            /// </summary>
            private long ParseBase62Number()
            {
                if (Eat('_'))
                {
                    return 0;
                }

                long n = 0;
                while (true)
                {
                    char c = Next();
                    if (c == '_')
                    {
                        return unchecked(n + 1);
                    }

                    long d;
                    if (c >= '0' && c <= '9')
                    {
                        d = c - '0';
                    }
                    else if (c >= 'a' && c <= 'z')
                    {
                        d = 10 + (c - 'a');
                    }
                    else if (c >= 'A' && c <= 'Z')
                    {
                        d = 36 + (c - 'A');
                    }
                    else
                    {
                        throw new InvalidOperationException("Invalid base-62 digit");
                    }

                    n = unchecked(n * 62 + d);
                }
            }

            /// <summary>
            /// Parses a decimal number per the v0 grammar:
            /// <c>&lt;decimal-number&gt; = "0" | &lt;nonzero-digit&gt; {&lt;digit&gt;}</c>.
            /// A leading zero is always a standalone value (0); multi-digit numbers
            /// must start with 1–9.
            /// </summary>
            private long ParseDecimalNumber()
            {
                if (_pos >= _endOffset || _input[_pos] < '0' || _input[_pos] > '9')
                {
                    throw new InvalidOperationException("Expected decimal number");
                }

                // A leading zero stands alone (RFC 2603 grammar).
                if (_input[_pos] == '0')
                {
                    _pos++;
                    return 0;
                }

                long n = 0;
                while (_pos < _endOffset && _input[_pos] >= '0' && _input[_pos] <= '9')
                {
                    long prev = n;
                    n = n * 10 + (_input[_pos] - '0');
                    if (n < prev)
                    {
                        throw new InvalidOperationException("Decimal number overflow");
                    }

                    _pos++;
                }

                return n;
            }

            /// <summary>
            /// Skips an optional decimal number (used for the encoding version after "_R").
            /// </summary>
            private void SkipOptionalDecimalNumber()
            {
                while (_pos < _endOffset && _input[_pos] >= '0' && _input[_pos] <= '9')
                {
                    _pos++;
                }
            }

            #endregion

            #region Identifier parsing

            /// <summary>
            /// Tries to parse a disambiguator ("s" followed by a base-62 number).
            /// Returns the disambiguator value, or -1 if none is present.
            /// </summary>
            private long TryParseDisambiguator()
            {
                if (_pos < _endOffset && _input[_pos] == 's')
                {
                    _pos++;
                    return ParseBase62Number();
                }

                return -1;
            }

            /// <summary>
            /// Parses an undisambiguated identifier: optional "u" prefix (Punycode),
            /// decimal length, optional "_" separator, then the raw bytes.
            /// </summary>
            private string ParseUndisambiguatedIdentifier()
            {
                bool isPunycode = Eat('u');
                long length = ParseDecimalNumber();

                // The "_" separator is present when the identifier bytes start with
                // a decimal digit or underscore to keep them unambiguous from the length.
                if (length > 0)
                {
                    Eat('_');
                }

                if (_pos + length > _endOffset)
                {
                    throw new InvalidOperationException("Identifier length exceeds input");
                }

                string bytes = _input.Substring(_pos, (int)length);
                _pos += (int)length;

                if (isPunycode)
                {
                    return DecodePunycode(bytes);
                }

                return bytes;
            }

            /// <summary>
            /// Parses an identifier (optional disambiguator + undisambiguated identifier).
            /// The disambiguator is consumed but not included in the returned name.
            /// </summary>
            private string ParseIdentifier()
            {
                TryParseDisambiguator();
                return ParseUndisambiguatedIdentifier();
            }

            #endregion

            #region Symbol / Path parsing

            /// <summary>
            /// Entry point: parses the symbol name after the "_R" prefix.
            /// Grammar: [decimal-number] path [instantiating-crate]
            /// </summary>
            public string ParseSymbolName()
            {
                SkipOptionalDecimalNumber();
                string result = ParsePath(inValue: true);

                // Optional instantiating crate suffix (RFC 2603) — parse and discard.
                if (HasMore)
                {
                    ParsePath(inValue: false);
                }

                // All input must be consumed; trailing garbage is invalid.
                if (HasMore)
                {
                    throw new InvalidOperationException("Trailing data after symbol name");
                }

                return result;
            }

            /// <summary>
            /// Parses a path production.
            /// </summary>
            /// <param name="inValue">
            /// True when the path is used as a value (function/method name) — affects
            /// whether generic arguments use turbofish syntax (e.g. "::&lt;T&gt;").
            /// </param>
            private string ParsePath(bool inValue)
            {
                EnterRecursion();
                try
                {
                    char tag = Next();
                    switch (tag)
                    {
                        case 'C': // Crate root
                            return ParseIdentifier();

                        case 'M': // Inherent impl: <Type>
                        {
                            TryParseDisambiguator();
                            ParsePath(inValue: false); // impl-path (not displayed)
                            string type = ParseType();
                            return Scratch().Append('<').Append(type).Append('>').ToString();
                        }

                        case 'X': // Trait impl: <Type as Trait>
                        {
                            TryParseDisambiguator();
                            ParsePath(inValue: false); // impl-path (not displayed)
                            string type = ParseType();
                            string trait_ = ParsePath(inValue: false);
                            return Scratch().Append('<').Append(type).Append(" as ").Append(trait_).Append('>').ToString();
                        }

                        case 'Y': // <Type as Path> (trait, no impl)
                        {
                            string type = ParseType();
                            string path = ParsePath(inValue: false);
                            return Scratch().Append('<').Append(type).Append(" as ").Append(path).Append('>').ToString();
                        }

                        case 'N': // Nested path
                        {
                            char ns = Next();
                            if (!((ns >= 'a' && ns <= 'z') || (ns >= 'A' && ns <= 'Z')))
                            {
                                throw new InvalidOperationException("Invalid namespace character");
                            }

                            string innerPath = ParsePath(inValue);
                            TryParseDisambiguator();
                            string name = ParseUndisambiguatedIdentifier();

                            // Closures and shims get special formatting.
                            string displayName;
                            if (ns == 'C')
                            {
                                displayName = name.Length > 0 ? string.Concat("{closure:", name, "}") : "{closure}";
                            }
                            else if (ns == 'S')
                            {
                                displayName = name.Length > 0 ? string.Concat("{shim:", name, "}") : "{shim}";
                            }
                            else
                            {
                                displayName = name;
                            }

                            return Scratch().Append(innerPath).Append("::").Append(displayName).ToString();
                        }

                        case 'I': // Generic args: path<arg1, arg2, ...>
                        {
                            string basePath = ParsePath(inValue);
                            string sep = inValue ? "::<" : "<";
                            var sb = new StringBuilder(basePath.Length + 64);
                            sb.Append(basePath);
                            sb.Append(sep);
                            bool first = true;
                            while (!Eat('E'))
                            {
                                if (!first) sb.Append(", ");
                                sb.Append(ParseGenericArg());
                                first = false;
                            }
                            if (first) return basePath;
                            sb.Append('>');
                            return sb.ToString();
                        }

                        case 'B': // Backref
                            return ParseBackref(_parsePathDelegate, inValue);

                        default:
                            throw new InvalidOperationException("Invalid path tag '" + tag + "'");
                    }
                }
                finally
                {
                    LeaveRecursion();
                }
            }

            /// <summary>
            /// Handles a backref by saving position, jumping to the referenced location,
            /// invoking the given parse function, and restoring position.
            /// </summary>
            private string ParseBackref(Func<bool, string> parseFunc, bool inValue)
            {
                long refPos = ParseBase62Number();
                if (refPos < 0 || refPos > _input.Length)
                {
                    throw new InvalidOperationException("Invalid backref position");
                }

                int absolutePos = _baseOffset + (int)refPos;

                // Backref must point to a position before the current parse position.
                if (absolutePos >= _pos || absolutePos < _baseOffset)
                {
                    throw new InvalidOperationException("Invalid backref position");
                }

                int savedPos = _pos;
                _pos = absolutePos;
                string result = parseFunc(inValue);
                _pos = savedPos;
                return result;
            }

            /// <summary>
            /// Overload for parse functions that don't take a bool parameter.
            /// </summary>
            private string ParseBackref(Func<string> parseFunc)
            {
                long refPos = ParseBase62Number();
                if (refPos < 0 || refPos > _input.Length)
                {
                    throw new InvalidOperationException("Invalid backref position");
                }

                int absolutePos = _baseOffset + (int)refPos;

                if (absolutePos >= _pos || absolutePos < _baseOffset)
                {
                    throw new InvalidOperationException("Invalid backref position");
                }

                int savedPos = _pos;
                _pos = absolutePos;
                string result = parseFunc();
                _pos = savedPos;
                return result;
            }

            #endregion

            #region Type parsing

            /// <summary>
            /// Parses a type production.
            /// </summary>
            private string ParseType()
            {
                EnterRecursion();
                try
                {
                    char c = Peek();

                    // Check for basic (primitive) types first — all lowercase single chars.
                    string basicType = GetBasicType(c);
                    if (basicType != null)
                    {
                        _pos++;
                        return basicType;
                    }

                    switch (c)
                    {
                        case 'A': // Array [T; N]
                            _pos++;
                        {
                            string elemType = ParseType();
                            string size = ParseConst();
                            return Scratch().Append('[').Append(elemType).Append("; ").Append(size).Append(']').ToString();
                        }

                        case 'S': // Slice [T]
                            _pos++;
                        {
                            string elemType = ParseType();
                            return Scratch().Append('[').Append(elemType).Append(']').ToString();
                        }

                        case 'T': // Tuple (T1, T2, ...)
                            _pos++;
                        {
                            var sb = new StringBuilder(64);
                            sb.Append('(');
                            int count = 0;
                            while (!Eat('E'))
                            {
                                if (count > 0) sb.Append(", ");
                                sb.Append(ParseType());
                                count++;
                            }
                            if (count == 0) return "()";
                            if (count == 1)
                            {
                                sb.Append(",)");
                                return sb.ToString();
                            }
                            sb.Append(')');
                            return sb.ToString();
                        }

                        case 'R': // Reference &T or &'a T
                            _pos++;
                        {
                            SkipOptionalLifetime();
                            string innerType = ParseType();
                            return "&" + innerType;
                        }

                        case 'Q': // Mutable reference &mut T
                            _pos++;
                        {
                            SkipOptionalLifetime();
                            string innerType = ParseType();
                            return "&mut " + innerType;
                        }

                        case 'P': // Raw pointer *const T
                            _pos++;
                        {
                            string innerType = ParseType();
                            return "*const " + innerType;
                        }

                        case 'O': // Raw pointer *mut T
                            _pos++;
                        {
                            string innerType = ParseType();
                            return "*mut " + innerType;
                        }

                        case 'F': // Function pointer fn(...)
                            _pos++;
                            return ParseFnSig();

                        case 'G': // Higher-ranked lifetime binder: G <base-62-number> F <fn-sig>
                            _pos++;
                            ParseBase62Number(); // skip lifetime count
                            if (!Eat('F'))
                            {
                                throw new InvalidOperationException("Expected 'F' after binder");
                            }

                            return ParseFnSig();

                        case 'D': // Dyn trait object
                            _pos++;
                            return ParseDynBounds();

                        case 'B': // Backref
                            _pos++;
                            return ParseBackref(_parseTypeDelegate);

                        default:
                            // Named type — parse as path (not in value context).
                            return ParsePath(inValue: false);
                    }
                }
                finally
                {
                    LeaveRecursion();
                }
            }

            /// <summary>
            /// Returns the Rust name for a basic type tag, or null if the character
            /// is not a basic type.
            /// </summary>
            private static string GetBasicType(char c)
            {
                switch (c)
                {
                    case 'a': return "i8";
                    case 'b': return "bool";
                    case 'c': return "char";
                    case 'd': return "f64";
                    case 'e': return "str";
                    case 'f': return "f32";
                    case 'h': return "u8";
                    case 'i': return "isize";
                    case 'j': return "usize";
                    case 'l': return "i32";
                    case 'm': return "u32";
                    case 'n': return "i128";
                    case 'o': return "u128";
                    case 'p': return "_";
                    case 's': return "i16";
                    case 't': return "u16";
                    case 'u': return "()";
                    case 'v': return "...";
                    case 'x': return "i64";
                    case 'y': return "u64";
                    case 'z': return "!";
                    default: return null;
                }
            }

            #endregion

            #region Lifetime

            /// <summary>
            /// Skips an optional lifetime ("L" + base-62 number). Lifetimes are not
            /// included in the demangled output.
            /// </summary>
            private void SkipOptionalLifetime()
            {
                if (_pos < _endOffset && _input[_pos] == 'L')
                {
                    _pos++;
                    ParseBase62Number();
                }
            }

            #endregion

            #region Generic args

            /// <summary>
            /// Parses a single generic argument (lifetime, type, or const).
            /// </summary>
            private string ParseGenericArg()
            {
                if (_pos < _endOffset && _input[_pos] == 'L')
                {
                    // Lifetime — skip and return placeholder.
                    _pos++;
                    ParseBase62Number();
                    return "'_";
                }

                if (_pos < _endOffset && _input[_pos] == 'K')
                {
                    // Const generic
                    _pos++;
                    return ParseConst();
                }

                return ParseType();
            }

            #endregion

            #region Const parsing

            /// <summary>
            /// Parses a const value (type + hex data, placeholder, or backref).
            /// </summary>
            private string ParseConst()
            {
                EnterRecursion();
                try
                {
                    char c = Peek();
                    if (c == 'p')
                    {
                        _pos++;
                        return "_";
                    }

                    if (c == 'B')
                    {
                        _pos++;
                        return ParseBackref(_parseConstDelegate);
                    }

                    // Parse the type to determine how to format the value.
                    string typeName = ParseType();
                    return ParseConstData(typeName);
                }
                finally
                {
                    LeaveRecursion();
                }
            }

            /// <summary>
            /// Parses const-data: optional "n" (negative), hex digits, terminated by "_".
            /// Formats the value based on the const's type.
            /// </summary>
            private string ParseConstData(string typeName)
            {
                bool negative = Eat('n');

                // Only signed integer types can have the negative prefix (RFC 2603).
                if (negative && (typeName == "u8" || typeName == "u16" || typeName == "u32" ||
                    typeName == "u64" || typeName == "u128" || typeName == "usize"))
                {
                    throw new InvalidOperationException("Invalid negative const for unsigned type");
                }

                int hexStart = _pos;
                while (_pos < _endOffset && IsHexDigit(_input[_pos]))
                {
                    _pos++;
                }

                if (!Eat('_'))
                {
                    throw new InvalidOperationException("Expected '_' after const data");
                }

                // -1 because Eat consumed the '_'.
                int hexLen = _pos - 1 - hexStart;

                if (typeName == "bool")
                {
                    if (negative)
                    {
                        throw new InvalidOperationException("Invalid negative bool const");
                    }

                    if (hexLen == 0 || (hexLen == 1 && _input[hexStart] == '0'))
                    {
                        return "false";
                    }

                    if (hexLen == 1 && _input[hexStart] == '1')
                    {
                        return "true";
                    }

                    throw new InvalidOperationException("Invalid boolean const value");
                }

                if (typeName == "char")
                {
                    if (negative)
                    {
                        throw new InvalidOperationException("Invalid negative char const");
                    }

                    if (hexLen <= 8)
                    {
                        ulong codePoint = hexLen > 0 ? ParseHexValue(_input, hexStart, hexLen) : 0;
                        if (codePoint <= 0x10FFFF)
                        {
                            try
                            {
                                return EscapeCharLiteral((int)codePoint);
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                // Surrogate range — fall through to hex display.
                            }
                        }
                    }
                }

                // Integer types — display in decimal for values that fit, hex otherwise.
                string prefix = negative ? "-" : "";

                if (hexLen == 0)
                {
                    return prefix + "0";
                }

                if (hexLen <= 16)
                {
                    ulong value = ParseHexValue(_input, hexStart, hexLen);
                    return prefix + value.ToString();
                }

                return prefix + "0x" + _input.Substring(hexStart, hexLen);
            }

            private static ulong ParseHexValue(string hex, int start, int length)
            {
                ulong n = 0;
                for (int i = start; i < start + length; i++)
                {
                    char c = hex[i];
                    int d = (c >= '0' && c <= '9') ? c - '0' : 10 + (c - 'a');
                    n = n * 16 + (ulong)d;
                }

                return n;
            }

            /// <summary>
            /// Formats a Unicode code point as an escaped Rust character literal.
            /// </summary>
            private static string EscapeCharLiteral(int codePoint)
            {
                switch (codePoint)
                {
                    case 0x00: return "'\\0'";
                    case 0x09: return "'\\t'";
                    case 0x0A: return "'\\n'";
                    case 0x0D: return "'\\r'";
                    case 0x27: return "'\\''";
                    case 0x5C: return "'\\\\'";
                    default:
                        if (codePoint < 0x20 || (codePoint >= 0x7F && codePoint <= 0x9F))
                        {
                            return string.Concat("'\\u{", codePoint.ToString("x"), "}'");
                        }

                        return "'" + char.ConvertFromUtf32(codePoint) + "'";
                }
            }

            #endregion

            #region Function signature

            /// <summary>
            /// Parses a function signature:
            /// <c>[binder] [unsafe] [extern "abi"] param-types "E" return-type</c>.
            /// The optional binder ("G" + base-62 count) encodes higher-ranked lifetimes
            /// (e.g. <c>for&lt;'a&gt; fn(&amp;'a str)</c>).
            /// </summary>
            private string ParseFnSig()
            {
                var sb = new StringBuilder(64);

                // Optional higher-ranked lifetime binder (RFC 2603: <binder> = "G" <base-62-number>).
                if (Eat('G'))
                {
                    ParseBase62Number(); // skip lifetime count
                }

                bool isUnsafe = Eat('U');
                if (isUnsafe)
                {
                    sb.Append("unsafe ");
                }

                if (Eat('K'))
                {
                    string abi = ParseAbi();
                    sb.Append("extern \"");
                    sb.Append(abi);
                    sb.Append("\" ");
                }

                sb.Append("fn(");

                bool firstParam = true;
                while (!Eat('E'))
                {
                    if (!firstParam) sb.Append(", ");
                    sb.Append(ParseType());
                    firstParam = false;
                }

                sb.Append(')');

                string returnType = ParseType();
                if (returnType != "()")
                {
                    sb.Append(" -> ");
                    sb.Append(returnType);
                }

                return sb.ToString();
            }

            /// <summary>
            /// Parses an ABI specifier: "C" for the C ABI, or an undisambiguated identifier.
            /// </summary>
            private string ParseAbi()
            {
                if (Eat('C'))
                {
                    return "C";
                }

                return ParseUndisambiguatedIdentifier();
            }

            #endregion

            #region Dyn trait bounds

            /// <summary>
            /// Parses dyn bounds: optional binder, trait list terminated by "E", then a lifetime.
            /// </summary>
            private string ParseDynBounds()
            {
                // Optional binder for higher-ranked lifetimes.
                long binderCount = 0;
                if (Eat('G'))
                {
                    binderCount = ParseBase62Number();
                }

                var sb = new StringBuilder(64);
                sb.Append("dyn ");                bool first = true;
                while (!Eat('E'))
                {
                    if (!first) sb.Append(" + ");
                    sb.Append(ParseDynTrait());
                    first = false;
                }

                // Mandatory lifetime bound (RFC 2603: <dyn-bounds> = ... "E" <lifetime>).
                // Parsed but not displayed in simplified output.
                if (!HasMore || _input[_pos] != 'L')
                {
                    throw new InvalidOperationException("Missing required lifetime in dyn bounds");
                }

                SkipLifetime();

                if (first)
                {
                    return "dyn";
                }

                return sb.ToString();
            }

            /// <summary>
            /// Parses a single dyn trait: a path followed by zero or more associated type bindings.
            /// </summary>
            private string ParseDynTrait()
            {
                string traitPath = ParsePath(inValue: false);

                var sb = new StringBuilder(traitPath.Length + 32);
                sb.Append(traitPath);
                sb.Append('<');
                bool first = true;
                while (_pos < _endOffset && _input[_pos] == 'p')
                {
                    _pos++;
                    string assocName = ParseUndisambiguatedIdentifier();
                    string assocType = ParseType();
                    if (!first) sb.Append(", ");
                    sb.Append(assocName);
                    sb.Append(" = ");
                    sb.Append(assocType);
                    first = false;
                }

                if (first) return traitPath;
                sb.Append('>');
                return sb.ToString();
            }

            /// <summary>
            /// Unconditionally parses and discards a lifetime ("L" + base-62 number).
            /// </summary>
            private void SkipLifetime()
            {
                if (_pos < _endOffset && _input[_pos] == 'L')
                {
                    _pos++;
                    ParseBase62Number();
                }
            }

            #endregion

            #region Punycode

            /// <summary>
            /// Decodes a Punycode-encoded identifier (RFC 3492) back to Unicode.
            /// Returns the raw input on decoding failure.
            /// </summary>
            private static string DecodePunycode(string input)
            {
                if (string.IsNullOrEmpty(input))
                {
                    return input;
                }

                const int Base = 36;
                const int TMax = 26;
                const int InitialBias = 72;
                const int InitialN = 0x80;

                // Split at the last '_': everything before is the basic (ASCII) portion,
                // everything after is the encoded (delta) portion.
                int lastUnderscore = input.LastIndexOf('_');
                string basicPortion;
                string deltaPortion;
                if (lastUnderscore >= 0)
                {
                    basicPortion = input.Substring(0, lastUnderscore);
                    deltaPortion = input.Substring(lastUnderscore + 1);
                }
                else
                {
                    basicPortion = "";
                    deltaPortion = input;
                }

                if (deltaPortion.Length == 0 && basicPortion.Length > 0)
                {
                    return basicPortion;
                }

                var output = new List<int>(basicPortion.Length + deltaPortion.Length);
                foreach (char c in basicPortion)
                {
                    output.Add(c);
                }

                int n = InitialN;
                int bias = InitialBias;
                long i = 0;
                int pos = 0;

                while (pos < deltaPortion.Length)
                {
                    long oldI = i;
                    long w = 1;

                    for (int k = Base; ; k += Base)
                    {
                        if (pos >= deltaPortion.Length)
                        {
                            return input; // Decoding failure — return raw.
                        }

                        char c = deltaPortion[pos++];
                        int digit;
                        if (c >= 'a' && c <= 'z')
                        {
                            digit = c - 'a';
                        }
                        else if (c >= 'A' && c <= 'Z')
                        {
                            digit = c - 'A';
                        }
                        else if (c >= '0' && c <= '9')
                        {
                            digit = 26 + (c - '0');
                        }
                        else
                        {
                            return input; // Invalid digit — return raw.
                        }

                        try
                        {
                            checked
                            {
                                i += digit * w;
                            }
                        }
                        catch (OverflowException)
                        {
                            return input; // Overflow — return raw.
                        }

                        int t = k <= bias ? 1
                              : k >= bias + TMax ? TMax
                              : k - bias;
                        if (digit < t)
                        {
                            break;
                        }

                        try
                        {
                            checked
                            {
                                w *= Base - t;
                            }
                        }
                        catch (OverflowException)
                        {
                            return input; // Overflow — return raw.
                        }
                    }

                    // Sanity check: i must be a valid insertion index scaled by code point range.
                    if (i > (long)(output.Count + 1) * (0x10FFFF + 1))
                    {
                        return input; // Overflow — return raw.
                    }

                    int count = output.Count + 1;
                    bias = AdaptBias(i - oldI, count, oldI == 0);
                    n += (int)(i / count);
                    i %= count;

                    if (n < 0 || n > 0x10FFFF)
                    {
                        return input; // Invalid code point — return raw.
                    }

                    output.Insert((int)i, n);
                    i++;
                }

                var sb = new StringBuilder(output.Count);
                foreach (int cp in output)
                {
                    if (cp <= 0xFFFF)
                    {
                        sb.Append((char)cp);
                    }
                    else
                    {
                        sb.Append(char.ConvertFromUtf32(cp));
                    }
                }

                return sb.ToString();
            }

            private static int AdaptBias(long delta, int numPoints, bool firstTime)
            {
                delta = firstTime ? delta / 700 : delta / 2;
                delta += delta / numPoints;

                const long baseMinusTMin = 35; // Base - TMin
                int k = 0;
                while (delta > baseMinusTMin * 26 / 2) // (Base - TMin) * TMax / 2
                {
                    delta /= baseMinusTMin;
                    k += 36;
                }

                return k + (int)((baseMinusTMin + 1) * delta / (delta + 38)); // Skew = 38
            }

            #endregion
        }
    }
}
