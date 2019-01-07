// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Text;

    public sealed class EventSourceTextWriter : TextWriter
    {
        private static readonly TextWriterEventSource TextWriterEventSourceLogger = new TextWriterEventSource();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(string value)
        {
            TextWriterEventSourceLogger.WriteMessage(value);
        }

        public override void Write(string format, object arg0)
        {
            TextWriterEventSourceLogger.WriteMessage(string.Format(format, arg0));
        }

        public override void Write(string format, object arg0, object arg1)
        {
            TextWriterEventSourceLogger.WriteMessage(string.Format(format, arg0, arg1));
        }

        public override void Write(string format, object arg0, object arg1, object arg2)
        {
            TextWriterEventSourceLogger.WriteMessage(string.Format(format, arg0, arg1, arg2));
        }

        public override void Write(string format, params object[] arg)
        {
            TextWriterEventSourceLogger.WriteMessage(string.Format(format, arg));
        }

        public override void Write(char value)
        {
            TextWriterEventSourceLogger.WriteMessage(value.ToString());
        }

        public override void WriteLine()
        {
            TextWriterEventSourceLogger.WriteMessage(string.Empty);
        }

        public override void WriteLine(string value)
        {
            TextWriterEventSourceLogger.WriteLine(value);
        }

        public override void WriteLine(string format, object arg0)
        {
            TextWriterEventSourceLogger.WriteLine(string.Format(format, arg0));
        }

        public override void WriteLine(string format, object arg0, object arg1)
        {
            TextWriterEventSourceLogger.WriteLine(string.Format(format, arg0, arg1));
        }

        public override void WriteLine(string format, object arg0, object arg1, object arg2)
        {
            TextWriterEventSourceLogger.WriteLine(string.Format(format, arg0, arg1, arg2));
        }

        public override void WriteLine(string format, params object[] arg)
        {
            TextWriterEventSourceLogger.WriteLine(string.Format(format, arg));
        }

        private class TextWriterEventSource : EventSource
        {
            public void WriteMessage(string message)
            {
                this.WriteEvent(1, message);
            }

            public void WriteLine(string message)
            {
                this.WriteEvent(2, message);
            }
        }
    }
}