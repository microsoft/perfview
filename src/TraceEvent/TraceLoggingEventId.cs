using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Tracing
{
    /// <summary>
    /// TraceLoggingEventId is a class that manages assigning event IDs (small 64k numbers)
    /// to TraceLogging Style events (which don't have them).  Because TraceEvent uses EventIDs
    /// so fundamentally this deficiency is very problematic.   
    /// 
    /// Arguably this should have been done by the ETW system itself.  
    /// 
    /// You use it by calling TestForTraceLoggingEventAndFixupIfNeeded on eventRecords.  
    /// You also have to explicitly call 'Dispose' when you are done with this class.  
    /// </summary>
    internal unsafe struct TraceLoggingEventId : IDisposable
    {
        /// <summary>
        /// Checks to see if eventRecord has TraceLogging meta data associated with it (EVENT_HEADER_EXT_TYPE_EVENT_SCHEMA_TL)
        /// and if so updates EventHeader.Id to be an event ID unique to that provider/opcode/meta-data blob. 
        /// </summary>
        public void TestForTraceLoggingEventAndFixupIfNeeded(TraceEventNativeMethods.EVENT_RECORD* eventRecord)
        {
            // This method is designed to be inlined and thus have very low overhead for the non-tracelogging case.   
            if (eventRecord->ExtendedDataCount != 0)
            {
                TestForTraceLoggingEventAndFixupIfNeededHelper(eventRecord);
            }
        }

        /// <summary>
        /// cleans up native memory allocated by this routine.   
        /// </summary>
        public void Dispose()
        {
            if (m_traceLoggingEventMap == null)
            {
                return;
            }

            foreach (var kvp in m_traceLoggingEventMap)
            {
                Marshal.FreeHGlobal((IntPtr)kvp.Key.Provider);
            }

            m_traceLoggingEventMap = null;
            m_nextTraceLoggingIDForProvider = null;
        }

        #region private

        /// <summary>
        /// Checks to see if this event has TraceLogging meta data associated with it (EVENT_HEADER_EXT_TYPE_EVENT_SCHEMA_TL)
        /// and if so updates EventHeader.Id to be an event ID unique to that provider/opcode/meta-data blob. 
        /// </summary>
        private void TestForTraceLoggingEventAndFixupIfNeededHelper(TraceEventNativeMethods.EVENT_RECORD* eventRecord)
        {
            var ptr = eventRecord->ExtendedData;
            var end = &eventRecord->ExtendedData[eventRecord->ExtendedDataCount];
            while (ptr < end)
            {
                if (ptr->ExtType == TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_EVENT_SCHEMA_TL)
                {
                    eventRecord->EventHeader.Id = GetEventIDForTraceLoggingEvent(eventRecord, ptr);
                }

                ptr++;
            }
        }

        /// <summary>
        /// given that 'eventRecord' is a TraceLogging event (with meta-data 'metaData'), return a eventID that is unique
        /// to that provider/opcode/meta-data blob.  
        /// </summary>
        private ushort GetEventIDForTraceLoggingEvent(TraceEventNativeMethods.EVENT_RECORD* eventRecord, TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM* metaData)
        {
            Debug.Assert(metaData->ExtType == TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_EVENT_SCHEMA_TL);
            if (m_traceLoggingEventMap == null)     // Lazy init.  
            {
                m_traceLoggingEventMap = new Dictionary<ProviderMetaDataKey, ushort>();
                m_nextTraceLoggingIDForProvider = new Dictionary<Guid, ushort>();
            }

            // Check if I am in the table of assigned eventIds for this meta-data- blob
            ProviderMetaDataKey key = new ProviderMetaDataKey(&eventRecord->EventHeader.ProviderId, eventRecord->EventHeader.Opcode, (byte*)metaData->DataPtr, metaData->DataSize);
            ushort ret;
            if (!m_traceLoggingEventMap.TryGetValue(key, out ret))
            {
                // No then get the next ID for this particular provider (and allocate a new one)
                if (!m_nextTraceLoggingIDForProvider.TryGetValue(eventRecord->EventHeader.ProviderId, out ret))
                {
                    ret = 0xFF00;   // We arbitrarily pick the 'high end' of the event ID range to stay way from user-allocated IDs.   However we also avoid the last 256 ID just in case.  
                }

                --ret;
                m_nextTraceLoggingIDForProvider[eventRecord->EventHeader.ProviderId] = ret;
                if (ret == 0) // means we wrapped around.  We have no more!
                {
                    throw new InvalidOperationException("Error ran out of TraceLogging Event IDs for provider " + eventRecord->EventHeader.ProviderId);
                }

                // Make a copy of memory the key points at.   Thus the table 'owns' the data the keys point at.   
                // This is reclaimed in 'Dispose'
                int copyDataSize = (key.DataSize + 3) & ~3;         // round it up to a multiple of 4.  CopyBlob requires this.  
                byte* copy = (byte*)Marshal.AllocHGlobal(copyDataSize + sizeof(Guid));
                key.Provider = (Guid*)(copy);
                *key.Provider = eventRecord->EventHeader.ProviderId;
                copy += sizeof(Guid);
                TraceEvent.CopyBlob((IntPtr)key.Data, (IntPtr)copy, copyDataSize);
                key.Data = copy;

                // Add the new key and eventID to the table. 
                m_traceLoggingEventMap.Add(key, ret);
            }
            return ret;
        }

        /// <summary>
        /// ProviderMetaDataKey is what we use to look up TraceLogging meta-data.  It is 
        /// basically just GUID (representing the provider) an opcode (start/stop) and 
        /// a blob (representing the TraceLogging meta-data for an event) that knows how to 
        /// compare itself so it can be a key to a hash table. 
        /// </summary>
        private struct ProviderMetaDataKey : IEquatable<ProviderMetaDataKey>
        {
            public ProviderMetaDataKey(Guid* provider, byte opcode, byte* data, int dataSize)
            {
                Provider = provider;
                Opcode = opcode;
                Data = data;
                DataSize = dataSize;
            }

            public Guid* Provider;
            public byte* Data;
            public int DataSize;
            public byte Opcode;

            public override int GetHashCode()
            {
                int ret = DataSize + Provider->GetHashCode();
                if (4 <= DataSize)
                {
                    ret += *((int*)Data);
                }

                return ret;
            }

            public bool Equals(ProviderMetaDataKey other)
            {
                if (DataSize != other.DataSize)
                {
                    return false;
                }

                if (*Provider != *other.Provider)
                {
                    return false;
                }

                if (Opcode != other.Opcode)
                {
                    return false;
                }

                byte* ptrOther = other.Data;
                byte* endPtr = Data + DataSize;
                for (byte* ptr = Data; ptr < endPtr; ptr++)
                {
                    if (*ptr != *ptrOther)
                    {
                        return false;
                    }

                    ptrOther++;
                }
                return true;
            }
        }

        // Given particular provider, opcode and tracelogging meta-data blob, look up the assigned ID
        private Dictionary<ProviderMetaDataKey, ushort> m_traceLoggingEventMap;

        // For each provider look up the next unassigned eventID that can be used. 
        private Dictionary<Guid, ushort> m_nextTraceLoggingIDForProvider;
        #endregion 
    }
}
