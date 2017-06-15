using FastSerialization;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    public static class EventPipeEventSourceFactory
    {
        /// <summary>
        /// Create a compatible EventPipe event source for the given file.
        /// </summary>
        /// <param name="fileName">The full path of the event pipe file name.</param>
        /// <returns>The compatible EventPipe event source.</returns>
        public static EventPipeEventSource CreateEventPipeEventSource(string fileName)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException(fileName);
            }

            var deserializer = new Deserializer(fileName);

            deserializer.ReadBytesAndVerify(s_ExpectedHeaderTags);

            // Read the version.
            var version = deserializer.ReadInt();

            // Read the minimum reader version.
            var minimumReaderVersion = deserializer.ReadInt();

            switch (version)
            {
                case 1: return new EventPipeEventSourceV1(deserializer, fileName);
                default: throw new NotSupportedException($"The version of {fileName} is {version} which is not yet supported.");
            }
        }

        #region Private
        private static byte[] s_ExpectedHeaderTags = new byte[] {
            0x4 /*BeginObject tag*/,
            0x4 /*BeginObject tag*/,
            0x1 /*SerializationType for SerializationType itself is null*/};
        #endregion
    }
}
