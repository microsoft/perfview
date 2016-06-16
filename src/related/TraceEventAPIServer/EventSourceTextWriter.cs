namespace TraceEventAPIServer
{
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Text;

    public sealed class EventSourceTextWriter : TextWriter
    {
        private static readonly TextWriterEventSource textWriterEventSource = new TextWriterEventSource();

        public override void Write(string value)
        {
            textWriterEventSource.WriteMessage(value);
        }

        public override void Write(string format, object arg0)
        {
            textWriterEventSource.WriteMessage(string.Format(format, arg0));
        }

        public override void Write(string format, object arg0, object arg1)
        {
            textWriterEventSource.WriteMessage(string.Format(format, arg0, arg1));
        }

        public override void Write(string format, object arg0, object arg1, object arg2)
        {
            textWriterEventSource.WriteMessage(string.Format(format, arg0, arg1, arg2));
        }

        public override void Write(string format, params object[] arg)
        {
            textWriterEventSource.WriteMessage(string.Format(format, arg));
        }

        public override void Write(char value)
        {
            textWriterEventSource.WriteMessage(value.ToString());
        }

        public override void WriteLine()
        {
            textWriterEventSource.WriteMessage(string.Empty);
        }

        public override void WriteLine(string value)
        {
            textWriterEventSource.WriteLine(value);
        }

        public override void WriteLine(string format, object arg0)
        {
            textWriterEventSource.WriteLine(string.Format(format, arg0));
        }

        public override void WriteLine(string format, object arg0, object arg1)
        {
            textWriterEventSource.WriteLine(string.Format(format, arg0, arg1));
        }

        public override void WriteLine(string format, object arg0, object arg1, object arg2)
        {
            textWriterEventSource.WriteLine(string.Format(format, arg0, arg1, arg2));
        }

        public override void WriteLine(string format, params object[] arg)
        {
            textWriterEventSource.WriteLine(string.Format(format, arg));
        }

        public override Encoding Encoding => Encoding.UTF8;

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