using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using Microsoft.Diagnostics.Tracing.AutomatedAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class AutomatedAnalysisTextWriterTests : TestBase
    {
        public AutomatedAnalysisTextWriterTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void SingleThread_SingleMessage()
        {
            using (TestEventListener listener = new TestEventListener())
            {
                string s = "Hello World!";
                AutomatedAnalysisTextWriter writer = AutomatedAnalysisTextWriter.Instance;
                writer.WriteLine(s);

                Assert.Single(listener.Messages);
                Assert.Equal(s, listener.Messages.First());
            }
        }

        [Fact]
        public void SingleThread_MultiMessage()
        {
            int numItems = 3;

            using (TestEventListener listener = new TestEventListener())
            {
                string s = "Hello World!";
                AutomatedAnalysisTextWriter writer = AutomatedAnalysisTextWriter.Instance;
                for (int i = 0; i < numItems; i++)
                {
                    writer.WriteLine(s);
                }

                Assert.Equal(numItems, listener.Messages.Count());
                for (int i = 0; i < numItems; i++)
                {
                    Assert.Equal(s, listener.Messages[i]);
                }
            }
        }

        [Fact]
        public void SingleThread_MultiPartMessages()
        {
            int numItems = 3;

            using (TestEventListener listener = new TestEventListener())
            {
                string s = "Hello World!";
                AutomatedAnalysisTextWriter writer = AutomatedAnalysisTextWriter.Instance;
                for (int i = 0; i < numItems; i++)
                {
                    writer.Write('H');
                    writer.Write("ello ");
                    writer.WriteLine("World!");
                }

                Assert.Equal(numItems, listener.Messages.Count());
                for (int i = 0; i < numItems; i++)
                {
                    Assert.Equal(s, listener.Messages[i]);
                }
            }
        }

        [Fact]
        public void MultiThread_MultiPartMessages()
        {
            int numItems = 6;

            using (TestEventListener listener = new TestEventListener())
            {
                string s = "Hello World!";
                AutomatedAnalysisTextWriter writer = AutomatedAnalysisTextWriter.Instance;

                ManualResetEvent syncEvent = new ManualResetEvent(false);
                Thread[] threads = new Thread[numItems];
                for (int i = 0; i < numItems; i++)
                {
                    threads[i] = new Thread(() =>
                    {
                        // Wait for all threads to be created.
                        syncEvent.WaitOne();

                        writer.Write('H');
                        writer.Write("ello ");
                        writer.WriteLine("World!");
                    });
                    threads[i].IsBackground = true;
                    threads[i].Start();
                }

                // Release the threads to do their work.
                syncEvent.Set();

                // Wait for the threads to die.
                bool anyThreadAlive;
                do
                {
                    anyThreadAlive = false;
                    for (int i = 0; i < numItems; i++)
                    {
                        if (threads[i].IsAlive)
                        {
                            anyThreadAlive = true;
                            Thread.Sleep(10);
                            break;
                        }
                    }
                }
                while (anyThreadAlive);

                Assert.Equal(numItems, listener.Messages.Count());
                for (int i = 0; i < numItems; i++)
                {
                    Assert.Equal(s, listener.Messages[i]);
                }
            }
        }
    }

    public sealed class TestEventListener : EventListener
    {
        public List<string> Messages { get; } = new List<string>();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.Equals("Microsoft-Diagnostics-Tracing-AutomatedAnalysis"))
            {
                EnableEvents(eventSource, EventLevel.Verbose);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            lock (Messages)
            {
                Messages.Add((string)eventData.Payload[0]);
            }
        }
    }
}