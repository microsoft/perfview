using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    public class EventMetadata
    {
        public string ProviderName;
        public uint EventId;
        public uint Version;
        public string EventName;
        public ulong Keywords;
        public uint Level;
        // An array of event parameter definition consist of parameter type and name
        public Tuple<TypeCode, string>[] ParameterDefinitions;

        public Guid ProviderId
        {
            get
            {
                if (_ProviderID == Guid.Empty)
                {
                    _ProviderID = GetEventPipeGuidFromName(ProviderName);
                }
                return _ProviderID;
            }
            set
            {
                _ProviderID = value;
            }
        }

        public static Guid GetEventPipeGuidFromName(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                return Guid.Empty;
            }

            // Legacy GUID lookups (events which existed before the current Guid generation conventions)
            if (name == TplEtwProviderTraceEventParser.ProviderName)
            {
                return TplEtwProviderTraceEventParser.ProviderGuid;
            }
            else if (name == ClrTraceEventParser.ProviderName)
            {
                return ClrTraceEventParser.ProviderGuid;
            }
            else if (name == ClrPrivateTraceEventParser.ProviderName)
            {
                return ClrPrivateTraceEventParser.ProviderGuid;
            }
            else if (name == ClrRundownTraceEventParser.ProviderName)
            {
                return ClrRundownTraceEventParser.ProviderGuid;
            }
            else if (name == ClrStressTraceEventParser.ProviderName)
            {
                return ClrStressTraceEventParser.ProviderGuid;
            }

            // Hash the name according to current event source naming conventions
            else
            {
                return TraceEventProviders.GetEventSourceGuidFromName(name);
            }
        }

        #region private
        private Guid _ProviderID = Guid.Empty;
        #endregion
    }
}