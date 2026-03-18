using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Symbols
{
    /// <summary>
    /// Demangles C++ symbol names that use the Itanium ABI mangling convention.
    /// Handles symbols that start with the "_Z" prefix.
    /// Reference: https://itanium-cxx-abi.github.io/cxx-abi/abi.html#mangling
    /// </summary>
    internal sealed class ItaniumDemangler
    {
        private readonly Parser _parser;

        /// <summary>
        /// Creates a new ItaniumDemangler instance. The instance can be reused
        /// to demangle multiple symbols without allocating a new parser each time.
        /// </summary>
        public ItaniumDemangler()
        {
            _parser = new Parser();
        }

        /// <summary>
        /// Attempts to demangle an Itanium C++ ABI mangled name.
        /// Returns the demangled name on success, or null if the name is not a valid mangled name
        /// or demangling fails.
        /// </summary>
        public string Demangle(string mangledName)
        {
            if (mangledName == null || mangledName.Length < 3 || mangledName[0] != '_' || mangledName[1] != 'Z')
            {
                return null;
            }

            // Compute the effective end offset after stripping ELF versioning and GCC/LLVM
            // linker suffixes. This avoids allocating a Substring.
            int endOffset = ComputeLinkerAnnotationEnd(mangledName);
            if (endOffset < 3)
            {
                return null;
            }

            _parser.Reset(mangledName, 2, endOffset); // skip "_Z"

            try
            {
                string result = _parser.ParseEncoding();

                // Reject the result if there is unconsumed trailing input.
                if (result != null && !_parser.IsAtEnd)
                {
                    return null;
                }

                return result;
            }
            catch
            {
                return null; // Demangling failed; caller will use the mangled name as-is.
            }
        }

        /// <summary>
        /// Computes the effective end offset of a mangled name after stripping ELF
        /// symbol-versioning suffixes (e.g. "@GLIBCXX_3.4") and GCC/LLVM
        /// compiler-generated suffixes (e.g. ".cold", ".isra.0", ".lto_priv.1",
        /// ".constprop.0", ".localalias", ".part.0").
        /// Returns the end offset (exclusive) rather than allocating a substring.
        /// </summary>
        private static int ComputeLinkerAnnotationEnd(string mangledName)
        {
            // Strip ELF symbol versioning: "@VERSION" or "@@VERSION" at the end.
            // Track the effective end rather than creating an intermediate substring.
            int end = mangledName.Length;
            int atIndex = mangledName.IndexOf('@');
            if (atIndex > 0)
                end = atIndex;

            // Strip trailing ELF build-ID hashes. Some toolchains append a hex hash
            // (SHA-1 = 40 hex chars, or MD5 = 32 hex chars) directly to the mangled name
            // without a delimiter. We strip this BEFORE GCC suffixes because the hash is
            // appended after any linker suffixes (e.g., ".cold<hash>").
            // Detect by scanning backward for a contiguous run of lowercase hex digits.
            // If the run is >= 40 chars, strip exactly 40 (SHA-1); else if >= 32, strip 32 (MD5).
            // This handles the common case where the last char of the mangled name is also
            // a hex character (e.g., _Z7_assert<b><40-char-hash> gives hexRun=41).
            if (end > 42) // minimum: _Z + 1 char + 40 hex
            {
                int hexRun = 0;
                for (int i = end - 1; i >= 0; i--)
                {
                    char c = mangledName[i];
                    if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))
                    {
                        hexRun++;
                    }
                    else
                    {
                        break;
                    }
                }
                if (hexRun >= 40)
                {
                    end -= 40;
                }
                else if (hexRun >= 32)
                {
                    end -= 32;
                }
            }

            // Strip GCC/LLVM suffixes by scanning backward.
            // Known patterns (after the dot): cold, isra, lto_priv, constprop, localalias, part.
            // Some have a trailing ".N" numeric component (e.g., ".isra.0", ".lto_priv.1").
            while (end > 2)
            {
                int dot = mangledName.LastIndexOf('.', end - 1);
                if (dot <= 1)
                    break;

                int segStart = dot + 1;
                int segLen = end - segStart;

                // Check if this segment is a bare number — part of a previous suffix.
                // E.g., in ".isra.0", when we encounter "0" we need to look at the name before it.
                if (segLen > 0 && IsAllDigits(mangledName, segStart, segLen))
                {
                    // This is the ".N" trailing part. Look for the suffix name before it.
                    int prevDot = mangledName.LastIndexOf('.', dot - 1);
                    if (prevDot > 1)
                    {
                        // Pass the combined "name.N" region (e.g. "isra.0") to the helper so
                        // it can validate both the name and the numeric suffix together.
                        int combinedStart = prevDot + 1;
                        int combinedLen = end - prevDot - 1;
                        if (IsKnownLinkerSuffix(mangledName, combinedStart, combinedLen))
                        {
                            // Strip both ".name.N"
                            end = prevDot;
                            continue;
                        }
                    }

                    // Bare number without a known prefix — not a linker suffix.
                    break;
                }

                // The segment itself is a known suffix name (no trailing number).
                if (IsKnownLinkerSuffix(mangledName, segStart, segLen))
                {
                    end = dot;
                    continue;
                }

                break;
            }

            return end;
        }

        /// <summary>
        /// Returns true if every character in the substring [start, start+length) is a digit.
        /// </summary>
        private static bool IsAllDigits(string s, int start, int length)
        {
            for (int i = start; i < start + length; i++)
            {
                if (s[i] < '0' || s[i] > '9')
                    return false;
            }
            return length > 0;
        }

        /// <summary>
        /// Returns true if the substring s[start, start+length) equals target (ordinal).
        /// </summary>
        private static bool RegionEquals(string s, int start, int length, string target)
        {
            if (length != target.Length) return false;
            return string.Compare(s, start, target, 0, length, StringComparison.Ordinal) == 0;
        }

        /// <summary>
        /// Returns true if the substring s[start, start+length) is a recognized GCC/LLVM linker
        /// suffix name, either bare (e.g. "cold", "localalias", "isra") or combined with a
        /// trailing numeric component (e.g. "isra.0", "lto_priv.1", "constprop.2", "part.3").
        /// </summary>
        private static bool IsKnownLinkerSuffix(string s, int start, int length)
        {
            // Check bare single-segment suffixes.
            if (RegionEquals(s, start, length, "cold")) return true;
            if (RegionEquals(s, start, length, "localalias")) return true;
            if (RegionEquals(s, start, length, "isra")) return true;
            if (RegionEquals(s, start, length, "lto_priv")) return true;
            if (RegionEquals(s, start, length, "constprop")) return true;
            if (RegionEquals(s, start, length, "part")) return true;

            // Check multi-segment suffixes: name.N (isra, lto_priv, constprop, part).
            int innerDot = -1;
            for (int i = start; i < start + length; i++)
            {
                if (s[i] == '.')
                {
                    innerDot = i;
                    break;
                }
            }

            if (innerDot > start)
            {
                int nameLen = innerDot - start;
                int numStart = innerDot + 1;
                int numLen = (start + length) - numStart;
                if (numLen > 0 && IsAllDigits(s, numStart, numLen))
                {
                    if (RegionEquals(s, start, nameLen, "isra")) return true;
                    if (RegionEquals(s, start, nameLen, "lto_priv")) return true;
                    if (RegionEquals(s, start, nameLen, "constprop")) return true;
                    if (RegionEquals(s, start, nameLen, "part")) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Recursive descent parser for the Itanium C++ ABI mangling grammar.
        /// </summary>
        private sealed class Parser
        {
            private string _input;
            private int _pos;
            private int _endOffset;
            private readonly List<string> _substitutions;
            private List<string> _templateArguments;

            // Snapshot of the function-level template arguments, captured in ParseEncoding after
            // parsing the function name. Template args parsed during TYPE parsing (e.g. inner
            // template specializations in parameter types) must not corrupt this scope.
            private List<string> _functionTemplateArgs;

            // Reusable backing list for _functionTemplateArgs to avoid allocating a copy each call.
            private List<string> _functionTemplateArgsBacking;

            // Tracks whether the function name in the current encoding ended with template args,
            // which means the bare-function-type includes a leading return type that must be skipped.
            private bool _encodingHasTemplateArgs;

            // CV-qualifiers and ref-qualifier on a member function (from nested-name).
            // Appended after the parameter list, e.g. "const", "volatile", " &", " &&".
            private string _functionQualifiers;

            // Guard against deeply nested or crafted symbols that would cause a StackOverflowException.
            private int _depth;
            private const int MaxDepth = 256;

            // Pool of reusable StringBuilders to avoid per-method allocations.
            // Since the parser is recursive, multiple StringBuilders may be in use simultaneously.
            private readonly StringBuilder[] _sbPool = new StringBuilder[8];
            private int _sbPoolIndex;

            internal Parser()
            {
                _substitutions = new List<string>();
                _templateArguments = new List<string>();
                for (int i = 0; i < _sbPool.Length; i++)
                    _sbPool[i] = new StringBuilder(128);
            }

            /// <summary>
            /// Acquires a reusable StringBuilder from the pool. Callers must call
            /// <see cref="ReleaseSb"/> when done. Falls back to a new StringBuilder
            /// if the pool is exhausted (deep recursion).
            /// </summary>
            private StringBuilder AcquireSb()
            {
                if (_sbPoolIndex < _sbPool.Length)
                {
                    var sb = _sbPool[_sbPoolIndex++];
                    sb.Clear();
                    return sb;
                }
                return new StringBuilder(128);
            }

            private void ReleaseSb()
            {
                if (_sbPoolIndex > 0) _sbPoolIndex--;
            }

            public void Reset(string input, int startPos, int endOffset)
            {
                _input = input;
                _pos = startPos;
                _endOffset = endOffset;
                _substitutions.Clear();
                _templateArguments.Clear();
                _functionTemplateArgs = null;
                _functionQualifiers = "";
                _encodingHasTemplateArgs = false;
                _depth = 0;
                _sbPoolIndex = 0;
            }

            #region Helpers

            /// <summary>
            /// Returns true if all input has been consumed.
            /// </summary>
            public bool IsAtEnd => _pos >= _endOffset;

            private bool HasMore => _pos < _endOffset;

            private char PeekOr(char fallback)
            {
                return _pos < _endOffset ? _input[_pos] : fallback;
            }

            private char Consume()
            {
                if (_pos >= _endOffset)
                {
                    throw new InvalidOperationException("Unexpected end of input");
                }

                return _input[_pos++];
            }

            private void Expect(char c)
            {
                if (_pos >= _endOffset || _input[_pos] != c)
                {
                    throw new InvalidOperationException($"Expected '{c}' at position {_pos}");
                }

                _pos++;
            }

            private bool TryConsume(char c)
            {
                if (HasMore && _input[_pos] == c)
                {
                    _pos++;
                    return true;
                }

                return false;
            }

            private bool LookAhead(string s)
            {
                if (_pos + s.Length > _endOffset)
                {
                    return false;
                }

                return string.Compare(_input, _pos, s, 0, s.Length, StringComparison.Ordinal) == 0;
            }

            private bool TryConsume(string s)
            {
                if (LookAhead(s))
                {
                    _pos += s.Length;
                    return true;
                }

                return false;
            }

            private int ParsePositiveNumber()
            {
                if (!HasMore || !char.IsDigit(_input[_pos]))
                {
                    throw new InvalidOperationException("Expected digit");
                }

                long result = 0;
                while (HasMore && char.IsDigit(_input[_pos]))
                {
                    result = result * 10 + (_input[_pos++] - '0');
                    if (result > int.MaxValue)
                    {
                        throw new InvalidOperationException("Number overflow");
                    }
                }

                return (int)result;
            }

            private void AddSubstitution(string entry)
            {
                // Guard against null entries being added to the substitution table.
                if (entry == null)
                {
                    return;
                }

                // The ABI spec says no entity is added more than once, but our parser
                // may encounter the same entity multiple times during complex template
                // expression parsing. Use linear scan for dedup — the substitution list
                // is typically small (5-15 entries) and avoids HashSet overhead.
                if (!_substitutions.Contains(entry))
                {
                    _substitutions.Add(entry);
                }
            }

            /// <summary>
            /// Increments the recursion depth and throws if the limit is exceeded.
            /// Callers must decrement <see cref="_depth"/> when they return (via try/finally).
            /// </summary>
            private void EnterRecursion()
            {
                if (++_depth > MaxDepth)
                {
                    throw new InvalidOperationException("Recursion depth limit exceeded");
                }
            }

            /// <summary>
            /// Wraps a pointer/reference modifier around an inner type, using inside-out
            /// declarator syntax for function types and array types.
            /// For example, a pointer to "void (int)" becomes "void (*)(int)" rather than "void (int)*".
            /// </summary>
            private static string WrapModifier(string inner, string modifier)
            {
                if (string.IsNullOrEmpty(inner))
                {
                    return modifier;
                }

                char lastChar = inner[inner.Length - 1];

                // Function type: ends with ')' where the matching '(' is preceded by a space
                // (from ParseFunctionType which always produces "retType (params)").
                if (lastChar == ')')
                {
                    int paramOpen = FindMatchingOpenParen(inner, inner.Length - 1);
                    if (paramOpen >= 0)
                    {
                        // Bare function type: "retType (params)" — space before the param '('
                        if (paramOpen > 0 && inner[paramOpen - 1] == ' ')
                        {
                            var sb = new StringBuilder(inner.Length + modifier.Length + 4);
                            sb.Append(inner, 0, paramOpen - 1);
                            sb.Append(" (");
                            sb.Append(modifier);
                            sb.Append(')');
                            sb.Append(inner, paramOpen, inner.Length - paramOpen);
                            return sb.ToString();
                        }

                        // Already-wrapped function type: "retType (existingMods)(params)" —
                        // the param '(' is preceded by ')' from an existing modifier group.
                        if (paramOpen > 0 && inner[paramOpen - 1] == ')')
                        {
                            int modGroupClose = paramOpen - 1;
                            var sb = new StringBuilder(inner.Length + modifier.Length);
                            sb.Append(inner, 0, modGroupClose);
                            sb.Append(modifier);
                            sb.Append(inner, modGroupClose, inner.Length - modGroupClose);
                            return sb.ToString();
                        }
                    }
                }

                // Array type: ends with ']'; insert modifier before the first '['.
                if (lastChar == ']')
                {
                    int bracketPos = inner.IndexOf('[');
                    if (bracketPos >= 0)
                    {
                        // Already-wrapped array: "elemType (existingMods)[dim]"
                        if (bracketPos > 0 && inner[bracketPos - 1] == ')')
                        {
                            var sb = new StringBuilder(inner.Length + modifier.Length + 2);
                            sb.Append(inner, 0, bracketPos - 1);
                            sb.Append(modifier);
                            sb.Append(')');
                            sb.Append(inner, bracketPos, inner.Length - bracketPos);
                            return sb.ToString();
                        }

                        // Bare array: "elemType[dim]"
                        {
                            var sb = new StringBuilder(inner.Length + modifier.Length + 4);
                            sb.Append(inner, 0, bracketPos);
                            sb.Append(" (");
                            sb.Append(modifier);
                            sb.Append(')');
                            sb.Append(inner, bracketPos, inner.Length - bracketPos);
                            return sb.ToString();
                        }
                    }
                }

                // Simple case (e.g. int* , int&): just append.
                return string.Concat(inner, modifier);
            }

            /// <summary>
            /// Strips trailing ref-qualifier (" &amp;" / " &amp;&amp;") and exception spec (" noexcept",
            /// " noexcept(...)", " throw(...)") suffixes from a function type string so that
            /// WrapModifier can correctly detect the parameter-list closing ')'.
            /// The stripped suffixes are returned via out parameters for re-appending after wrapping.
            /// </summary>
            private static string StripFunctionTypeSuffixes(string type, out string exceptionSpecSuffix, out string refQualSuffix)
            {
                refQualSuffix = "";
                exceptionSpecSuffix = "";

                if (string.IsNullOrEmpty(type))
                {
                    return type;
                }

                string baseType = type;
                int end = type.Length;

                // Strip trailing ref-qualifier suffix (" &" or " &&").
                if (end >= 3 && type[end - 1] == '&' && type[end - 2] == '&' && type[end - 3] == ' ')
                {
                    refQualSuffix = " &&";
                    end -= 3;
                }
                else if (end >= 2 && type[end - 1] == '&' && type[end - 2] == ' ')
                {
                    refQualSuffix = " &";
                    end -= 2;
                }

                // Strip trailing exception specification (" noexcept", " noexcept(...)", " throw(...)").
                const string noexceptSimple = " noexcept";
                if (end >= noexceptSimple.Length &&
                    string.CompareOrdinal(type, end - noexceptSimple.Length, noexceptSimple, 0, noexceptSimple.Length) == 0)
                {
                    exceptionSpecSuffix = noexceptSimple;
                    end -= noexceptSimple.Length;
                }
                else if (end > 0 && type[end - 1] == ')')
                {
                    // Check for noexcept(...) or throw(...) — both end with ')'
                    int noexceptIdx = type.LastIndexOf(" noexcept(", end - 1, end);
                    int throwIdx = type.LastIndexOf(" throw(", end - 1, end);
                    int specIdx = Math.Max(noexceptIdx, throwIdx);
                    if (specIdx >= 0)
                    {
                        exceptionSpecSuffix = type.Substring(specIdx, end - specIdx);
                        end = specIdx;
                    }
                }

                baseType = end < type.Length ? type.Substring(0, end) : type;

                return baseType;
            }

            /// <summary>
            /// Finds the index of the '(' that matches the ')' at closePos, walking backwards.
            /// Returns -1 if no match is found.
            /// </summary>
            private static int FindMatchingOpenParen(string s, int closePos)
            {
                int depth = 0;
                for (int i = closePos; i >= 0; i--)
                {
                    if (s[i] == ')') depth++;
                    else if (s[i] == '(') depth--;
                    if (depth == 0) return i;
                }
                return -1;
            }

            #endregion

            #region Encoding

            // <encoding> ::= <function name> <bare-function-type>
            //            ::= <data name>
            //            ::= <special-name>
            /// <summary>
            /// Parses an encoding (function name + type, data name, or special name).
            /// </summary>
            public string ParseEncoding()
            {
                if (!HasMore)
                {
                    return null;
                }

                EnterRecursion();
                try
                {
                    // <special-name> starts with T or GV/GR
                    if (PeekOr('\0') == 'T' || LookAhead("GV") || LookAhead("GR"))
                    {
                        return ParseSpecialName();
                    }

                    _encodingHasTemplateArgs = false;
                    _functionQualifiers = "";
                    string name = ParseName();
                    if (name == null)
                    {
                        return null;
                    }

                    // Snapshot the function-level template arguments so that T_ resolution
                    // is not corrupted by template args parsed during parameter type parsing
                    // (e.g. when a parameter type is itself a template specialization).
                    if (_templateArguments != null && _templateArguments.Count > 0)
                    {
                        if (_functionTemplateArgsBacking == null)
                            _functionTemplateArgsBacking = new List<string>(_templateArguments.Count);
                        else
                            _functionTemplateArgsBacking.Clear();
                        _functionTemplateArgsBacking.AddRange(_templateArguments);
                        _functionTemplateArgs = _functionTemplateArgsBacking;
                    }
                    else
                    {
                        _functionTemplateArgs = null;
                    }

                    // Snapshot the function qualifiers so they are not corrupted by nested-name
                    // parsing during parameter types (e.g. a parameter of type Baz::quux triggers
                    // ParseNestedName which resets _functionQualifiers).
                    string functionQualifiers = _functionQualifiers;

                    // If nothing follows the name, it's a data name (variable).
                    if (!HasMore || !CanStartType())
                    {
                        return name;
                    }

                    // Parse bare-function-type.
                    // Template function specializations encode the return type first; skip it.
                    if (_encodingHasTemplateArgs)
                    {
                        ParseType(); // return type — discard
                    }

                    var sb = AcquireSb();
                    sb.Append(name);
                    sb.Append('(');
                    int paramCount = 0;
                    string firstParam = null;
                    while (HasMore && CanStartType())
                    {
                        string type = ParseType();
                        if (type == null)
                        {
                            break;
                        }

                        if (paramCount == 0)
                        {
                            firstParam = type;
                        }
                        else
                        {
                            if (paramCount == 1)
                            {
                                sb.Append(firstParam);
                            }
                            sb.Append(", ");
                            sb.Append(type);
                        }
                        paramCount++;
                    }

                    // A single "void" parameter means empty parameter list.
                    if (paramCount == 1 && firstParam != "void")
                    {
                        sb.Append(firstParam);
                    }
                    sb.Append(')');
                    if (functionQualifiers.Length > 0)
                    {
                        sb.Append(functionQualifiers);
                    }
                    string result = sb.ToString();
                    ReleaseSb();
                    return result;
                }
                finally
                {
                    _depth--;
                }
            }

            /// <summary>
            /// Returns true if the character at the current position can begin a type production.
            /// Used to determine the boundary between name and bare-function-type, or end of parameters.
            /// </summary>
            private bool CanStartType()
            {
                if (!HasMore)
                {
                    return false;
                }

                char c = _input[_pos];

                // 'E' terminates scopes (nested names, template args, etc.) — never a type start.
                if (c == 'E')
                {
                    return false;
                }

                // Digits start source-names (class-enum-type).
                if (char.IsDigit(c))
                {
                    return true;
                }

                switch (c)
                {
                    // Builtin types (single char)
                    case 'v': case 'w': case 'b': case 'c': case 'a': case 'h':
                    case 's': case 't': case 'i': case 'j': case 'l': case 'm':
                    case 'x': case 'y': case 'n': case 'o': case 'f': case 'd':
                    case 'e': case 'g': case 'z':
                    // Type constructors
                    case 'P': // pointer
                    case 'R': // lvalue reference
                    case 'O': // rvalue reference
                    case 'C': // complex (C99)
                    case 'G': // imaginary (C99)
                    case 'K': // const qualifier
                    case 'V': // volatile qualifier
                    case 'r': // restrict qualifier
                    case 'F': // function type
                    case 'A': // array type
                    case 'M': // pointer-to-member
                    case 'N': // nested name (class-enum-type)
                    case 'S': // substitution
                    case 'T': // template param
                    case 'D': // decltype / D-prefixed builtins
                    case 'u': // vendor extended type
                    case 'U': // unnamed/lambda type name (Ut/Ul)
                        return true;
                    default:
                        return false;
                }
            }

            #endregion

            #region Special Name

            // <special-name> ::= TV <type>   # virtual table
            //                ::= TT <type>   # VTT
            //                ::= TI <type>   # typeinfo structure
            //                ::= TS <type>   # typeinfo name
            //                ::= TW <name>   # TLS wrapper function
            //                ::= TH <name>   # TLS init function
            //                ::= GV <name>   # guard variable
            //                ::= T <call-offset> <encoding>  # virtual thunk
            /// <summary>
            /// Parses a special name (vtable, typeinfo, thunks, TLS, guard variables).
            /// </summary>
            private string ParseSpecialName()
            {
                if (TryConsume("TV"))
                {
                    string type = ParseType();
                    return type != null ? string.Concat("vtable for ", type) : null;
                }

                if (TryConsume("TT"))
                {
                    string type = ParseType();
                    return type != null ? string.Concat("VTT for ", type) : null;
                }

                if (TryConsume("TI"))
                {
                    string type = ParseType();
                    return type != null ? string.Concat("typeinfo for ", type) : null;
                }

                if (TryConsume("TS"))
                {
                    string type = ParseType();
                    return type != null ? string.Concat("typeinfo name for ", type) : null;
                }

                if (TryConsume("TW"))
                {
                    string name = ParseName();
                    return name != null ? string.Concat("TLS wrapper function for ", name) : null;
                }

                if (TryConsume("TH"))
                {
                    string name = ParseName();
                    return name != null ? string.Concat("TLS init function for ", name) : null;
                }

                if (TryConsume("Tc"))
                {
                    // Covariant return thunk: Tc <call-offset> <call-offset> <encoding>
                    ParseCallOffset();
                    ParseCallOffset();
                    string encoding = ParseEncoding();
                    return encoding != null ? string.Concat("covariant return thunk to ", encoding) : null;
                }

                // Non-virtual / virtual thunk: T <call-offset> <encoding>
                // Only consume the 'T' prefix; leave h/v for ParseCallOffset.
                if (HasMore && _input[_pos] == 'T')
                {
                    _pos++; // consume 'T'
                    ParseCallOffset();
                    string encoding = ParseEncoding();
                    return encoding != null ? string.Concat("thunk to ", encoding) : null;
                }

                if (TryConsume("GV"))
                {
                    string name = ParseName();
                    return name != null ? string.Concat("guard variable for ", name) : null;
                }

                if (TryConsume("GR"))
                {
                    string name = ParseName();
                    if (name == null)
                    {
                        return null;
                    }

                    // Consume optional [<seq-id>] _ suffix for the reference temporary index.
                    // ABI format: GR <name> [<seq-id>] _
                    // First temp: GR <name> _ (seq-id absent)
                    // Subsequent: GR <name> <base-36 seq-id> _ (seq-id is 0, 1, ..., 9, A, B, ...)
                    // Older ABI revisions omitted the suffix entirely (just GR <name>).
                    while (HasMore && (char.IsDigit(_input[_pos]) || (char.IsLetter(_input[_pos]) && char.IsUpper(_input[_pos]))))
                    {
                        _pos++;
                    }

                    TryConsume('_');

                    return string.Concat("reference temporary for ", name);
                }

                return null;
            }

            // <call-offset> ::= h <nv-offset> _
            //                ::= v <v-offset> _
            /// <summary>
            /// Consumes the call offset without returning it.
            /// </summary>
            private void ParseCallOffset()
            {
                if (TryConsume('h'))
                {
                    // h <nv-offset> _   where nv-offset is a number
                    ParseSignedNumber();
                    Expect('_');
                }
                else if (TryConsume('v'))
                {
                    // v <v-offset> _   where v-offset is <offset number> _ <virtual offset number>
                    ParseSignedNumber();
                    Expect('_');
                    ParseSignedNumber();
                    Expect('_');
                }
                else
                {
                    throw new InvalidOperationException("Expected 'h' or 'v' in call-offset");
                }
            }

            private long ParseSignedNumber()
            {
                bool negative = TryConsume('n');

                if (!HasMore || !char.IsDigit(_input[_pos]))
                {
                    throw new InvalidOperationException("Expected digit in signed number");
                }

                long value = 0;
                while (HasMore && char.IsDigit(_input[_pos]))
                {
                    int digit = _input[_pos++] - '0';
                    if (value > (long.MaxValue - digit) / 10)
                    {
                        throw new InvalidOperationException("Number overflow");
                    }
                    value = value * 10 + digit;
                }

                return negative ? -value : value;
            }

            #endregion

            #region Name

            // <name> ::= <nested-name>
            //        ::= <unscoped-name>
            //        ::= <unscoped-template-name> <template-args>
            //        ::= <local-name>
            /// <summary>
            /// Parses a name (nested, unscoped, template, or local).
            /// </summary>
            private string ParseName()
            {
                if (!HasMore)
                {
                    return null;
                }

                EnterRecursion();
                try
                {
                    char c = _input[_pos];

                    if (c == 'N')
                    {
                        return ParseNestedName();
                    }

                    if (c == 'Z')
                    {
                        return ParseLocalName();
                    }

                    // <unscoped-name> with optional "St" (::std::) prefix
                    string prefix = TryConsume("St") ? "std::" : "";

                    string name = ParseUnqualifiedName(null);
                    if (name == null)
                    {
                        return null;
                    }

                    if (prefix.Length > 0) name = string.Concat(prefix, name);

                    // If template args follow, this is <unscoped-template-name> <template-args>.
                    if (HasMore && _input[_pos] == 'I')
                    {
                        // The template-name is a substitution candidate.
                        AddSubstitution(name);
                        string templateArgs = ParseTemplateArgs();
                        name += templateArgs;
                        // The full template-id is also a substitution candidate.
                        AddSubstitution(name);
                        _encodingHasTemplateArgs = true;
                    }

                    return name;
                }
                finally
                {
                    _depth--;
                }
            }

            // <local-name> ::= Z <encoding> E <name>
            //              ::= Z <encoding> E s [<discriminator>]
            /// <summary>
            /// Parses a local name.
            /// </summary>
            private string ParseLocalName()
            {
                Expect('Z');
                bool savedEncodingHasTemplateArgs = _encodingHasTemplateArgs;
                string savedFunctionQualifiers = _functionQualifiers;
                string encoding = ParseEncoding();
                _encodingHasTemplateArgs = savedEncodingHasTemplateArgs;
                _functionQualifiers = savedFunctionQualifiers;
                if (encoding == null)
                {
                    return null;
                }

                Expect('E');

                if (TryConsume('s'))
                {
                    // String literal — consume optional discriminator
                    ConsumeDiscriminator();
                    return string.Concat(encoding, "::string literal");
                }

                string entity = ParseName();
                if (entity == null)
                {
                    return null;
                }

                // Consume optional discriminator
                ConsumeDiscriminator();

                return string.Concat(encoding, "::", entity);
            }

            /// <summary>
            /// Consumes an optional discriminator suffix.
            /// Single underscore format: _ &lt;single digit&gt; (values 1–9)
            /// Double underscore format: __ &lt;non-negative number&gt; _ (values ≥ 10)
            /// </summary>
            private void ConsumeDiscriminator()
            {
                if (HasMore && _input[_pos] == '_')
                {
                    if (_pos + 1 < _endOffset && _input[_pos + 1] == '_')
                    {
                        // Double underscore format: __ <non-negative number> _
                        _pos += 2; // skip '__'
                        while (HasMore && char.IsDigit(_input[_pos]))
                        {
                            _pos++;
                        }

                        if (HasMore && _input[_pos] == '_')
                        {
                            _pos++; // skip trailing '_'
                        }
                    }
                    else
                    {
                        // Single underscore format: _ <single digit>
                        _pos++; // skip '_'
                        if (HasMore && char.IsDigit(_input[_pos]))
                        {
                            _pos++;
                        }
                    }
                }
            }

            #endregion

            #region Nested Name

            // <nested-name> ::= N [<CV-qualifiers>] [<ref-qualifier>] <prefix>* <unqualified-name> E
            //               ::= N [<CV-qualifiers>] [<ref-qualifier>] <template-prefix> <template-args> E
            /// <summary>
            /// Parses a nested name within N...E delimiters.
            /// </summary>
            private string ParseNestedName()
            {
                Expect('N');

                // Optional CV-qualifiers on the member function.
                int cvFlags = 0;
                while (HasMore)
                {
                    char c = _input[_pos];
                    if (c == 'K') { cvFlags |= 1; _pos++; }
                    else if (c == 'V') { cvFlags |= 2; _pos++; }
                    else if (c == 'r') { cvFlags |= 4; _pos++; }
                    else { break; }
                }
                string cvQuals = CvFlagsToString(cvFlags);

                // Optional ref-qualifier: R = & , O = &&
                string refQual = "";
                if (HasMore && (_input[_pos] == 'R' || _input[_pos] == 'O'))
                {
                    // In nested-name context after N and CV-quals, R/O are ref-qualifiers
                    // only if what follows is a valid prefix start (not end-of-scope).
                    int saved = _pos;
                    char rq = Consume();
                    if (!HasMore || _input[_pos] == 'E')
                    {
                        _pos = saved; // Invalid; restore.
                    }
                    else
                    {
                        refQual = rq == 'R' ? " &" : " &&";
                    }
                }

                _functionQualifiers = refQual.Length == 0 ? cvQuals : string.Concat(cvQuals, refQual);

                string result = ParsePrefixSequence();

                Expect('E');

                return result;
            }

            /// <summary>
            /// Iteratively parses the prefix sequence within a nested name, building up
            /// "Outer::Inner::Name" and recording substitution candidates for each prefix component.
            /// </summary>
            private string ParsePrefixSequence()
            {
                string accumulated = null;
                string lastSimpleName = null;
                bool lastWasTemplateArgs = false;

                while (HasMore && _input[_pos] != 'E')
                {
                    // Template args attach to the current accumulated prefix.
                    if (_input[_pos] == 'I')
                    {
                        string templateArgs = ParseTemplateArgs();
                        if (accumulated != null)
                        {
                            accumulated += templateArgs;
                            AddSubstitution(accumulated);
                        }

                        lastWasTemplateArgs = true;
                        continue;
                    }

                    lastWasTemplateArgs = false;
                    string component;

                    if (_input[_pos] == 'S')
                    {
                        // Substitution or 'St' (::std::) prefix.
                        if (_pos + 1 < _endOffset && _input[_pos + 1] == 't')
                        {
                            _pos += 2; // consume "St"
                            component = "std";
                        }
                        else
                        {
                            component = ParseSubstitution();
                        }
                    }
                    else if (_input[_pos] == 'T')
                    {
                        component = ParseTemplateParam();
                    }
                    else if (_input[_pos] == 'D' && _pos + 1 < _endOffset
                             && (_input[_pos + 1] == 't' || _input[_pos + 1] == 'T'))
                    {
                        component = ParseDecltype();
                    }
                    else if (_input[_pos] == 'C' || (_input[_pos] == 'D' && _pos + 1 < _endOffset && char.IsDigit(_input[_pos + 1])))
                    {
                        // Constructor or destructor — uses the last class name.
                        component = ParseCtorDtorName(lastSimpleName);
                    }
                    else
                    {
                        component = ParseUnqualifiedName(lastSimpleName);
                    }

                    if (component == null)
                    {
                        throw new InvalidOperationException("Failed to parse prefix component");
                    }

                    lastSimpleName = component;
                    accumulated = accumulated == null ? component : string.Concat(accumulated, "::", component);

                    // Every prefix component is a substitution candidate.
                    AddSubstitution(accumulated);
                }

                // Set the template-args flag for the encoding: true only if the nested name
                // ended with template-args (making this a template function/specialization).
                _encodingHasTemplateArgs = lastWasTemplateArgs;

                return accumulated;
            }

            #endregion

            #region Unqualified Name

            // <unqualified-name> ::= <operator-name>
            //                    ::= <ctor-dtor-name>
            //                    ::= <source-name>
            //                    ::= <unnamed-type-name>
            //                    ::= L <source-name> [<discriminator>]  # internal linkage
            /// <summary>
            /// Parses an unqualified name (operator, ctor/dtor, source-name, unnamed type,
            /// or internal-linkage name).
            /// </summary>
            private string ParseUnqualifiedName(string enclosingClass)
            {
                if (!HasMore)
                {
                    return null;
                }

                char c = _input[_pos];

                // L <source-name> [<discriminator>] — name with internal linkage (file-scope static).
                // The 'L' qualifier is stripped in the demangled output; only the source-name is kept.
                if (c == 'L' && _pos + 1 < _endOffset && char.IsDigit(_input[_pos + 1]))
                {
                    _pos++; // consume 'L'
                    string name = ParseSourceName();
                    // Consume ABI tags if present.
                    while (HasMore && _input[_pos] == 'B' && _pos + 1 < _endOffset && char.IsDigit(_input[_pos + 1]))
                    {
                        _pos++;
                        string tag = ParseSourceName();
                        name = string.Concat(name, "[abi:", tag, "]");
                    }

                    ConsumeDiscriminator();
                    return name;
                }

                // <source-name> starts with a digit (length prefix).
                if (char.IsDigit(c))
                {
                    string name = ParseSourceName();
                    // <abi-tag>* ::= B <source-name>  — vendor ABI tags (e.g. B5cxx11 → [abi:cxx11]).
                    // Consume and append any ABI tags that follow.
                    while (HasMore && _input[_pos] == 'B' && _pos + 1 < _endOffset && char.IsDigit(_input[_pos + 1]))
                    {
                        _pos++; // consume 'B'
                        string tag = ParseSourceName();
                        name = string.Concat(name, "[abi:", tag, "]");
                    }

                    return name;
                }

                // <ctor-dtor-name>: C (ctor) or D (dtor) followed by digit.
                if (c == 'C' || (c == 'D' && _pos + 1 < _endOffset && char.IsDigit(_input[_pos + 1])))
                {
                    return ParseCtorDtorName(enclosingClass);
                }

                // <unnamed-type-name> ::= Ut [<nonnegative number>] _
                //                    ::= Ul <lambda-sig> E [<nonnegative number>] _
                if (LookAhead("Ut"))
                {
                    return ParseUnnamedTypeName();
                }

                if (LookAhead("Ul"))
                {
                    return ParseClosureTypeName();
                }

                // <operator-name>
                return ParseOperatorName();
            }

            // <source-name> ::= <positive length number> <identifier>
            /// <summary>
            /// Parses a source name (length-prefixed identifier).
            /// </summary>
            private string ParseSourceName()
            {
                int length = ParsePositiveNumber();
                if (length <= 0 || (long)_pos + length > _endOffset)
                {
                    throw new InvalidOperationException("Invalid source name length");
                }

                string name = _input.Substring(_pos, length);
                _pos += length;
                return name;
            }

            // <ctor-dtor-name> ::= C1 | C2 | C3 | CI1 <base class type> | CI2 <base class type>
            //                  ::= D0 | D1 | D2
            /// <summary>
            /// Parses a constructor or destructor name.
            /// </summary>
            private string ParseCtorDtorName(string enclosingClass)
            {
                if (!HasMore)
                {
                    return null;
                }

                char c = Consume();
                if (c == 'C')
                {
                    if (!HasMore) return null;
                    char kind = Consume();
                    if (kind == 'I')
                    {
                        // CI1/CI2: inheriting constructor — consume variant digit and base class type.
                        if (!HasMore) return null;
                        char iVariant = Consume();
                        if (iVariant != '1' && iVariant != '2') return null;
                        ParseType(); // base class type, discarded
                    }
                    else if (kind != '1' && kind != '2' && kind != '3'
                        && kind != '4' && kind != '5')
                    {
                        return null;
                    }

                    // Constructor name is the same as the enclosing class name.
                    return enclosingClass ?? "constructor";
                }

                if (c == 'D')
                {
                    if (!HasMore) return null;
                    char dVariant = Consume();
                    if (dVariant != '0' && dVariant != '1' && dVariant != '2') return null;
                    return string.Concat("~", enclosingClass ?? "destructor");
                }

                throw new InvalidOperationException("Expected ctor/dtor name");
            }

            // <unnamed-type-name> ::= Ut [<nonnegative number>] _
            /// <summary>
            /// Parses an unnamed type name.
            /// </summary>
            private string ParseUnnamedTypeName()
            {
                _pos += 2; // consume "Ut"
                int discStart = _pos;
                while (HasMore && char.IsDigit(_input[_pos]))
                    _pos++;
                string discriminator = _pos > discStart ? _input.Substring(discStart, _pos - discStart) : "";

                Expect('_');
                return string.Concat("{unnamed type#", discriminator.Length == 0 ? "1" : (int.Parse(discriminator) + 2).ToString(), "}");
            }

            // <closure-type-name> ::= Ul <lambda-sig> E [<nonnegative number>] _
            // <lambda-sig>        ::= <parameter type>+
            /// <summary>
            /// Parses a lambda closure type name.
            /// </summary>
            private string ParseClosureTypeName()
            {
                _pos += 2; // consume "Ul"

                int paramCount = 0;
                string firstParam = null;
                var paramSb = AcquireSb();
                while (HasMore && _input[_pos] != 'E')
                {
                    string type = ParseType();
                    if (type == null)
                    {
                        break;
                    }

                    if (paramCount == 0)
                    {
                        firstParam = type;
                    }
                    else
                    {
                        if (paramCount == 1)
                        {
                            paramSb.Append(firstParam);
                        }
                        paramSb.Append(", ");
                        paramSb.Append(type);
                    }
                    paramCount++;
                }

                Expect('E');

                // Optional discriminator number (identifies which lambda in a scope).
                int discStart = _pos;
                while (HasMore && char.IsDigit(_input[_pos]))
                    _pos++;
                string discriminator = _pos > discStart ? _input.Substring(discStart, _pos - discStart) : "";

                Expect('_');

                int displayIndex = discriminator.Length == 0 ? 1 : checked(int.Parse(discriminator) + 2);
                var result = AcquireSb();
                result.Append("{lambda(");
                if (paramCount >= 2)
                {
                    result.Append(paramSb);
                }
                else if (paramCount == 1 && firstParam != "void")
                {
                    result.Append(firstParam);
                }
                result.Append(")#");
                result.Append(displayIndex);
                result.Append('}');
                string resultStr = result.ToString();
                ReleaseSb();
                ReleaseSb();
                return resultStr;
            }

            #endregion

            #region Operator Name

            /// <summary>
            /// Parses two-character operator name encodings.
            /// </summary>
            private string ParseOperatorName()
            {
                if (_pos + 1 >= _endOffset)
                {
                    return null;
                }

                char c1 = _input[_pos];
                char c2 = _input[_pos + 1];
                string op = LookupOperator(c1, c2);

                if (op != null)
                {
                    _pos += 2;
                    return op;
                }

                // cv <type> — cast operator
                if (c1 == 'c' && c2 == 'v')
                {
                    _pos += 2;
                    string targetType = ParseType();
                    return string.Concat("operator ", targetType);
                }

                // li <source-name> — literal operator (operator "")
                if (c1 == 'l' && c2 == 'i')
                {
                    _pos += 2;
                    string suffix = ParseSourceName();
                    return string.Concat("operator\"\"", suffix);
                }

                return null;
            }

            /// <summary>
            /// Converts CV-qualifier bit flags to a qualifier string.
            /// Bit 0 = const (K), bit 1 = volatile (V), bit 2 = restrict (r).
            /// Output order is const, volatile, restrict — matching the K, V, r parse order used
            /// in ParseNestedName and ParsePointerToMemberType.
            /// </summary>
            private static string CvFlagsToString(int flags)
            {
                switch (flags)
                {
                    case 0: return "";
                    case 1: return " const";
                    case 2: return " volatile";
                    case 3: return " const volatile";
                    case 4: return " restrict";
                    case 5: return " const restrict";
                    case 6: return " volatile restrict";
                    case 7: return " const volatile restrict";
                    default: return "";
                }
            }

            private static string LookupOperator(char c1, char c2)
            {
                // Organized by first character for efficient lookup.
                switch (c1)
                {
                    case 'n':
                        if (c2 == 'w') return "operator new";
                        if (c2 == 'a') return "operator new[]";
                        if (c2 == 'g') return "operator-";
                        if (c2 == 't') return "operator!";
                        if (c2 == 'e') return "operator!=";
                        break;
                    case 'd':
                        if (c2 == 'l') return "operator delete";
                        if (c2 == 'a') return "operator delete[]";
                        if (c2 == 'e') return "operator*";
                        if (c2 == 'v') return "operator/";
                        if (c2 == 'V') return "operator/=";
                        break;
                    case 'p':
                        if (c2 == 's') return "operator+";
                        if (c2 == 'l') return "operator+";
                        if (c2 == 'L') return "operator+=";
                        if (c2 == 'p') return "operator++";
                        if (c2 == 'm') return "operator->*";
                        if (c2 == 't') return "operator->";
                        break;
                    case 'a':
                        if (c2 == 'd') return "operator&";
                        if (c2 == 'n') return "operator&";
                        if (c2 == 'N') return "operator&=";
                        if (c2 == 'S') return "operator=";
                        if (c2 == 'a') return "operator&&";
                        break;
                    case 'c':
                        if (c2 == 'o') return "operator~";
                        if (c2 == 'm') return "operator,";
                        if (c2 == 'l') return "operator()";
                        break;
                    case 'm':
                        if (c2 == 'i') return "operator-";
                        if (c2 == 'I') return "operator-=";
                        if (c2 == 'l') return "operator*";
                        if (c2 == 'L') return "operator*=";
                        if (c2 == 'm') return "operator--";
                        break;
                    case 'r':
                        if (c2 == 'm') return "operator%";
                        if (c2 == 'M') return "operator%=";
                        if (c2 == 's') return "operator>>";
                        if (c2 == 'S') return "operator>>=";
                        break;
                    case 'o':
                        if (c2 == 'r') return "operator|";
                        if (c2 == 'R') return "operator|=";
                        if (c2 == 'o') return "operator||";
                        break;
                    case 'e':
                        if (c2 == 'o') return "operator^";
                        if (c2 == 'O') return "operator^=";
                        if (c2 == 'q') return "operator==";
                        break;
                    case 'l':
                        if (c2 == 's') return "operator<<";
                        if (c2 == 'S') return "operator<<=";
                        if (c2 == 't') return "operator<";
                        if (c2 == 'e') return "operator<=";
                        break;
                    case 'g':
                        if (c2 == 't') return "operator>";
                        if (c2 == 'e') return "operator>=";
                        break;
                    case 's':
                        if (c2 == 's') return "operator<=>";
                        break;
                    case 'i':
                        if (c2 == 'x') return "operator[]";
                        break;
                    case 'q':
                        if (c2 == 'u') return "operator?";
                        break;
                }

                return null;
            }

            #endregion

            #region Type

            /// <summary>
            /// Parses a type production. Adds non-builtin results to the substitution table.
            /// </summary>
            private string ParseType()
            {
                if (!HasMore)
                {
                    return null;
                }

                EnterRecursion();
                try
                {
                    char c = _input[_pos];
                    string result;

                // --- CV-qualified type ---
                if (c == 'K' || c == 'V' || c == 'r')
                {
                    result = ParseCVQualifiedType();
                    if (result != null)
                    {
                        AddSubstitution(result);
                    }
                    return result;
                }

                // --- Pointer ---
                if (c == 'P')
                {
                    _pos++;
                    string inner = ParseType();
                    string baseType = StripFunctionTypeSuffixes(inner, out string exSpec, out string refQual);
                    result = WrapModifier(baseType, "*");
                    if (exSpec.Length > 0 || refQual.Length > 0) result = string.Concat(result, exSpec, refQual);
                    AddSubstitution(result);
                    return result;
                }

                // --- Lvalue reference ---
                if (c == 'R')
                {
                    _pos++;
                    string inner = ParseType();
                    string baseType = StripFunctionTypeSuffixes(inner, out string exSpec, out string refQual);
                    result = WrapModifier(baseType, "&");
                    if (exSpec.Length > 0 || refQual.Length > 0) result = string.Concat(result, exSpec, refQual);
                    AddSubstitution(result);
                    return result;
                }

                // --- Rvalue reference ---
                if (c == 'O')
                {
                    _pos++;
                    string inner = ParseType();
                    string baseType = StripFunctionTypeSuffixes(inner, out string exSpec, out string refQual);
                    result = WrapModifier(baseType, "&&");
                    if (exSpec.Length > 0 || refQual.Length > 0) result = string.Concat(result, exSpec, refQual);
                    AddSubstitution(result);
                    return result;
                }

                // --- Complex (C99) ---
                if (c == 'C')
                {
                    _pos++;
                    string inner = ParseType();
                    result = string.Concat(inner, " _Complex");
                    AddSubstitution(result);
                    return result;
                }

                // --- Imaginary (C99) ---
                if (c == 'G')
                {
                    _pos++;
                    string inner = ParseType();
                    result = string.Concat(inner, " _Imaginary");
                    AddSubstitution(result);
                    return result;
                }

                // --- Function type ---
                if (c == 'F')
                {
                    result = ParseFunctionType();
                    AddSubstitution(result);
                    return result;
                }

                // --- Array type ---
                if (c == 'A')
                {
                    result = ParseArrayType();
                    AddSubstitution(result);
                    return result;
                }

                // --- Pointer-to-member ---
                if (c == 'M')
                {
                    result = ParsePointerToMemberType();
                    AddSubstitution(result);
                    return result;
                }

                // --- Template parameter ---
                if (c == 'T')
                {
                    result = ParseTemplateParam();

                    // Template-template-param followed by template-args
                    if (HasMore && _input[_pos] == 'I')
                    {
                        AddSubstitution(result);
                        string templateArgs = ParseTemplateArgs();
                        result += templateArgs;
                    }

                    AddSubstitution(result);
                    return result;
                }

                // --- Substitution ---
                if (c == 'S')
                {
                    // 'St' = "std::" prefix used in a type context (e.g., Std::string).
                    if (_pos + 1 < _endOffset && _input[_pos + 1] == 't')
                    {
                        _pos += 2;
                        string innerName = ParseUnqualifiedName(null);
                        result = string.Concat("std::", innerName);

                        // May be followed by template args.
                        if (HasMore && _input[_pos] == 'I')
                        {
                            AddSubstitution(result);
                            string targs = ParseTemplateArgs();
                            result += targs;
                        }

                        AddSubstitution(result);
                        return result;
                    }

                    result = ParseSubstitution();

                    // Substitution followed by template args: e.g. S0_ I <args> E
                    if (HasMore && _input[_pos] == 'I')
                    {
                        AddSubstitution(result);
                        string targs = ParseTemplateArgs();
                        result += targs;
                        AddSubstitution(result);
                    }

                    // Substitution itself is already in the table; don't re-add.
                    return result;
                }

                // --- Decltype ---
                if (c == 'D' && _pos + 1 < _endOffset
                    && (_input[_pos + 1] == 't' || _input[_pos + 1] == 'T'))
                {
                    result = ParseDecltype();
                    AddSubstitution(result);
                    return result;
                }

                // --- Pack expansion (C++11) ---
                // Dp <type> — expands a parameter pack (T...).
                if (c == 'D' && _pos + 1 < _endOffset && _input[_pos + 1] == 'p')
                {
                    _pos += 2;
                    string inner = ParseType();
                    if (inner == null) return null;
                    result = string.Concat(inner, "...");
                    AddSubstitution(result);
                    return result;
                }

                // --- D-prefixed builtin types and vendor extended type (u) ---
                result = ParseBuiltinType();
                if (result != null)
                {
                    return result; // builtins are NOT substitution candidates
                }

                // --- Vendor extended type ---
                if (c == 'u')
                {
                    _pos++;
                    result = ParseSourceName();
                    AddSubstitution(result);
                    return result;
                }

                // --- Class-enum type (parsed via <name>) ---
                // Handles source-names (digit), nested names (N), local names (Z),
                // and unnamed/lambda type names starting with Ut/Ul.
                if (char.IsDigit(c) || c == 'N' || c == 'Z'
                    || (c == 'U' && _pos + 1 < _endOffset && (_input[_pos + 1] == 't' || _input[_pos + 1] == 'l')))
                {
                    result = ParseName();
                    if (result != null)
                    {
                        AddSubstitution(result);
                        return result;
                    }
                }

                return null;
                }
                finally
                {
                    _depth--;
                }
            }

            /// <summary>
            /// Parses consecutive CV qualifiers applied to the inner type.
            /// Outputs: "inner_type const volatile restrict" (east-const style).
            /// </summary>
            private string ParseCVQualifiedType()
            {
                int savedPos = _pos;
                bool isConst = false, isVolatile = false, isRestrict = false;

                while (HasMore)
                {
                    char c = _input[_pos];
                    if (c == 'K') { isConst = true; _pos++; }
                    else if (c == 'V') { isVolatile = true; _pos++; }
                    else if (c == 'r') { isRestrict = true; _pos++; }
                    else { break; }
                }

                string inner = ParseType();
                if (inner == null)
                {
                    _pos = savedPos;
                    return null;
                }

                var sb = AcquireSb();
                sb.Append(inner);
                if (isRestrict) sb.Append(" restrict");
                if (isVolatile) sb.Append(" volatile");
                if (isConst) sb.Append(" const");

                string result = sb.ToString();
                ReleaseSb();
                return result;
            }

            // <function-type> ::= F [Y] <bare-function-type> E
            /// <summary>
            /// Parses a function type.
            /// </summary>
            private string ParseFunctionType()
            {
                Expect('F');
                TryConsume('Y'); // extern "C" indicator — ignored

                // Return type
                string returnType = ParseType();
                if (returnType == null) return null;

                // Parameter types
                int paramCount = 0;
                string firstParam = null;
                var paramSb = AcquireSb();
                while (HasMore && _input[_pos] != 'E')
                {
                    // Check for ref-qualifier (R or O) immediately before the terminating 'E'.
                    // The ABI grammar is: F [Y] <bare-function-type> [<ref-qualifier>] E
                    // Without this check, R/O would be consumed by ParseType as reference types.
                    if ((_input[_pos] == 'R' || _input[_pos] == 'O')
                        && _pos + 1 < _endOffset && _input[_pos + 1] == 'E')
                    {
                        break;
                    }

                    string paramType = ParseType();
                    if (paramType == null)
                    {
                        break;
                    }

                    if (paramCount == 0)
                    {
                        firstParam = paramType;
                    }
                    else
                    {
                        if (paramCount == 1)
                        {
                            paramSb.Append(firstParam);
                        }
                        paramSb.Append(", ");
                        paramSb.Append(paramType);
                    }
                    paramCount++;
                }

                // Handle exception specifications (Itanium ABI 5.1.5).
                // Do = noexcept, DO <expression> E = noexcept(expr), Dw <type>+ E = throw(types).
                string exceptionSpec = "";
                if (_pos + 1 < _endOffset && _input[_pos] == 'D')
                {
                    char next = _input[_pos + 1];
                    if (next == 'o')
                    {
                        // Do = noexcept
                        _pos += 2;
                        exceptionSpec = " noexcept";
                    }
                    else if (next == 'O')
                    {
                        // DO <expression> E = noexcept(expr)
                        _pos += 2;
                        string expr = ParseExpression();
                        if (!TryConsume('E'))
                        {
                            ReleaseSb();
                            return null;
                        }

                        exceptionSpec = string.Concat(" noexcept(", expr ?? "", ")");
                    }
                    else if (next == 'w')
                    {
                        // Dw <type>+ E = throw(types)
                        _pos += 2;
                        bool firstThrow = true;
                        var throwSb = AcquireSb();
                        while (HasMore && _input[_pos] != 'E')
                        {
                            string throwType = ParseType();
                            if (throwType == null)
                            {
                                ReleaseSb();
                                ReleaseSb();
                                return null;
                            }

                            if (!firstThrow)
                            {
                                throwSb.Append(", ");
                            }
                            throwSb.Append(throwType);
                            firstThrow = false;
                        }

                        if (!TryConsume('E'))
                        {
                            ReleaseSb();
                            ReleaseSb();
                            return null;
                        }

                        exceptionSpec = string.Concat(" throw(", throwSb.ToString(), ")");
                        ReleaseSb();
                    }
                }

                // Consume optional ref-qualifier before 'E'.
                string refQualifier = "";
                if (HasMore && _input[_pos] == 'R' && _pos + 1 < _endOffset && _input[_pos + 1] == 'E')
                {
                    refQualifier = " &";
                    _pos++;
                }
                else if (HasMore && _input[_pos] == 'O' && _pos + 1 < _endOffset && _input[_pos + 1] == 'E')
                {
                    refQualifier = " &&";
                    _pos++;
                }

                Expect('E');

                var resultSb = AcquireSb();
                resultSb.Append(returnType);
                resultSb.Append(" (");
                if (paramCount >= 2)
                {
                    resultSb.Append(paramSb);
                }
                else if (paramCount == 1 && firstParam != "void")
                {
                    resultSb.Append(firstParam);
                }
                resultSb.Append(')');
                resultSb.Append(exceptionSpec);
                resultSb.Append(refQualifier);
                string funcResult = resultSb.ToString();
                ReleaseSb();
                ReleaseSb();
                return funcResult;
            }

            // <array-type> ::= A <positive dimension number> _ <element type>
            //              ::= A [<dimension expression>] _ <element type>
            /// <summary>
            /// Parses an array type.
            /// </summary>
            private string ParseArrayType()
            {
                Expect('A');

                string dimension = "";
                if (HasMore && char.IsDigit(_input[_pos]))
                {
                    dimension = ParsePositiveNumber().ToString();
                }
                else if (HasMore && _input[_pos] != '_')
                {
                    // Dimension is an expression (template param, sizeof, etc.)
                    dimension = ParseExpression();
                    if (dimension == null) return null;
                }

                Expect('_');
                string elementType = ParseType();

                // If the element type is itself an array, insert this dimension before
                // the existing ones so that dimensions appear in parse order (outer first).
                // For example, A10_A5_i should produce int[10][5], not int[5][10].
                // Only enter this path when the element type genuinely IS an array —
                // check that it ends with ']' and the matching '[' is at the outermost
                // level (not inside parentheses, which would indicate a function parameter).
                bool elementIsArray = false;
                int bracketPos = -1;
                if (elementType.Length > 0 && elementType[elementType.Length - 1] == ']')
                {
                    // Walk backwards to verify the trailing ']' closes a bracket at the outermost level.
                    int depth = 0;
                    for (int i = elementType.Length - 1; i >= 0; i--)
                    {
                        char ch = elementType[i];
                        if (ch == ')') depth++;
                        else if (ch == '(') depth--;
                        else if (ch == '>') depth++;
                        else if (ch == '<') depth--;
                        else if (ch == '[' && depth == 0)
                        {
                            elementIsArray = true;
                            break;
                        }
                    }

                    if (elementIsArray)
                    {
                        // Find the first '[' at the outermost level so we insert before all existing dimensions.
                        depth = 0;
                        for (int i = 0; i < elementType.Length; i++)
                        {
                            char ch = elementType[i];
                            if (ch == '(' || ch == '<') depth++;
                            else if (ch == ')' || ch == '>') depth--;
                            else if (ch == '[' && depth == 0)
                            {
                                bracketPos = i;
                                break;
                            }
                        }
                    }
                }

                if (elementIsArray)
                {
                    // If the bracket is preceded by ')' — this is a wrapped type like "int (*)[5]".
                    // Don't merge dimensions; instead insert inside the wrapper so that
                    // "int (*)[5]" with dimension 10 becomes "int (*[10])[5]".
                    if (bracketPos > 0 && elementType[bracketPos - 1] == ')')
                    {
                        int closeParenPos = bracketPos - 1;
                        var sb = AcquireSb();
                        sb.Append(elementType, 0, closeParenPos);
                        sb.Append('[');
                        sb.Append(dimension);
                        sb.Append(']');
                        sb.Append(elementType, closeParenPos, elementType.Length - closeParenPos);
                        string result = sb.ToString();
                        ReleaseSb();
                        return result;
                    }

                    {
                        var sb = AcquireSb();
                        sb.Append(elementType, 0, bracketPos);
                        sb.Append('[');
                        sb.Append(dimension);
                        sb.Append(']');
                        sb.Append(elementType, bracketPos, elementType.Length - bracketPos);
                        string result = sb.ToString();
                        ReleaseSb();
                        return result;
                    }
                }

                return string.Concat(elementType, "[", dimension, "]");
            }

            // <pointer-to-member-type> ::= M <class type> <member type>
            /// <summary>
            /// Parses a pointer-to-member type.
            /// </summary>
            private string ParsePointerToMemberType()
            {
                Expect('M');
                string classType = ParseType();
                if (classType == null) return null;

                // For member function pointers, the member type may have leading CV qualifiers
                // (KFivE = const member function). Intercept them here so that WrapModifier can
                // correctly wrap the bare function type, then reattach the qualifiers after.
                // This produces "int (C::*)() const" instead of "int () const C::*".
                int memberFnFlags = 0;
                int savedPos = _pos;

                while (HasMore && (_input[_pos] == 'K' || _input[_pos] == 'V' || _input[_pos] == 'r'))
                {
                    char c = _input[_pos];
                    if (c == 'K') memberFnFlags |= 1;
                    else if (c == 'V') memberFnFlags |= 2;
                    else memberFnFlags |= 4;
                    _pos++;
                }

                if (!HasMore || _input[_pos] != 'F')
                {
                    // Not a [CV-qualified] function type — restore and let ParseType handle it.
                    _pos = savedPos;
                    memberFnFlags = 0;
                }
                string memberFnQuals = CvFlagsToString(memberFnFlags);

                string memberType = ParseType();
                if (memberType == null) return null;

                string modifier = string.Concat(classType, "::*");

                string baseMemberType = StripFunctionTypeSuffixes(memberType, out string exceptionSpecSuffix, out string refQualSuffix);

                // For function and array member types, WrapModifier uses inside-out syntax:
                // int (C::*)() or int (C::*)[10].
                if (baseMemberType != null && baseMemberType.Length > 0
                    && (baseMemberType[baseMemberType.Length - 1] == ')' || baseMemberType[baseMemberType.Length - 1] == ']'))
                {
                    return string.Concat(WrapModifier(baseMemberType, modifier), memberFnQuals, exceptionSpecSuffix, refQualSuffix);
                }

                // Simple types need a space separator: "int A::*".
                return string.Concat(memberType, " ", modifier);
            }

            #endregion

            #region Builtin Type

            /// <summary>
            /// Parses builtin types. These are NOT added to the substitution table.
            /// </summary>
            private string ParseBuiltinType()
            {
                if (!HasMore)
                {
                    return null;
                }

                // D-prefixed extended builtins
                if (_input[_pos] == 'D' && _pos + 1 < _endOffset)
                {
                    string dResult = TryParseDBuiltin();
                    if (dResult != null)
                    {
                        return dResult;
                    }
                }

                string result;
                switch (_input[_pos])
                {
                    case 'v': result = "void"; break;
                    case 'w': result = "wchar_t"; break;
                    case 'b': result = "bool"; break;
                    case 'c': result = "char"; break;
                    case 'a': result = "signed char"; break;
                    case 'h': result = "unsigned char"; break;
                    case 's': result = "short"; break;
                    case 't': result = "unsigned short"; break;
                    case 'i': result = "int"; break;
                    case 'j': result = "unsigned int"; break;
                    case 'l': result = "long"; break;
                    case 'm': result = "unsigned long"; break;
                    case 'x': result = "long long"; break;
                    case 'y': result = "unsigned long long"; break;
                    case 'n': result = "__int128"; break;
                    case 'o': result = "unsigned __int128"; break;
                    case 'f': result = "float"; break;
                    case 'd': result = "double"; break;
                    case 'e': result = "long double"; break;
                    case 'g': result = "__float128"; break;
                    case 'z': result = "..."; break;
                    default: return null;
                }

                _pos++;
                return result;
            }

            /// <summary>
            /// Tries to parse a D-prefixed builtin type (e.g. Dd, Di, Dn).
            /// Returns null if the D-prefix is not a known builtin.
            /// </summary>
            private string TryParseDBuiltin()
            {
                char d = _input[_pos + 1];
                string result;

                switch (d)
                {
                    case 'd': result = "decimal64"; break;
                    case 'e': result = "decimal128"; break;
                    case 'f': result = "decimal32"; break;
                    case 'h': result = "half"; break;
                    case 'i': result = "char32_t"; break;
                    case 's': result = "char16_t"; break;
                    case 'a': result = "auto"; break;
                    case 'c': result = "decltype(auto)"; break;
                    case 'n': result = "std::nullptr_t"; break;
                    default: return null;
                }

                _pos += 2;
                return result;
            }

            #endregion

            #region Template Args

            // <template-args> ::= I <template-arg>+ E
            /// <summary>
            /// Stores the parsed template arguments for later T_ resolution.
            /// </summary>
            private string ParseTemplateArgs()
            {
                Expect('I');

                EnterRecursion();
                try
                {
                    // Save the outer template arguments so that sibling template args
                    // are parsed with the correct context. Without this, parsing A<int>
                    // as a sibling arg overwrites _templateArguments before a subsequent
                    // T_ sibling is resolved.
                    var savedArgs = _templateArguments;
                    var args = new List<string>(4);

                    while (HasMore && _input[_pos] != 'E')
                    {
                        _templateArguments = savedArgs;
                        string arg = ParseTemplateArg();
                        if (arg == null)
                        {
                            break;
                        }

                        args.Add(arg);
                    }

                    Expect('E');

                    // Store the parsed template args for T_ resolution.
                    // Always update so that T_ resolves against the most recently parsed
                    // (innermost) template arguments, matching the Itanium ABI spec.
                    // For member templates like A<int>::foo<float>, this ensures T_ in
                    // foo's signature resolves to float (from foo's args), not int (from A's args).
                    _templateArguments = args;

                    var sb = AcquireSb();
                    sb.Append('<');
                    for (int i = 0; i < args.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(args[i]);
                    }
                    sb.Append('>');
                    string result = sb.ToString();
                    ReleaseSb();
                    return result;
                }
                finally
                {
                    _depth--;
                }
            }

            // <template-arg> ::= <type>
            //               ::= X <expression> E
            //               ::= <expr-primary>
            //               ::= J <template-arg>* E  (argument pack)
            /// <summary>
            /// Parses a single template argument.
            /// </summary>
            private string ParseTemplateArg()
            {
                if (!HasMore)
                {
                    return null;
                }

                EnterRecursion();
                try
                {
                    // Expression: X <expression> E
                    if (_input[_pos] == 'X')
                    {
                        _pos++;
                        string expr = ParseExpression();
                        Expect('E');
                        return expr;
                    }

                    // Primary expression: L <type> <value> E  or  L <mangled-name> E
                    if (_input[_pos] == 'L')
                    {
                        return ParseExprPrimary();
                    }

                    // Argument pack: J <template-arg>* E
                    if (_input[_pos] == 'J')
                    {
                        _pos++;
                        var packArgs = new List<string>(2);
                        while (HasMore && _input[_pos] != 'E')
                        {
                            string packArg = ParseTemplateArg();
                            if (packArg == null)
                            {
                                break;
                            }

                            packArgs.Add(packArg);
                        }

                        Expect('E');
                        var packSb = AcquireSb();
                        for (int i = 0; i < packArgs.Count; i++)
                        {
                            if (i > 0) packSb.Append(", ");
                            packSb.Append(packArgs[i]);
                        }
                        string packResult = packSb.ToString();
                        ReleaseSb();
                        return packResult;
                    }

                    // Type
                    return ParseType();
                }
                finally
                {
                    _depth--;
                }
            }

            #endregion

            #region Expression (simplified)

            /// <summary>
            /// Simplified expression parser. Full expression parsing is extremely complex;
            /// this handles the most common forms encountered in real binaries.
            /// </summary>
            private string ParseExpression()
            {
                if (!HasMore)
                {
                    return null;
                }

                EnterRecursion();
                try
                {
                    // Primary expression
                    if (_input[_pos] == 'L')
                    {
                        return ParseExprPrimary();
                    }

                    // Template parameter
                    if (_input[_pos] == 'T')
                    {
                        return ParseTemplateParam();
                    }

                    // sizeof(type): st <type>
                    if (LookAhead("st"))
                    {
                        _pos += 2;
                        string type = ParseType();
                        return string.Concat("sizeof(", type, ")");
                    }

                    // sizeof...(pack): sZ <template-param>
                    if (LookAhead("sZ"))
                    {
                        _pos += 2;
                        string param = ParseTemplateParam();
                        return string.Concat("sizeof...(", param, ")");
                    }

                    // <unresolved-name> ::= <simple-id>
                    // <simple-id> ::= <source-name> [<template-args>]
                    // In expression context, a bare source name (digit-prefixed) optionally
                    // followed by template args represents an unresolved name.
                    if (char.IsDigit(_input[_pos]))
                    {
                        string name = ParseSourceName();
                        if (HasMore && _input[_pos] == 'I')
                        {
                            string templateArgs = ParseTemplateArgs();
                            name += templateArgs;
                        }

                        return name;
                    }

                    // cv = type conversion (cast): cv <type> <expression>
                    // or cv <type> _ <expression>* E (list initialization)
                    // Handled before LookupOperator because cv is also a special operator name.
                    if (LookAhead("cv"))
                    {
                        _pos += 2;
                        string type = ParseType();
                        if (type == null)
                        {
                            return null;
                        }

                        if (_pos < _endOffset && _input[_pos] == '_')
                        {
                            // List-initialization form: cv <type> _ <expr>* E
                            _pos++; // consume _
                            var args = new List<string>(4);
                            while (HasMore && _input[_pos] != 'E')
                            {
                                string arg = ParseExpression();
                                if (arg == null)
                                {
                                    return null;
                                }

                                args.Add(arg);
                            }

                            if (!TryConsume('E'))
                            {
                                return null;
                            }

                            var listSb = AcquireSb();
                            listSb.Append('(');
                            listSb.Append(type);
                            listSb.Append("){");
                            for (int i = 0; i < args.Count; i++)
                            {
                                if (i > 0) listSb.Append(", ");
                                listSb.Append(args[i]);
                            }
                            listSb.Append('}');
                            string listResult = listSb.ToString();
                            ReleaseSb();
                            return listResult;
                        }
                        else
                        {
                            string expr = ParseExpression();
                            if (expr == null)
                            {
                                return null;
                            }

                            return string.Concat("(", type, ")(", expr, ")");
                        }
                    }

                    // cl = function call: cl <expression>+ E
                    // The first expression is the callee; remaining expressions are arguments.
                    // Handled before LookupOperator because cl is also in the operator table
                    // (for operator() overload names) and would otherwise be treated as binary.
                    if (LookAhead("cl"))
                    {
                        _pos += 2;

                        // Parse callee (first expression)
                        string callee = ParseExpression();
                        if (callee == null)
                        {
                            return null;
                        }

                        // Parse arguments until E
                        var args = new List<string>(4);
                        while (HasMore && _input[_pos] != 'E')
                        {
                            string arg = ParseExpression();
                            if (arg == null)
                            {
                                return null;
                            }

                            args.Add(arg);
                        }

                        if (!TryConsume('E'))
                        {
                            return null;
                        }

                        var callSb = AcquireSb();
                        callSb.Append(callee);
                        callSb.Append('(');
                        for (int i = 0; i < args.Count; i++)
                        {
                            if (i > 0) callSb.Append(", ");
                            callSb.Append(args[i]);
                        }
                        callSb.Append(')');
                        string callResult = callSb.ToString();
                        ReleaseSb();
                        return callResult;
                    }

                    // C++ named cast operators: sc, rc, dc, cc — all have form: <cast> <type> <expression>
                    if (_pos + 1 < _endOffset)
                    {
                        string castName = null;
                        if (LookAhead("sc")) castName = "static_cast";
                        else if (LookAhead("rc")) castName = "reinterpret_cast";
                        else if (LookAhead("dc")) castName = "dynamic_cast";
                        else if (LookAhead("cc")) castName = "const_cast";

                        if (castName != null)
                        {
                            _pos += 2;
                            string type = ParseType();
                            if (type == null)
                            {
                                return null;
                            }

                            string expr = ParseExpression();
                            if (expr == null)
                            {
                                return null;
                            }

                            return string.Concat(castName, "<", type, ">(", expr, ")");
                        }
                    }

                    // dt: member access (expr.name)
                    if (LookAhead("dt"))
                    {
                        _pos += 2;
                        string expr = ParseExpression();
                        if (expr == null)
                        {
                            return null;
                        }

                        string name = ParseUnqualifiedName(null);
                        return string.Concat(expr, ".", name);
                    }

                    // ds: pointer-to-member dereference (expr.*expr)
                    if (LookAhead("ds"))
                    {
                        _pos += 2;
                        string lhs = ParseExpression();
                        if (lhs == null)
                        {
                            return null;
                        }

                        string rhs = ParseExpression();
                        if (rhs == null)
                        {
                            return null;
                        }

                        return string.Concat(lhs, ".*", rhs);
                    }

                    // sr: scope resolution — <type>::<unqualified-name> [<template-args>]
                    if (LookAhead("sr"))
                    {
                        _pos += 2;
                        string type = ParseType();
                        if (type == null)
                        {
                            return null;
                        }

                        string name = ParseUnqualifiedName(null);
                        if (HasMore && _input[_pos] == 'I')
                        {
                            string templateArgs = ParseTemplateArgs();
                            if (templateArgs != null)
                            {
                                name += templateArgs;
                            }
                        }

                        return string.Concat(type, "::", name);
                    }

                    // Two-character operator expressions: <operator> <operand(s)>
                    if (_pos + 1 < _endOffset)
                    {
                        char c1 = _input[_pos];
                        char c2 = _input[_pos + 1];
                        string op = LookupOperator(c1, c2);
                        if (op != null)
                        {
                            _pos += 2;

                            int arity = GetOperatorArity(c1, c2);
                            string operand1 = ParseExpression();
                            string sym = ExtractOperatorSymbol(op);

                            if (arity == 1)
                            {
                                return string.Concat(sym, "(", operand1, ")");
                            }

                            if (arity == 3)
                            {
                                // Ternary conditional: qu = ? :
                                string operand2 = ParseExpression();
                                string operand3 = ParseExpression();
                                return string.Concat("(", operand1, ") ? (", operand2, ") : (", operand3, ")");
                            }

                            // Binary (default)
                            string rhs = ParseExpression();
                            return string.Concat("(", operand1, ") ", sym, " (", rhs, ")");
                        }
                    }

                    // Fallback: consume characters until 'E' or '_' at the correct nesting level.
                    var sb = AcquireSb();
                    int nestDepth = 0;
                    while (HasMore)
                    {
                        char c = _input[_pos];
                        if (c == 'E' && nestDepth == 0) break;

                        // Template param tokens (T_, T0_, T1_, ...) and substitution tokens
                        // (S_, S0_, SA_, ...) contain a trailing '_' that must not be confused
                        // with the array dimension delimiter. Consume these patterns atomically.
                        if (c == 'T' && nestDepth == 0)
                        {
                            int peekPos = _pos + 1;
                            while (peekPos < _endOffset && char.IsDigit(_input[peekPos]))
                            {
                                peekPos++;
                            }

                            if (peekPos < _endOffset && _input[peekPos] == '_')
                            {
                                while (_pos <= peekPos)
                                {
                                    sb.Append(Consume());
                                }

                                continue;
                            }
                        }

                        if (c == 'S' && nestDepth == 0)
                        {
                            int peekPos = _pos + 1;
                            while (peekPos < _endOffset && char.IsLetterOrDigit(_input[peekPos]))
                            {
                                peekPos++;
                            }

                            if (peekPos < _endOffset && _input[peekPos] == '_')
                            {
                                while (_pos <= peekPos)
                                {
                                    sb.Append(Consume());
                                }

                                continue;
                            }
                        }

                        if (c == '_' && nestDepth == 0) break; // Stop for array dimension delimiter
                        if (c == 'I' || c == 'X' || c == 'N' || c == 'F' || c == 'L' || c == 'Z' || c == 'J') nestDepth++;

                        // Two-character prefixes that open E-terminated scopes:
                        // Ul (lambda sig), DO (noexcept expr), Dw (throw expr).
                        if (_pos + 1 < _endOffset &&
                            ((c == 'U' && _input[_pos + 1] == 'l') ||
                             (c == 'D' && (_input[_pos + 1] == 'O' || _input[_pos + 1] == 'w'))))
                        {
                            nestDepth++;
                        }

                        if (c == 'E' && nestDepth > 0) nestDepth--;
                        sb.Append(Consume());
                    }

                    string fallbackResult = sb.ToString();
                    ReleaseSb();
                    return fallbackResult;
                }
                finally
                {
                    _depth--;
                }
            }

            /// <summary>
            /// Extracts the operator symbol from a full "operator+" style string.
            /// </summary>
            private static string ExtractOperatorSymbol(string operatorName)
            {
                if (operatorName.StartsWith("operator"))
                {
                    return operatorName.Substring(8).TrimStart();
                }

                return operatorName;
            }

            /// <summary>
            /// Returns the arity of an operator given its two-character mangled code.
            /// 1 = unary, 2 = binary, 3 = ternary.
            /// </summary>
            private static int GetOperatorArity(char c1, char c2)
            {
                // Unary operators
                if (c1 == 'p' && c2 == 's') return 1; // operator+ (unary positive)
                if (c1 == 'n' && c2 == 'g') return 1; // operator- (negate)
                if (c1 == 'd' && c2 == 'e') return 1; // operator* (dereference)
                if (c1 == 'a' && c2 == 'd') return 1; // operator& (address-of)
                if (c1 == 'c' && c2 == 'o') return 1; // operator~ (complement)
                if (c1 == 'n' && c2 == 't') return 1; // operator! (logical not)
                if (c1 == 'p' && c2 == 'p') return 1; // operator++ (prefix)
                if (c1 == 'm' && c2 == 'm') return 1; // operator-- (prefix)

                // Ternary operator
                if (c1 == 'q' && c2 == 'u') return 3; // operator? (conditional)

                // Everything else is binary
                return 2;
            }

            // <expr-primary> ::= L <type> <value number> E
            //                ::= L <mangled-name> E
            /// <summary>
            /// Parses a primary expression literal.
            /// </summary>
            private string ParseExprPrimary()
            {
                Expect('L');

                // L _Z <encoding> E — reference to a function or variable
                if (LookAhead("_Z"))
                {
                    _pos += 2; // skip "_Z"
                    // Save all encoding-level state that the inner ParseEncoding will reset.
                    // This mirrors the save/restore in ParseLocalName.
                    bool savedEncodingHasTemplateArgs = _encodingHasTemplateArgs;
                    string savedFunctionQualifiers = _functionQualifiers;
                    var savedFunctionTemplateArgs = _functionTemplateArgs;
                    string name = ParseEncoding();
                    _encodingHasTemplateArgs = savedEncodingHasTemplateArgs;
                    _functionQualifiers = savedFunctionQualifiers;
                    _functionTemplateArgs = savedFunctionTemplateArgs;
                    Expect('E');
                    return name;
                }

                string type = ParseType();
                if (type == null) type = "?";

                // Parse value (everything up to 'E')
                var sb = AcquireSb();
                while (HasMore && _input[_pos] != 'E')
                {
                    sb.Append(Consume());
                }

                Expect('E');

                string value = sb.ToString();
                ReleaseSb();

                // Negative numbers use 'n' prefix in mangled form.
                if (value.Length > 0 && value[0] == 'n')
                {
                    value = string.Concat("-", value.Substring(1));
                }

                if (type == "bool")
                {
                    return value == "0" ? "false" : "true";
                }

                return string.Concat("(", type, ")", value);
            }

            #endregion

            #region Substitution

            // <substitution> ::= S <seq-id> _
            //               ::= S_              (first substitution, index 0)
            //               ::= Sa | Sb | Ss | Si | So | Sd
            /// <summary>
            /// Parses a substitution reference.
            /// </summary>
            private string ParseSubstitution()
            {
                Expect('S');

                if (!HasMore)
                {
                    throw new InvalidOperationException("Unexpected end after 'S'");
                }

                // Built-in substitutions (well-known components)
                switch (_input[_pos])
                {
                    case 'a': _pos++; return "std::allocator";
                    case 'b': _pos++; return "std::basic_string";
                    case 's':
                        _pos++;
                        return "std::basic_string<char, std::char_traits<char>, std::allocator<char>>";
                    case 'i':
                        _pos++;
                        return "std::basic_istream<char, std::char_traits<char>>";
                    case 'o':
                        _pos++;
                        return "std::basic_ostream<char, std::char_traits<char>>";
                    case 'd':
                        _pos++;
                        return "std::basic_iostream<char, std::char_traits<char>>";
                }

                // S_ — first user substitution (index 0)
                if (_input[_pos] == '_')
                {
                    _pos++;
                    if (_substitutions.Count == 0)
                    {
                        return "{S}";
                    }

                    return _substitutions[0];
                }

                // S <base-36 seq-id> _ — user substitution at index (seq-id + 1)
                long seqId = 0;
                while (HasMore && _input[_pos] != '_')
                {
                    char ch = Consume();
                    if (ch >= '0' && ch <= '9')
                    {
                        seqId = seqId * 36 + (ch - '0');
                    }
                    else if (ch >= 'A' && ch <= 'Z')
                    {
                        seqId = seqId * 36 + (ch - 'A' + 10);
                    }
                    else
                    {
                        throw new InvalidOperationException("Invalid base-36 character in substitution: " + ch);
                    }

                    if (seqId >= int.MaxValue)
                    {
                        throw new InvalidOperationException("Substitution index overflow");
                    }
                }

                Expect('_');

                int index = (int)seqId + 1;
                if (index >= _substitutions.Count)
                {
                    // Some deeply nested symbols (e.g., local entities with complex template args)
                    // may reference substitutions that our parser doesn't fully track. Return a
                    // placeholder rather than failing so the rest of the demangling can proceed.
                    return string.Concat("{S", seqId.ToString(), "}");
                }

                return _substitutions[index];
            }

            #endregion

            #region Template Param

            // <template-param> ::= T_        (first template parameter, index 0)
            //                  ::= T <number> _   (parameter at index number+1)
            /// <summary>
            /// Parses a template parameter reference.
            /// </summary>
            private string ParseTemplateParam()
            {
                Expect('T');

                int index;
                if (TryConsume('_'))
                {
                    index = 0;
                }
                else
                {
                    // Parse the non-negative index number.
                    long paramIndex = 0;
                    while (HasMore && _input[_pos] != '_')
                    {
                        char ch = Consume();
                        if (ch >= '0' && ch <= '9')
                        {
                            paramIndex = paramIndex * 10 + (ch - '0');
                        }
                        else
                        {
                            throw new InvalidOperationException("Invalid template param index character");
                        }

                        if (paramIndex >= int.MaxValue)
                        {
                            throw new InvalidOperationException("Template param index overflow");
                        }
                    }

                    Expect('_');
                    index = (int)paramIndex + 1;
                }

                if (_functionTemplateArgs != null && index < _functionTemplateArgs.Count)
                {
                    return _functionTemplateArgs[index];
                }

                // Only fall back to _templateArguments when NOT in function template context.
                // When _functionTemplateArgs is active, _templateArguments may have been mutated
                // by nested template parsing (e.g., a sibling parameter like A<int>).
                if (_functionTemplateArgs == null && index < _templateArguments.Count)
                {
                    return _templateArguments[index];
                }

                // Fallback: return a placeholder for unresolved template parameters.
                return index == 0 ? "T" : string.Concat("T", (index - 1).ToString());
            }

            #endregion

            #region Decltype

            // <decltype> ::= Dt <expression> E
            //            ::= DT <expression> E
            /// <summary>
            /// Parses a decltype expression.
            /// </summary>
            private string ParseDecltype()
            {
                if (TryConsume("Dt") || TryConsume("DT"))
                {
                    string expr = ParseExpression();
                    Expect('E');
                    return string.Concat("decltype(", expr, ")");
                }

                return null;
            }

            #endregion
        }
    }
}
