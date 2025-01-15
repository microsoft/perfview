// Copyright (c) Microsoft Corporation.  All rights reserved
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Tracing
{
    /// <summary>
    /// A small number that you can get from the GetReferenceForGCAddress that is
    /// invariant as the GC address moves around during GCs.   Because this index
    /// is small it can be used to store information about the GC reference in a
    /// side growable array.  
    /// </summary>
    public enum GCReferenceID
    {
        /// <summary>
        /// Indicates that the address is no longer alive.  
        /// </summary>
        Dead = -1
    };

    // TODO FIX NOW NOT DONE

    /// <summary>
    /// This computer will keep track of GC references as they change over time 
    /// </summary>
    public class GCReferenceComputer
    {
        /// <summary>
        /// Create a new GCRefernece computer from the stream of events 'source'.   When 'source' is processed
        /// you can call 'GetReferenceForGCAddress' to get stable ids for GC references.  
        /// </summary>
        /// <param name="source"></param>
        public GCReferenceComputer(TraceEventDispatcher source)
        {
            source.Clr.GCBulkMovedObjectRanges += delegate (GCBulkMovedObjectRangesTraceData data)
            {
            };
            source.Clr.GCBulkSurvivingObjectRanges += delegate (GCBulkSurvivingObjectRangesTraceData data)
            {
            };
            source.Clr.GCStart += delegate (GCStartTraceData data)
            {
            };
            source.Clr.GCGenerationRange += delegate (GCGenerationRangeTraceData data)
            {
            };

        }

        /// <summary>
        /// Get a stable ID for a GcAddress.  This ID can be compared for object identity.
        /// This only works at the current point in time when scanning the source.  
        /// </summary>
        public GCReferenceID GetReferenceForGCAddress(Address GcAddress)
        {
            return (GCReferenceID)(int)GcAddress;
        }

        /// <summary>
        /// If you no longer need to track the GC reference, call this function to remove the tracking.  
        /// </summary>
        public void DisposeGCReference(GCReferenceID GCReferenceID)
        {
        }
    }

}



