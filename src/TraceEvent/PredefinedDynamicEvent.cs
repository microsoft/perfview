//     Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Tracing.Parsers
{
    /// <summary>
    /// PredefinedDynamicEvent is used to create strongly typed representations of self-describing events.
    /// This allows self-describing events to have the same level of strongly typed
    /// support as manifested events.
    /// </summary>
    public abstract class PredefinedDynamicEvent : TraceEvent
    {
        /// <summary>
        /// Creates a template for a self-describing event.
        /// </summary>
        /// <param name="eventName">The name of the event</param>
        /// <param name="providerGuid">The GUID of the provider</param>
        /// <param name="providerName">The name of the provider</param>
        public PredefinedDynamicEvent(string eventName, Guid providerGuid, string providerName)
            : base(0, 0, eventName, Guid.Empty, 0, eventName, providerGuid, providerName)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                throw new ArgumentNullException(nameof(eventName));
            }
            if (providerGuid == Guid.Empty)
            {
                throw new ArgumentException("Provider GUID cannot be empty", nameof(providerGuid));
            }
            if (string.IsNullOrEmpty(providerName))
            {
                throw new ArgumentNullException(nameof(providerName));
            }

            this.eventName = eventName;
            containsSelfDescribingMetadata = true;
        }

        /// <summary>
        /// Dispatches the event to the appropriate handler.
        /// </summary>
        protected internal override void Dispatch()
        {
            Action<TraceEvent> callback = m_action;
            if (callback != null)
                callback(this);
        }

        /// <summary>
        /// Gets or sets the target action for this event.
        /// </summary>
        protected internal override Delegate Target
        {
            get { return m_action; }
            set { m_action = (Action<TraceEvent>)value; }
        }

        /// <summary>
        /// Creates a clone of this template.
        /// </summary>
        /// <returns>A new instance of the template</returns>
        public override unsafe TraceEvent Clone()
        {
            var clone = (PredefinedDynamicEvent)base.Clone();
            
            // Reset source-specific fields to ensure the clone can be registered with a new source
            clone.traceEventSource = null;
            clone.Target = null;

            return clone;
        }

        #region private
        private Action<TraceEvent> m_action;
        #endregion
    }


}