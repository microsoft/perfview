//     Copyright (c) Microsoft Corporation.  All rights reserved.

namespace System.Diagnostics.Tracing
{

    /// <summary>
    /// Used to send the rawManifest into the event stream as a series of events.  
    /// </summary>
    internal struct ManifestEnvelope
    {
        public const int MaxChunkSize = 0xFF00;
        public enum ManifestFormats : byte
        {
            SimpleXmlFormat = 1,          // Simply dump the XML manifest
        }

        // If you change these, please update DynamicManifestTraceEventData
        public ManifestFormats Format;
        public byte MajorVersion;
        public byte MinorVersion;
        public byte Magic;              // This is always 0x5B, which marks this as an envelope (with 1/256 probability of random collision)
        public ushort TotalChunks;
        public ushort ChunkNumber;
    };

}