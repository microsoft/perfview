//     Copyright (c) Microsoft Corporation.  All rights reserved.
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// 
using System;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Diagnostics.Utilities
{
    /// <summary>
    /// The important thing about these general utilities is that they have only dependencies on mscorlib and
    /// System (they can be used from anywhere).  
    /// </summary>
#if UTILITIES_PUBLIC
    public 
#endif
    internal class XmlUtilities
    {
        /// <summary>
        /// Given an XML element, remove the closing operator for it, so you can add new child elements to it by concatination. 
        /// </summary>
        public static string OpenXmlElement(string xmlElement)
        {
            if (xmlElement.EndsWith("/>"))
            {
                return xmlElement.Substring(0, xmlElement.Length - 2) + ">";
            }

            int endTagIndex = xmlElement.LastIndexOf("</");
            Debug.Assert(endTagIndex > 0);
            while (endTagIndex > 0 && Char.IsWhiteSpace(xmlElement[endTagIndex - 1]))
            {
                --endTagIndex;
            }

            return xmlElement.Substring(0, endTagIndex);
        }

        /// <summary>
        /// Given an object 'obj' do ToString() on it, and then transform it so that all speical XML characters are escaped and return the result. 
        /// If 'quote' is true also surround the resulting object with double quotes.
        /// </summary>
        public static string XmlEscape(object obj, bool quote = false)
        {
            string str = obj.ToString();
            StringBuilder sb = null;
            string entity = null;
            int copied = 0;
            for (int i = 0; i < str.Length; i++)
            {
                switch (str[i])
                {
                    case '&':
                        entity = "&amp;";
                        goto APPEND;
                    case '"':
                        entity = "&quot;";
                        goto APPEND;
                    case '\'':
                        entity = "&apos;";
                        goto APPEND;
                    case '<':
                        entity = "&lt;";
                        goto APPEND;
                    case '>':
                        entity = "&gt;";
                        goto APPEND;
                        APPEND:
                        {
                            if (sb == null)
                            {
                                sb = new StringBuilder();
                                if (quote)
                                {
                                    sb.Append('"');
                                }
                            }
                            while (copied < i)
                            {
                                sb.Append(str[copied++]);
                            }

                            sb.Append(entity);
                            copied++;
                        }
                        break;
                }
            }

            if (sb != null)
            {
                while (copied < str.Length)
                {
                    sb.Append(str[copied++]);
                }

                if (quote)
                {
                    sb.Append('"');
                }

                return sb.ToString();
            }
            if (quote)
            {
                str = "\"" + str + "\"";
            }

            return str;
        }
        /// <summary>
        /// A shortcut for XmlEscape(obj, true) (that is ToString the object, escape XML chars, and then surround with double quotes. 
        /// </summary>
        public static string XmlQuote(object obj)
        {
            return XmlEscape(obj, true);
        }
        /// <summary>
        /// Create a doubly quoted string for the decimal integer value
        /// </summary>
        public static string XmlQuote(int value)
        {
            return "\"" + value + "\"";
        }

        /// <summary>
        /// Create a double quoted string for the hexidecimal value of 'value'
        /// </summary>
        public static string XmlQuoteHex(uint value)
        {
            return "\"0x" + value.ToString("x").PadLeft(8, '0') + "\"";
        }
        /// <summary>
        /// Create a double quoted string for the hexidecimal value of 'value'
        /// </summary>
        public static string XmlQuoteHex(ulong value)
        {
            return "\"0x" + value.ToString("x").PadLeft(8, '0') + "\"";
        }
        /// <summary>
        /// Create a double quoted string for the hexidecimal value of 'value'
        /// </summary>
        public static string XmlQuoteHex(int value)
        {
            return XmlQuoteHex((uint)value);
        }
        /// <summary>
        /// Create a double quoted string for the hexidecimal value of 'value'
        /// </summary>
        public static string XmlQuoteHex(long value)
        {
            return XmlQuoteHex((ulong)value);
        }
    }

}