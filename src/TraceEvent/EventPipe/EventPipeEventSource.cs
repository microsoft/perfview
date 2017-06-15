using FastSerialization;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
            
            // Add a watermark to indicate it's from EventPipe
            origin = EventPipe;
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
