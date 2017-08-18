using FastSerialization;
using System;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    public abstract class EventPipeEventSource : TraceEventDispatcher
    {
        public EventPipeEventSource(Deserializer deserializer)
        {
            if (deserializer == null)
            {
                throw new ArgumentNullException(nameof(deserializer));
            }

            _deserializer = deserializer;
        }

        ~EventPipeEventSource()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {                                                                                                                                           
                _deserializer?.Dispose();
            }

            base.Dispose(disposing);
            GC.SuppressFinalize(this);
        }

        public const string EventPipe = "EventPipe";

        #region Private
        protected Deserializer _deserializer;
        #endregion

    }
}
