// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Parses symbol names and compiler-generated state-machine names used by async call stacks.
    /// Does not classify runtime helpers or verify captured module ownership.
    /// </summary>
    internal static class AsyncProfilerMethodNames
    {
        /// <summary>
        /// Extracts the method name without module/type qualification or parameters.
        /// Dots inside generic arguments are not separators. Null/empty input is returned unchanged.
        /// </summary>
        /// <example><c>"Module!Namespace.Type.Method()"</c> =&gt; <c>"Method"</c>.</example>
        public static string GetBareMethodName(string frameName)
        {
            if (frameName is null || frameName.Length == 0)
            {
                return frameName;
            }

            int end = GetMethodNameEnd(frameName);
            if (end <= 0)
            {
                return string.Empty;
            }

            int start = FindNameSeparator(frameName, end, includeNestedTypes: false) + 1;
            return frameName.Substring(start, end - start);
        }

        /// <summary>
        /// Extracts the declaring type's simple name, retaining generic arguments.
        /// Returns null if the method has no declaring-type qualification.
        /// </summary>
        /// <example><c>"Module!Namespace.Outer+Inner.Method()"</c> =&gt; <c>"Inner"</c>.</example>
        public static string GetDeclaringTypeName(string frameName)
        {
            if (frameName is null || frameName.Length == 0)
            {
                return null;
            }

            int end = GetMethodNameEnd(frameName);
            if (end <= 0)
            {
                return null;
            }

            int methodDot = FindNameSeparator(frameName, end, includeNestedTypes: false);
            if (methodDot <= 0 || frameName[methodDot] != '.')
            {
                return null;
            }

            // In Type..ctor, the constructor's leading dot is part of the member name.
            if (frameName[methodDot - 1] == '.')
            {
                string method = frameName.Substring(methodDot + 1, end - methodDot - 1);
                if (method == "ctor" || method == "cctor")
                {
                    methodDot--;
                }
            }

            int typeEnd = methodDot;
            int typeStart = FindNameSeparator(frameName, typeEnd, includeNestedTypes: true) + 1;
            return typeStart < typeEnd ? frameName.Substring(typeStart, typeEnd - typeStart) : null;
        }

        /// <summary>
        /// Converts a compiler-generated V1 state-machine method name such as
        /// <c>Namespace.Type+&lt;FooAsync&gt;d__5.MoveNext</c> to <c>Namespace.Type.FooAsync</c>.
        /// </summary>
        public static bool TryGetLogicalStateMachineMethodName(string methodName, out string logicalName)
        {
            return TryGetLogicalStateMachineMethodName(methodName, out logicalName, out _);
        }

        /// <summary>
        /// Decodes a generated V1 MoveNext method and returns its source name and generated type suffix.
        /// The suffix retains generic arguments; lambdas and local functions can have a bare <c>d</c> suffix.
        /// </summary>
        public static bool TryGetLogicalStateMachineMethodName(string methodName, out string logicalName, out string stateMachineSuffix)
        {
            logicalName = null;
            stateMachineSuffix = null;
            if (GetBareMethodName(methodName) != "MoveNext")
            {
                return false;
            }

            string stateMachineType = GetDeclaringTypeName(methodName);
            int suffixStart = GetStateMachineSuffixStart(stateMachineType);
            if (suffixStart < 0)
            {
                return false;
            }

            int end = GetMethodNameEnd(methodName);
            int methodDot = FindNameSeparator(methodName, end, includeNestedTypes: false);
            int typeStart = methodDot - stateMachineType.Length;
            if (typeStart <= 0 || methodName[typeStart - 1] != '+')
            {
                return false;
            }

            string declaringType = methodName.Substring(0, typeStart - 1).Replace('+', '.');
            string sourceMethod = stateMachineType.Substring(1, suffixStart - 2);
            logicalName = declaringType + "." + sourceMethod;
            stateMachineSuffix = stateMachineType.Substring(suffixStart);

            return true;
        }

        /// <summary>Recognizes generated V1 MoveNext methods, including lambdas and local functions.</summary>
        public static bool IsStateMachineMoveNext(string frameName, string method)
        {
            return method == "MoveNext" && GetStateMachineSuffixStart(GetDeclaringTypeName(frameName)) >= 0;
        }

        /// <summary>Extracts a MoveNext method's full declaring type without a module prefix.</summary>
        public static string GetStateMachineTypeName(string methodName)
        {
            if (GetBareMethodName(methodName) != "MoveNext")
            {
                return null;
            }

            int end = GetMethodNameEnd(methodName);
            int methodDot = FindNameSeparator(methodName, end, includeNestedTypes: false);
            if (methodDot <= 0 || methodName[methodDot] != '.')
            {
                return null;
            }

            int moduleEnd = methodName.IndexOf('!', 0, methodDot);
            int arguments = methodName.IndexOf('[', 0, methodDot);
            int typeStart = moduleEnd >= 0 && (arguments < 0 || moduleEnd < arguments) ? moduleEnd + 1 : 0;
            return methodDot > typeStart ? methodName.Substring(typeStart, methodDot - typeStart) : null;
        }

        /// <summary>
        /// Formats a logical V1 name with its generated ordinal, when present, and optional presentation state.
        /// </summary>
        public static string FormatLogicalStateMachineMethodName(string logicalName, string stateMachineSuffix, int? state = null)
        {
            string ordinal =
                stateMachineSuffix.StartsWith("d__", StringComparison.Ordinal)
                ? stateMachineSuffix.Substring(3)
                : (stateMachineSuffix.StartsWith("d_", StringComparison.Ordinal) ? stateMachineSuffix.Substring(2) : null);
            if (ordinal == null)
            {
                // Lambda/local-function ordinals are already in the source name. Keep any generic arguments.
                logicalName += stateMachineSuffix.Substring(1);
                return state.HasValue ? logicalName + " [state " + state.Value.ToString(CultureInfo.InvariantCulture) + "]" : logicalName;
            }

            return state.HasValue
                ? logicalName + " [ordinal " + ordinal + ", state " + state.Value.ToString(CultureInfo.InvariantCulture) + "]"
                : logicalName + " [ordinal " + ordinal + "]";
        }

        /// <summary>
        /// Normalizes return-type prefixes and type/member separators for matching.
        /// Stored symbols and the spelling of generic arguments and parameter types remain unchanged.
        /// </summary>
        public static string NormalizeMethodName(string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                return methodName;
            }

            int nameEnd = GetMethodNameEnd(methodName);
            int separator = nameEnd > 0 ? methodName.LastIndexOf("::", nameEnd - 1, nameEnd, StringComparison.Ordinal) : -1;
            int typeEnd = separator >= 0 ? separator : nameEnd;
            int moduleEnd = 0;
            int typeStart = 0;
            int bracketDepth = 0;
            for (int i = 0; i < typeEnd; i++)
            {
                char c = methodName[i];
                if (c == '[')
                {
                    bracketDepth++;
                }
                else if (c == ']')
                {
                    if (bracketDepth == 0)
                    {
                        return methodName;
                    }
                    bracketDepth--;
                }
                else if (bracketDepth == 0)
                {
                    // Keep module qualifiers, but not generic parameter tokens such as !0 or !!0.
                    if (c == '!' && i > 0 && methodName[i - 1] != '!' && !char.IsWhiteSpace(methodName[i - 1]))
                    {
                        moduleEnd = typeStart = i + 1;
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        typeStart = i + 1;
                    }
                }
            }

            if (bracketDepth != 0)
            {
                return methodName;
            }

            if (typeStart > moduleEnd)
            {
                methodName = methodName.Substring(0, moduleEnd) + methodName.Substring(typeStart);
                separator -= typeStart - moduleEnd;
            }

            return separator >= 0 ? methodName.Substring(0, separator) + "." + methodName.Substring(separator + 2) : methodName;
        }

        /// <summary>Removes the parameter list without changing module or type qualification.</summary>
        public static string GetMethodNameWithoutParameters(string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                return methodName;
            }

            int end = GetMethodNameEnd(methodName);
            return end == methodName.Length ? methodName : methodName.Substring(0, end);
        }

        /// <summary>Returns the type-name prefix before its generic-arity marker, if present.</summary>
        public static string GetTypeNameWithoutArity(string typeName)
        {
            int arity = typeName?.IndexOf('`') ?? -1;
            return arity >= 0 ? typeName.Substring(0, arity) : typeName;
        }

        /// <summary>Checks a symbol's module prefix; this does not verify captured module ownership.</summary>
        public static bool IsModuleQualifiedFrame(string frameName, string moduleName)
        {
            return frameName != null && frameName.Length > moduleName.Length &&
                frameName[moduleName.Length] == '!' && frameName.StartsWith(moduleName, StringComparison.Ordinal);
        }

        /// <summary>Extracts the final generic argument, preserving commas inside nested type arguments.</summary>
        public static bool TryGetLastTypeArgument(string typeName, out string typeArgument)
        {
            typeArgument = null;
            int argumentsStart = typeName?.IndexOf('[') ?? -1;
            if (argumentsStart < 0 || typeName[typeName.Length - 1] != ']')
            {
                return false;
            }

            int argumentStart = argumentsStart + 1;
            int depth = 0;
            for (int i = argumentStart; i < typeName.Length - 1; i++)
            {
                char c = typeName[i];
                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    if (--depth < 0)
                    {
                        return false;
                    }
                }
                else if (c == ',' && depth == 0)
                {
                    if (i == argumentStart)
                    {
                        return false;
                    }
                    argumentStart = i + 1;
                }
            }

            if (depth != 0 || argumentStart >= typeName.Length - 1)
            {
                return false;
            }

            typeArgument = typeName.Substring(argumentStart, typeName.Length - 1 - argumentStart).Trim();
            return typeArgument.Length != 0;
        }

        /// <summary>
        /// Extracts the simple implementing type after a known namespace prefix.
        /// Unlike the final method separator, the first member separator also handles explicit-interface members.
        /// </summary>
        public static string GetImplementingTypeName(string frameName, int typeStart)
        {
            int start = typeStart;
            int depth = 0;
            for (int i = start; i < frameName.Length; i++)
            {
                char c = frameName[i];
                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    if (--depth < 0)
                    {
                        return null;
                    }
                }
                else if (depth == 0 && c == '+')
                {
                    start = i + 1;
                }
                else if (depth == 0 && c == '.')
                {
                    return frameName.Substring(start, i - start);
                }
                else if (depth == 0 && c == '(')
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Matches an exact type and member, allowing generic arguments and an optional nested type.
        /// Names may omit the module prefix; other modules do not match.
        /// </summary>
        public static bool IsMethodOnType(string frameName, string fullTypeName, string method, string moduleName, string nestedTypeName = null)
        {
            if (GetBareMethodName(frameName) !=
                (method == ".ctor" ? "ctor" : method == ".cctor" ? "cctor" : method))
            {
                return false;
            }

            int typeStart = IsModuleQualifiedFrame(frameName, moduleName) ? moduleName.Length + 1 : 0;
            if (frameName.Length - typeStart <= fullTypeName.Length || string.CompareOrdinal(frameName, typeStart, fullTypeName, 0, fullTypeName.Length) != 0)
            {
                return false;
            }

            int typeEnd = typeStart + fullTypeName.Length;
            if (fullTypeName.IndexOf('`') >= 0 && frameName[typeEnd] == '[')
            {
                typeEnd = SkipTypeArguments(frameName, typeEnd);
                if (typeEnd < 0)
                {
                    return false;
                }
            }

            if (nestedTypeName != null)
            {
                bool hasNestedTypeRoom = frameName.Length - typeEnd > nestedTypeName.Length + 1;
                bool hasNestedType =
                    hasNestedTypeRoom &&
                    frameName[typeEnd] == '+' &&
                    string.CompareOrdinal(frameName, typeEnd + 1, nestedTypeName, 0, nestedTypeName.Length) == 0;
                if (!hasNestedType)
                {
                    return false;
                }

                typeEnd += nestedTypeName.Length + 1;
            }

            return typeEnd < frameName.Length && frameName[typeEnd] == '.' &&
                string.CompareOrdinal(frameName, typeEnd + 1, method, 0, method.Length) == 0 &&
                (typeEnd + 1 + method.Length == frameName.Length || frameName[typeEnd + 1 + method.Length] == '(');
        }

        /// <summary>Matches a type or one of its nested types, ignoring any module prefix and parameters.</summary>
        public static bool IsMethodOnTypeOrNestedType(string frameName, string typeName)
        {
            if (string.IsNullOrEmpty(frameName))
            {
                return false;
            }

            int start = frameName.IndexOf('!') + 1;
            int typeEnd = start + typeName.Length;
            if (frameName.Length <= typeEnd || string.CompareOrdinal(frameName, start, typeName, 0, typeName.Length) != 0)
            {
                return false;
            }

            int end = GetMethodNameEnd(frameName);
            int member = frameName.LastIndexOf('.', end - 1, end);
            bool isNestedTypeMember = frameName[typeEnd] == '+' && member > typeEnd + 1;
            bool isDirectTypeMember = frameName[typeEnd] == '.' && member == typeEnd;
            return isNestedTypeMember || isDirectTypeMember;
        }

        #region private

        private static int FindNameSeparator(string name, int end, bool includeNestedTypes)
        {
            int bracketDepth = 0;
            for (int i = end - 1; i >= 0; i--)
            {
                char c = name[i];
                if (c == ']')
                {
                    bracketDepth++;
                }
                else if (c == '[')
                {
                    if (bracketDepth > 0)
                    {
                        bracketDepth--;
                    }
                }
                else if (bracketDepth == 0 && (c == '.' || c == '!' || (includeNestedTypes && c == '+')))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int GetStateMachineSuffixStart(string typeName)
        {
            if (string.IsNullOrEmpty(typeName) || typeName[0] != '<')
            {
                return -1;
            }

            // Generic arguments can contain other state-machine names; only inspect this type's suffix.
            int arguments = typeName.IndexOf('[');
            int end = arguments >= 0 ? arguments : typeName.Length;
            int arity = typeName.IndexOf('`', 0, end);
            if (arity >= 0)
            {
                end = arity;
            }

            int close = typeName.LastIndexOf('>', end - 1, end);
            if (close < 2 || close + 1 >= end || typeName[close + 1] != 'd')
            {
                return -1;
            }

            int suffixStart = close + 1;
            int ordinalStart = suffixStart + 1;
            if (ordinalStart == end)
            {
                return suffixStart;
            }
            if (typeName[ordinalStart] != '_')
            {
                return -1;
            }

            ordinalStart++;
            if (ordinalStart < end && typeName[ordinalStart] == '_')
            {
                ordinalStart++;
            }

            int index = ordinalStart;
            while (index < end && typeName[index] >= '0' && typeName[index] <= '9')
            {
                index++;
            }

            return index == end && index > ordinalStart ? suffixStart : -1;
        }

        private static int GetMethodNameEnd(string methodName)
        {
            int parameters = methodName.IndexOf('(');
            return parameters >= 0 ? parameters : methodName.Length;
        }

        private static int SkipTypeArguments(string name, int start)
        {
            int depth = 0;
            for (int i = start; i < name.Length; i++)
            {
                if (name[i] == '[')
                {
                    depth++;
                }
                else if (name[i] == ']' && --depth == 0)
                {
                    return i + 1;
                }
            }

            return -1;
        }

        #endregion
    }
}
