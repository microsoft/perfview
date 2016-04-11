using System;
using System.IO;
using System.Reflection;

namespace Utilities
{
    class ExceptionMessage
    {
        /// <summary>
        /// Figures out a good user message for the exception 'e'   For things that directly
        /// relate to user actions we simply print the message, for everything else we print
        /// a detailed diagnostic message.  We set 'userLevel' to which one it was.  
        /// </summary>
        static public string GetUserMessage(Exception ex, out bool userLevel)
        {
            if (ex is ApplicationException || ex is FileNotFoundException || ex is DirectoryNotFoundException
                || ex is IOException || ex is UnauthorizedAccessException || ex is CommandLineParserException
                || ex is FormatException || ex is FastSerialization.SerializationException ||  ex is System.Xml.XmlException || 
		|| ex is NotSupportedException ||
                ex is ArgumentException && 
                    (ex.Message.StartsWith("Illegal characters in path", StringComparison.OrdinalIgnoreCase) ||
                     ex.Message.StartsWith("Requested value")))
            {
                userLevel = true;
                return "Error: " + ex.Message;
            }
            else
            {
                // This are really internal programmer exceptions, but printing them is useful for debugging.  
                userLevel = false;
                return "Exception Occurred: " + ex.ToString();
            }
        }
    }
}
