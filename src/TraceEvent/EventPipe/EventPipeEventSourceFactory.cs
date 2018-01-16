using FastSerialization;
using System;
using System.IO;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    public static class EventPipeEventSourceFactory
    {
        private static byte[] s_ExpectedHeaderTags = new byte[] {
            0x4 /*BeginObject tag*/,
            0x4 /*BeginObject tag*/,
            0x1 /*SerializationType for SerializationType itself is null*/};

        /// <summary>
        /// Create a compatible EventPipe event source for the given file.
        /// </summary>
        /// <param name="fileName">The full path of the event pipe file name.</param>
        /// <returns>The compatible EventPipe event source.</returns>
        public static EventPipeEventSource CreateEventPipeEventSource(string fileName)
        {
            if (fileName == null)
                throw new ArgumentNullException(nameof(fileName));

            if (!File.Exists(fileName))
                throw new FileNotFoundException(fileName);

            var deserializer = new Deserializer(fileName);

            var initialData = Read(deserializer, fileName);

            return new EventPipeEventSource(deserializer, initialData);
        }

        // the order of fields is defined in dotnet/coreclr/src/vm/eventpipefile.cpp
        // https://github.com/dotnet/coreclr/blob/c1bbdae7964b19b7063074d36e6af960f0cdc3a0/src/vm/eventpipefile.cpp#L49-L58
        internal static EventPipeEventSourceInitialData Read(Deserializer deserializer, string fileName)
        {
            deserializer.ReadBytesAndVerify(s_ExpectedHeaderTags);

            // Read the version.
            var version = deserializer.ReadInt();

            // Read the minimum reader version.
            var minimumReaderVersion = deserializer.ReadInt();

            if (version <= 2 && minimumReaderVersion == 0)
                return ReadOld(deserializer, fileName, version, minimumReaderVersion);

            return ReadModern(deserializer, fileName, version, minimumReaderVersion); ;
        }

        private static EventPipeEventSourceInitialData ReadModern(Deserializer deserializer, string fileName, int version, int minimumReaderVersion)
        {
            var processName = Path.GetFileNameWithoutExtension(fileName);

            VerifyHardcodedHeader(deserializer);

            var startEventsReference = deserializer.ReadForwardReference();
            var startOfEventStream = deserializer.ResolveForwardReference(startEventsReference, preserveCurrent: true);
            var endEventsReference = deserializer.ReadForwardReference();
            var endOfEventStream = deserializer.ResolveForwardReference(endEventsReference, preserveCurrent: true);

            var creationTime = ReadTimeFrom128Bit(deserializer);

            var startTimeStamp = deserializer.ReadInt64();
            var clockFrequency = deserializer.ReadInt64();

            var pointerSize = deserializer.ReadInt();
            var numberOfProcessors = deserializer.ReadInt();

            // we now move the the pointer to the start of events (end of initial data)
            // future versions might add some extra metadata (like OS version)
            // we don't know how to read these fields, so we just go to the begining of the stream (sth like stream.Position) 
            deserializer.Goto(startOfEventStream);

            return new EventPipeEventSourceInitialData(processName, version, minimumReaderVersion,
                creationTime, startTimeStamp, clockFrequency,
                pointerSize, numberOfProcessors,
                endOfEventStream);
        }

        private static EventPipeEventSourceInitialData ReadOld(Deserializer deserializer, string fileName, int version, int minimumReaderVersion)
        {
            var processName = Path.GetFileNameWithoutExtension(fileName);

            VerifyHardcodedHeader(deserializer);

            // Read END of event stream marker (the old version assumed that events start right after the initial data
            var reference = deserializer.ReadForwardReference();
            var endOfEventStream = deserializer.ResolveForwardReference(reference, preserveCurrent: true);

            var creationTime = ReadTimeFrom128Bit(deserializer);

            var startTimeStamp = deserializer.ReadInt64();
            var clockFrequency = deserializer.ReadInt64();

            return new EventPipeEventSourceInitialData(processName, version, minimumReaderVersion,
                creationTime, startTimeStamp, clockFrequency,
                8, // V1 EventPipe only supported x64
                1, // // old version has not info about processors#
                endOfEventStream);
        }

        private static void VerifyHardcodedHeader(Deserializer deserializer)
        {
            // Read and check the type name
            deserializer.ReadStringAndVerify("Microsoft.DotNet.Runtime.EventPipeFile");

            // Read tag and check
            deserializer.ReadByteAndVerify(0x6 /*EndObject tag*/);
        }

        private static DateTime ReadTimeFrom128Bit(Deserializer deserializer)
        {
            // Read the date and time of trace start.
            var year = deserializer.ReadInt16();
            var month = deserializer.ReadInt16();
            var dayOfWeek = deserializer.ReadInt16();
            var day = deserializer.ReadInt16();
            var hour = deserializer.ReadInt16();
            var minute = deserializer.ReadInt16();
            var second = deserializer.ReadInt16();
            var milliseconds = deserializer.ReadInt16();

            return new DateTime(year, month, day, hour, minute, second, milliseconds, DateTimeKind.Utc);
        }
    }
}