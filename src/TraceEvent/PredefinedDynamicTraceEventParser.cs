//     Copyright (c) Microsoft Corporation.  All rights reserved.
using FastSerialization;
using Microsoft.Diagnostics.Tracing.Compatibility;
using Microsoft.Diagnostics.Tracing.Etlx;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Tracing.Parsers
{
    /// <summary>
    /// A PredefinedDynamicTraceEventParser provides support for strongly typed TraceLogging events.
    /// TraceLogging events (also known as self-describing events) typically lack strong typing 
    /// information, making them difficult to use in strongly typed scenarios.
    /// 
    /// This parser allows you to register strongly typed templates for TraceLogging events,
    /// similar to what is supported for manifest-based events.
    /// </summary>
    public abstract class PredefinedDynamicTraceEventParser : TraceEventParser
    {
        /// <summary>
        /// Create a new PredefinedDynamicTraceEventParser and attach it to the given TraceEventSource.
        /// </summary>
        public PredefinedDynamicTraceEventParser(TraceEventSource source)
            : base(source)
        {
            // Initialize state
            state = (PredefinedDynamicTraceEventParserState)StateObject;
            if (state == null)
            {
                StateObject = state = new PredefinedDynamicTraceEventParserState();
            }

            // Register to handle unhandled events so we can check for TraceLogging events
            this.source.RegisterUnhandledEvent(OnUnhandledEvent);

            // No need to create a dynamic parser as we use RegisteredTraceEventParser.TryLookupWorker directly
        }

        /// <summary>
        /// Register a template for a TraceLogging event. This allows for strongly typed access
        /// to the event's data.
        /// </summary>
        /// <param name="template">The template defining the event structure</param>
        protected void RegisterTemplate(PredefinedDynamicEvent template)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            // Add the template to our collection
            state.AddTemplate(template.ProviderGuid, template.EventName, template);
        }



        /// <summary>
        /// Returns an enumerable of all registered TraceLogging event templates.
        /// </summary>
        public IEnumerable<PredefinedDynamicEvent> RegisteredTemplates
        {
            get { return state.GetAllTemplates(); }
        }

        /// <summary>
        /// Override. TraceEventParser objects conceptually attached to 'sources' have a 'state' object
        /// that remember registered event templates.
        /// </summary>
        protected internal override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            // This is called once before state is initialized.
            if (state == null) return;

            foreach (var template in state.GetAllTemplates())
            {
                if (eventsToObserve != null)
                {
                    var response = eventsToObserve(template.ProviderName, template.EventName);
                    if (response != EventFilterResponse.AcceptEvent)
                    {
                        continue;
                    }
                }

                // Note: Do not clone here - Subscribe will clone the template
                // Cloning here would cause double-cloning when used with AddCallbackForProviderEvents
                callback(template);
            }
        }

        /// <summary>
        /// Returns the provider name for this parser.
        /// </summary>
        protected abstract override string GetProviderName();

        /// <summary>
        /// Check if this is a TraceLogging event that we have a template for.
        /// If so, update the template and return true so the caller will dispatch it.
        /// </summary>
        private bool OnUnhandledEvent(TraceEvent data)
        {
            PredefinedDynamicEvent dynamicTraceEventData = null;
            Guid providerGuid = data.ProviderGuid;
            string eventName = data.EventName;

            // If we haven't yet seen this unknown event, we need to lookup its metadata.
            // Keep track of all attempted lookups so that we don't lookup a type that we don't know about over and over.
            if (!state.TryGetLookup(data.ProviderGuid, data.EventName, out dynamicTraceEventData))
            {
                // EventPipe metadata lookup.
                EventPipeEventSource eventPipeSource = data.Source as EventPipeEventSource;
                if (eventPipeSource != null && eventPipeSource.TryGetTemplateFromMetadata(data, out DynamicTraceEventData foundTemplate))
                {
                    providerGuid = foundTemplate.ProviderGuid;
                    eventName = foundTemplate.EventName;
                }
                else
                {
                    // TraceLog metadata lookup.
                    TraceLog traceLog = data.Source as TraceLog;
                    if (traceLog != null)
                    {
                        TraceEventCounts counts = traceLog.Stats.GetEventCounts(data);
                        if (counts != null)
                        {
                            providerGuid = counts.ProviderGuid;
                            eventName = counts.EventName;
                        }
                    }
                    else
                    {
                        // TraceLogging metadata lookup.
                        DynamicTraceEventData parsedTemplate = RegisteredTraceEventParser.TryLookupWorker(data);
                        if (parsedTemplate != null)
                        {
                            providerGuid = parsedTemplate.ProviderGuid;
                            eventName = parsedTemplate.EventName;
                        }
                    }
                }
            }
            else
            {
                if (dynamicTraceEventData != null)
                {
                    // Register the template so that it can be dispatched by the caller.
                    source.RegisterEventTemplate(dynamicTraceEventData);
                    var response = OnNewEventDefintion(dynamicTraceEventData, true);

                    // Return true to indicate the event is handled
                    // The caller will re-lookup and dispatch the event
                    return response == EventFilterResponse.AcceptEvent;
                }

                // We have already looked up this event on a previous call and found no template.
                return false;
            }

            // Check if we have a template for this event
            if (state.TryGetTemplate(providerGuid, eventName, out PredefinedDynamicEvent template))
            {
                // Move the event data to our template
                template.eventID = data.eventID;

                // Register the template so that it can be dispatched by the caller.
                source.RegisterEventTemplate(template);
                var response = OnNewEventDefintion(template, true);

                // Return true to indicate the event is handled
                // The caller will re-lookup and dispatch the event
                return response == EventFilterResponse.AcceptEvent;
            }

            // If we get here, we could not find a template.
            // Save this result so we don't keep trying to do the lookup over and over.
            // We must save the unresolved information because that's what we'd expect to come in again later.
            state.AddLookup(data.ProviderGuid, data.EventName, null);
            return false;
        }

        #region private
        private PredefinedDynamicTraceEventParserState state;
        #endregion
    }

    /// <summary>
    /// State object for PredefinedDynamicTraceEventParser.
    /// </summary>
    internal class PredefinedDynamicTraceEventParserState
    {
        /// <summary>
        /// Used as a key to look up templates by provider GUID and event name
        /// </summary>
        private class TemplateKey : IEquatable<TemplateKey>
        {
            public Guid ProviderGuid { get; }
            public string EventName { get; }

            public TemplateKey(Guid providerGuid, string eventName)
            {
                ProviderGuid = providerGuid;
                EventName = eventName ?? string.Empty;
            }

            public bool Equals(TemplateKey other)
            {
                return ProviderGuid.Equals(other.ProviderGuid) && 
                       string.Equals(EventName, other.EventName, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                if (obj is TemplateKey)
                    return Equals((TemplateKey)obj);
                return false;
            }

            public override int GetHashCode()
            {
                return ProviderGuid.GetHashCode() ^ EventName.GetHashCode();
            }
        }

        public PredefinedDynamicTraceEventParserState()
        {
            registeredTemplates = new Dictionary<TemplateKey, PredefinedDynamicEvent>();
            templateLookups = new Dictionary<TemplateKey, PredefinedDynamicEvent>();
        }

        /// <summary>
        /// Add a template to the dictionary
        /// </summary>
        public void AddTemplate(Guid providerGuid, string eventName, PredefinedDynamicEvent template)
        {
            var key = new TemplateKey(providerGuid, eventName);
            registeredTemplates[key] = template;
        }

        /// <summary>
        /// Get a template from the dictionary
        /// </summary>
        public bool TryGetTemplate(Guid providerGuid, string eventName, out PredefinedDynamicEvent template)
        {
            var key = new TemplateKey(providerGuid, eventName);
            return registeredTemplates.TryGetValue(key, out template);
        }

        /// <summary>
        /// Add a template to the lookup dictionary
        /// </summary>
        public void AddLookup(Guid providerGuid, string eventName, PredefinedDynamicEvent template)
        {
            var key = new TemplateKey(providerGuid, eventName);
            templateLookups[key] = template;
        }

        /// <summary>
        /// Get a template from the lookup dictionary
        /// </summary>
        public bool TryGetLookup(Guid providerGuid, string eventName, out PredefinedDynamicEvent template)
        {
            var key = new TemplateKey(providerGuid, eventName);
            return templateLookups.TryGetValue(key, out template);
        }

        /// <summary>
        /// Get all templates from the dictionary
        /// </summary>
        public IEnumerable<PredefinedDynamicEvent> GetAllTemplates()
        {
            return registeredTemplates.Values;
        }

        private Dictionary<TemplateKey, PredefinedDynamicEvent> registeredTemplates;
        private Dictionary<TemplateKey, PredefinedDynamicEvent> templateLookups;
    }
}