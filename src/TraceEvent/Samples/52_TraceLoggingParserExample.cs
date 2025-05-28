//     Copyright (c) Microsoft Corporation.  All rights reserved.
using Microsoft.Diagnostics.Tracing.Parsers;
using System;

namespace Microsoft.Diagnostics.Tracing.Samples
{
    /// <summary>
    /// Sample implementation of a strongly typed PredefinedDynamic event.
    /// This demonstrates how to create custom strongly typed dynamic event types.
    /// </summary>
    public class SamplePredefinedDynamicEvent : PredefinedDynamicEvent
    {
        /// <summary>
        /// Create a new instance of the sample event
        /// </summary>
        public SamplePredefinedDynamicEvent()
            : base("SampleEvent",
                Guid.Parse("ABCDEF12-3456-7890-ABCD-EF1234567890"),
                "SampleProvider")
        {
        }

        /// <summary>
        /// Gets the message field from the event payload
        /// </summary>
        public string Message
        {
            get { return GetUnicodeStringAt(0); }
        }

        /// <summary>
        /// Gets the ID field from the event payload
        /// </summary>
        public int Id
        {
            get { return GetInt32At(SkipUnicodeString(0)); }
        }

        /// <summary>
        /// Gets the timestamp field from the event payload
        /// </summary>
        public DateTime Timestamp
        {
            get { return GetDateTimeAt(SkipUnicodeString(0) + 4); }
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "Message", "Id", "Timestamp" };
                }

                return payloadNames;
            }
        }

        /// <summary>
        /// Overrides the payload value retrieval to provide strongly typed access to fields
        /// </summary>
        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Message;
                case 1:
                    return Id;
                case 2:
                    return TimeStamp;
                default:
                    return null;
            }
        }
    }
}