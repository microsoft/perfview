using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Diagnostics.Tracing.Ctf
{
    /// <summary>
    /// The parsed metadata.
    /// </summary>
    class CtfMetadata
    {
        private CtfTrace _trace;
        private CtfEnvironment _environment;
        private Dictionary<string, CtfClock> _clocks = new Dictionary<string, CtfClock>();
        private List<CtfStream> _streams = new List<CtfStream>();
        private Dictionary<string, CtfMetadataType> _typeAlias = new Dictionary<string, CtfMetadataType>();
        CtfMetadataParser _parser;

        public bool IsLoaded { get; private set; }

        internal void WriteMetadata(TextWriter output)
        {
            _trace.WriteLine(output, 0);
            _environment.WriteLine(output, 0);

            foreach (CtfClock clock in _clocks.Values)
                clock.WriteLine(output, 0);

            foreach (CtfStream stream in _streams)
                stream.WriteLine(output, 0);
        }

        public CtfTrace Trace { get { return _trace; } }
        public CtfEnvironment Environment { get { return _environment; } }
        public IList<CtfStream> Streams { get { return _streams; } }
        public ICollection<CtfClock> Clocks { get { return _clocks.Values; } }
        
        public CtfMetadata(CtfMetadataParser parser)
        {
            _parser = parser;
        }
        
        public void Load()
        {
            if (IsLoaded)
                return;

            IsLoaded = true;

            CtfStream stream;
            foreach (CtfMetadataDeclaration entry in _parser.Parse())
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
                        _environment = new CtfEnvironment(entry.Properties);
                        break;

                    case CtfDeclarationTypes.TypeAlias:
                        _typeAlias[entry.Name] = entry.Type;
                        break;

                    case CtfDeclarationTypes.Struct:
                        _typeAlias[entry.Name] = new CtfStruct(entry.Properties, entry.Fields);
                        break;

                    case CtfDeclarationTypes.Stream:
                        stream = new CtfStream(entry.Properties);
                        while (_streams.Count <= stream.ID)
                            _streams.Add(null);

                        _streams[stream.ID] = stream;
                        break;

                    case CtfDeclarationTypes.Event:
                        CtfEvent evt = new CtfEvent(entry.Properties);
                        stream = _streams[evt.Stream];
                        stream.AddEvent(evt);
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

        internal void WriteLine(TextWriter output, int indent)
        {
            string ind = new string(' ', indent);

            output.WriteLine("{0}Trace:", ind);
            output.WriteLine("{0}    Version:    {1}.{2}", ind, Major, Minor);
            output.WriteLine("{0}    UUID:       {1}", ind, UUID);
            output.WriteLine("{0}    Byte order: {1}", ind, ByteOrder);
            output.WriteLine("{0}    Header:", ind);

            Header.WriteLine(output, null, indent + 4);
            output.WriteLine();
        }

        internal void ResolveReferences(Dictionary<string, CtfMetadataType> typealias)
        {
            Header.ResolveReference(typealias);
        }
    }

    class CtfStream
    {
        List<CtfEvent> _events = new List<CtfEvent>();

        CtfMetadataType _header;
        CtfMetadataType _context;
        CtfMetadataType _eventContext;

        public int ID { get; private set; }
        public CtfStruct EventHeader { get { return (CtfStruct)_header; } }
        public CtfStruct PacketContext { get { return (CtfStruct)_context; } }
        public CtfStruct EventContext { get { return (CtfStruct)_eventContext; } }
        public IList<CtfEvent> Events { get { return _events; } }

        public CtfStream(CtfPropertyBag properties)
        {
            ID = properties.GetInt("id");
            _header = properties.GetType("event.header");
            _context = properties.GetType("packet.context");
            _eventContext = properties.GetType("event.context");
        }

        public void AddEvent(CtfEvent evt)
        {
            while (_events.Count <= evt.ID)
                _events.Add(null);

            Debug.Assert(_events[evt.ID] == null);
            _events[evt.ID] = evt;
        }

        internal void ResolveReferences(Dictionary<string, CtfMetadataType> typealias)
        {
            _header = _header.ResolveReference(typealias);
            _header.ResolveReference(typealias);

            _context = _context.ResolveReference(typealias);
            _context.ResolveReference(typealias);

            foreach (CtfEvent evt in _events)
                evt.ResolveReferences(typealias);
        }

        internal void WriteLine(TextWriter output, int indent)
        {
            string ind = new string(' ', indent);
            output.WriteLine("{0}Stream {1}:", ind, ID);
            output.WriteLine("{0}    Header:", ind);
            EventHeader.WriteLine(output, null, indent + 8);

            output.WriteLine("{0}    PacketContext:", ind);
            PacketContext.WriteLine(output, null, indent + 8);

            output.WriteLine("{0}    Events:", ind);
            foreach (CtfEvent evt in _events)
                evt.WriteLine(output, null, indent + 8);

            output.WriteLine();
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

        internal void WriteLine(TextWriter output, int indent)
        {
            string ind = new string(' ', indent);
            output.WriteLine("{0}Environment:", ind);
            output.WriteLine("{0}    Host name: {1}", ind, HostName);
            output.WriteLine("{0}    Domain:    {1}", ind, Domain);
            output.WriteLine("{0}    Tracer:    {1} ({2}.{3})", ind, TracerName, TracerMajor, TracerMinor);
            output.WriteLine();
        }
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
        
        internal void WriteLine(TextWriter output, int indent)
        {
            string ind = new string(' ', indent);
            output.WriteLine("{0}Clock:", ind);
            output.WriteLine("{0}    Name:        {1}", ind, Name);
            output.WriteLine("{0}    Description: {1}", ind, Description);
            output.WriteLine("{0}    UUID:        {1}", ind, UUID);
            output.WriteLine("{0}    Offset:      {1}", ind, Offset);
            output.WriteLine();
        }
    }

    class CtfEvent
    {
        const int SizeUninitialized = -2;
        public const int SizeIndeterminate = -1;

        int _size = SizeUninitialized;
        
        public bool IsFixedSize { get { return Size != SizeIndeterminate; } }

        public int Size
        {
            get
            {
                if (_size == SizeUninitialized)
                    _size = Fields.GetSize();

                Debug.Assert(_size >= SizeIndeterminate);
                return _size;
            }
        }

        public int ID { get; private set; }
        public string Name { get; private set; }
        public int Stream { get; private set; }
        public uint LogLevel { get; private set; }
        public CtfStruct Fields { get; private set; }

        public CtfEvent(CtfPropertyBag bag)
        {
            ID = bag.GetInt("id");
            Name = bag.GetString("name");
            Stream = bag.GetInt("stream_id");
            LogLevel = bag.GetUInt("loglevel");

            Fields = bag.GetStruct("fields");
        }

        internal void ResolveReferences(Dictionary<string, CtfMetadataType> typealias)
        {
            Fields.ResolveReference(typealias);
        }

        internal void WriteLine(TextWriter output, object[] values, int indent)
        {
            string ind = new string(' ', indent);
            output.WriteLine("{0}Event {1}:", ind, ID);
            output.WriteLine("{0}    Name:     {1}", ind, Name);
            output.WriteLine("{0}    LogLevel: {1}", ind, LogLevel);

            var actualFields = Fields.Fields;
            if (actualFields.Length > 0)
            {
                output.WriteLine("{0}    Fields:", ind);

                if (values == null)
                {
                    foreach (CtfField field in actualFields)
                        field.WriteLine(output, null, indent + 8);
                }
                else
                {
                    for (int i = 0; i < actualFields.Length; i++)
                        actualFields[i].WriteLine(output, values[i], indent + 8);
                }
            }
            else
            {
                output.WriteLine("{0}    No fields.", ind);
            }

            output.WriteLine();
        }

        public override string ToString()
        {
            return Name;
        }

        public int GetFieldOffset(string name)
        {
            return Fields.GetFieldOffset(name);
        }
    }
}
