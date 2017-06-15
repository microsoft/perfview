using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;


#pragma warning disable 1591        // disable warnings on XML comments not being present

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    public sealed class EventPipeTraceEventParser : ExternalTraceEventParser
    {
        public EventPipeTraceEventParser(TraceEventSource source)
            : base(source)
        {
        }

        public void AddTempalte(EventMetadata eventMetadata)
        {
            var key = Tuple.Create(eventMetadata.ProviderId, (TraceEventID)eventMetadata.EventId);
            if (!_templates.ContainsKey(key))
            {
                _templates.Add(key, NewTemplate(eventMetadata.ProviderId, eventMetadata.EventId, eventMetadata.EventName, eventMetadata.ParameterDefinitions));
            }
        }

        #region Override ExternalTraceEventParser
        internal override DynamicTraceEventData TryLookup(TraceEvent unknownEvent)
        {
            if (unknownEvent.IsClassicProvider) return null;

            DynamicTraceEventData template;
            return _templates.TryGetValue(Tuple.Create(unknownEvent.ProviderGuid, unknownEvent.ID), out template) ? template: null;
        }
        #endregion 

        #region Private
        private DynamicTraceEventData NewTemplate(Guid providerId, uint eventId, string eventName, Tuple<TypeCode, string>[] parameterDefinitions)
        {
            int opcode;
            string opcodeName;

            GetOpcodeFromEventName(eventName, out opcode, out opcodeName);

            var template = new DynamicTraceEventData(null, (int)eventId, 0, null, Guid.Empty, opcode, opcodeName, providerId, null);

            if (parameterDefinitions != null && parameterDefinitions.Length > 0)
            {
                template.payloadNames = new string[parameterDefinitions.Length];
                template.payloadFetches = new DynamicTraceEventData.PayloadFetch[parameterDefinitions.Length];

                ushort offset = 0;
                for (int i = 0; i < parameterDefinitions.Length; i++)
                {
                    template.payloadNames[i] = parameterDefinitions[i].Item2;
                    var fetch = new DynamicTraceEventData.PayloadFetch();

                    switch (parameterDefinitions[i].Item1)
                    {
                        case TypeCode.Boolean:
                            {
                                fetch.Type = typeof(bool);
                                fetch.Size = 4; // We follow windows conventions and use 4 bytes for bool.
                                fetch.Offset = offset;
                                break;
                            }
                        case TypeCode.Char:
                            {
                                fetch.Type = typeof(char);
                                fetch.Size = sizeof(char);
                                fetch.Offset = offset;
                                break;
                            }
                        case TypeCode.SByte:
                            {
                                fetch.Type = typeof(SByte);
                                fetch.Size = sizeof(SByte);
                                fetch.Offset = offset;
                                break;
                            }
                        case TypeCode.Byte:
                            {
                                fetch.Type = typeof(byte);
                                fetch.Size = sizeof(byte);
                                fetch.Offset = offset;
                                break;
                            }
                        case TypeCode.Int16:
                            {
                                fetch.Type = typeof(Int16);
                                fetch.Size = sizeof(Int16);
                                fetch.Offset = offset;
                                break;
                            }
                        case TypeCode.UInt16:
                            {
                                fetch.Type = typeof(UInt16);
                                fetch.Size = sizeof(UInt16);
                                fetch.Offset = offset;
                                break;
                            }
                        case TypeCode.Int32:
                            {
                                fetch.Type = typeof(Int32);
                                fetch.Size = sizeof(Int32);
                                fetch.Offset = offset;
                                break;
                            }
                        case TypeCode.UInt32:
                            {
                                fetch.Type = typeof(UInt32);
                                fetch.Size = sizeof(UInt32);
                                fetch.Offset = offset;
                                break;
                            }
                        case TypeCode.Int64:
                            {
                                fetch.Type = typeof(Int64);
                                fetch.Size = sizeof(Int64);
                                fetch.Offset = offset;
                                break;
                            }
                        case TypeCode.UInt64:
                            {
                                fetch.Type = typeof(UInt64);
                                fetch.Size = sizeof(UInt64);
                                fetch.Offset = offset;
                                break;
                            }
                        case TypeCode.Single:
                            {
                                fetch.Type = typeof(Single);
                                fetch.Size = sizeof(Single);
                                fetch.Offset = offset;
                                break;
                            }
                        case TypeCode.Double:
                            {
                                fetch.Type = typeof(Double);
                                fetch.Size = sizeof(Double);
                                fetch.Offset = offset;
                                break;
                            }
                        case TypeCode.Decimal:
                            {
                                fetch.Type = typeof(Decimal);
                                fetch.Size = sizeof(Decimal);
                                fetch.Offset = offset;
                                break;
                            }
                        case TypeCode.DateTime:
                            {
                                fetch.Type = typeof(DateTime);
                                fetch.Size = 8;
                                fetch.Offset = offset;
                                break;
                            }
                        case TypeCode.String:
                            {
                                fetch.Type = typeof(String);
                                fetch.Size = DynamicTraceEventData.NULL_TERMINATED;
                                fetch.Offset = offset;
                                break;
                            }
                        default:
                            {
                                throw new NotSupportedException($"{parameterDefinitions[i].Item1} is not supported.");
                            }
                    }

                    if (fetch.Size >= DynamicTraceEventData.SPECIAL_SIZES || offset == ushort.MaxValue)
                        offset = ushort.MaxValue;           // Indicate that the offset must be computed at run time.
                    else
                        offset += fetch.Size;

                    template.payloadFetches[i] = fetch;
                }
            }
            else
            {
                template.payloadNames = new string[0];
                template.payloadFetches = new DynamicTraceEventData.PayloadFetch[0];
            }

            return template;
        }

        private void GetOpcodeFromEventName(string eventName, out int opcode, out string opcodeName)
        {
            opcode = 0;
            opcodeName = null;

            if (eventName != null)
            {

                if (eventName.EndsWith("Start", StringComparison.OrdinalIgnoreCase))
                {
                    opcode = (int)TraceEventOpcode.Start;
                    opcodeName = nameof(TraceEventOpcode.Start);
                }
                else if (eventName.EndsWith("Stop", StringComparison.OrdinalIgnoreCase))
                {
                    opcode = (int)TraceEventOpcode.Stop;
                    opcodeName = nameof(TraceEventOpcode.Stop);
                }
            }
        }

        Dictionary<Tuple<Guid, TraceEventID>, DynamicTraceEventData> _templates = new Dictionary<Tuple<Guid, TraceEventID>, DynamicTraceEventData>();
        #endregion
    }
}