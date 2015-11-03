using System;
using System.IO;

using Microsoft.Diagnostics.Tracing.Etlx;

namespace Microsoft.Diagnostics.Tracing
{
    public unsafe sealed class LttngTextTraceEventSource : TraceEventDispatcher, IDisposable
    {
        private string _FileName;
        private StreamReader _StreamReader;

        public LttngTextTraceEventSource(string fileName)
        {
            _FileName = fileName;
        }

        public override int EventsLost
        {
            get { return 0; }
        }

        public override bool Process()
        {
            // TODO: Should probably change this.
            return true;
        }

        public void ParseAndCopyHeaderData(TraceLog log)
        {
            // TODO: Set these to make the code run.  Need to be backfilled with actual data from the header.
            sessionStartTimeUTC = DateTime.UtcNow;
            osVersion = new Version("0.0.0.0");
            cpuSpeedMHz = 10;
            numberOfProcessors = 1;
            pointerSize = 4;
        }

        private StreamReader Stream
        {
            get
            {
                if (_StreamReader == null)
                {
                    // TODO: Do we need to dispose the reader?
                    _StreamReader = new StreamReader(_FileName);
                }

                return _StreamReader;
            }
        }
    }
}
