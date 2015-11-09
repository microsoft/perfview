using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Tracing.Ctf
{
    /// <summary>
    /// The parsed metadata.
    /// </summary>
    class CtfMetadata
    {
        private CtfTrace _trace;
        private CtfEnvironment _env;
        private Dictionary<string, CtfClock> _clocks = new Dictionary<string, CtfClock>();
        private List<CtfStream> _streams = new List<CtfStream>();
        private List<CtfEvent> _events = new List<CtfEvent>();
        private Dictionary<string, CtfMetadataType> _typeAlias = new Dictionary<string, CtfMetadataType>();
        

        public CtfTrace Trace { get { return _trace; } }
        public CtfEnvironment Environment { get { return _env; } }
        public IList<CtfStream> Streams { get { return _streams; } }
        public IList<CtfEvent> Events { get { return _events; } }
        public ICollection<CtfClock> Clocks { get { return _clocks.Values; } }


        public CtfMetadata()
        {
        }

        public CtfMetadata(CtfMetadataParser parser)
        {
            Load(parser);
        }


        public void Load(CtfMetadataParser parser)
        {
            foreach (CtfMetadataDeclaration entry in parser.Parse())
            {
                switch (entry.Definition)
                {
                    case CtfDeclarationTypes.Clock:
                        CtfClock clock = new CtfClock(entry.Properties);
                        _clocks[clock.Name] = clock;
                        break;

                    case CtfDeclarationTypes.Trace:
                        _trace = new CtfTrace(entry.Properties);
                        break;

                    case CtfDeclarationTypes.Environment:
                        _env = new CtfEnvironment(entry.Properties);
                        break;

                    case CtfDeclarationTypes.TypeAlias:
                        _typeAlias[entry.Name] = entry.Type;
                        break;

                    case CtfDeclarationTypes.Struct:
                        _typeAlias[entry.Name] = new CtfStruct(entry.Fields);
                        break;

                    case CtfDeclarationTypes.Stream:
                        CtfStream stream = new CtfStream(entry.Properties);
                        while (_streams.Count <= stream.ID)
                            _streams.Add(null);

                        _streams[stream.ID] = stream;
                        break;

                    case CtfDeclarationTypes.Event:
                        CtfEvent evt = new CtfEvent(entry.Properties);
                        while (_events.Count <= evt.ID)
                            _events.Add(null);

                        _events[(int)evt.ID] = evt;
                        break;

                    default:
                        Debug.Fail("Unknown metadata entry type.");
                        break;
                }
            }


            ResolveReferences();
        }
        
        private void ResolveReferences()
        {
            _trace.ResolveReferences(_typeAlias);

            foreach (CtfStream stream in _streams)
                stream.ResolveReferences(_typeAlias);

            foreach (CtfEvent evt in _events)
                evt.ResolveReferences(_typeAlias);

        }
    }



    class CtfTrace
    {
        public short Major { get; private set; }
        public short Minor { get; private set; }
        public Guid UUID { get; private set; }
        public string ByteOrder { get; private set; }
        public CtfStruct Header { get; private set; }

        public CtfTrace(CtfPropertyBag bag)
        {
            Major = bag.GetShort("major");
            Minor = bag.GetShort("minor");
            UUID = new Guid(bag.GetString("uuid"));
            ByteOrder = bag.GetString("byte_order");
            Header = bag.GetStruct("packet.header");
        }

        internal void ResolveReferences(Dictionary<string, CtfMetadataType> typealias)
        {
            Header = (CtfStruct)Header.ResolveReference(typealias);
        }
    }

    class CtfStream
    {
        public int ID { get; private set; }
        public CtfMetadataType EventHeader { get; private set; }
        public CtfMetadataType PacketContext { get; private set; }

        public CtfStream(CtfPropertyBag properties)
        {
            ID = properties.GetInt("id");
            EventHeader = properties.GetType("event.header");
            PacketContext = properties.GetType("packet.context");
        }

        internal void ResolveReferences(Dictionary<string, CtfMetadataType> _typeAlias)
        {
            EventHeader = EventHeader.ResolveReference(_typeAlias);
            PacketContext = PacketContext.ResolveReference(_typeAlias);
        }
    }

    class CtfEnvironment
    {
        public CtfEnvironment(CtfPropertyBag bag)
        {
            HostName = bag.GetString("hostname");
            Domain = bag.GetString("domain");
            TracerName = bag.GetString("tracer");
            TracerMajor = bag.GetIntOrNull("tracer_major") ?? 0;
            TracerMinor = bag.GetIntOrNull("tracer_minor") ?? 0;
        }

        public string Domain { get; private set; }
        public string HostName { get; private set; }
        public string TracerName { get; private set; }
        public int TracerMajor { get; private set; }
        public int TracerMinor { get; private set; }
    }

    class CtfClock
    {
        public CtfClock(CtfPropertyBag bag)
        {
            Name = bag.GetString("name");
            UUID = new Guid(bag.GetString("uuid"));
            Description = bag.GetString("description");
            Frequency = bag.GetUlong("freq");
            Offset = bag.GetUlong("offset");
        }

        public string Description { get; private set; }
        public ulong Frequency { get; private set; }
        public string Name { get; private set; }
        public ulong Offset { get; private set; }
        public Guid UUID { get; private set; }
    }

    class CtfEvent
    {
        public int ID { get; private set; }
        public string Name { get; private set; }
        public uint Stream { get; private set; }
        public uint LogLevel { get; private set; }
        public CtfField[] Fields { get; private set; }

        public CtfEvent(CtfPropertyBag bag)
        {
            ID = bag.GetInt("id");
            Name = bag.GetString("name");
            Stream = bag.GetUInt("stream_id");
            LogLevel = bag.GetUInt("loglevel");

            Fields = bag.GetStruct("fields").Fields;
        }

        internal void ResolveReferences(Dictionary<string, CtfMetadataType> typealias)
        {
            foreach (CtfField field in Fields)
                field.ResolveReference(typealias);
        }
    }
}
