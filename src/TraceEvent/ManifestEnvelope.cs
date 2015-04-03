//     Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using FastSerialization;
using Microsoft.Diagnostics.Tracing.Parsers;

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
        public byte Magic;
        public ushort TotalChunks;
        public ushort ChunkNumber;
    };

}