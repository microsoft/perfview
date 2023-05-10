using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Xunit;

namespace TraceEventTests
{
    public class Issue1767 : IDisposable
    {
        int sessionCount = 65;
        List<string> sessions = new List<string>();

        public Issue1767()
        {
            try
            {
                for (int i = 0; i < sessionCount; i++)
                {
                    string name = $"Issue1767Test_{i}";
                    sessions.Add(name);
                    TraceEventSession session = new TraceEventSession(name);
                    session.EnableProvider(ClrTraceEventParser.ProviderGuid, TraceEventLevel.Informational, (ulong)ClrTraceEventParser.Keywords.GC);
                }
            }
            catch
            {
                // not all systems can create this many sessions
            }
        }

        public void Dispose()
        {
            foreach (string sessionName in sessions)
            {
                TraceEventSession session = new TraceEventSession(sessionName);
                session.Dispose();
            }
        }

        [Fact]
        public void GetActiveSessionNamesWithMoreThan64()
        {
            List<string> activeSessionNames = TraceEventSession.GetActiveSessionNames();

            // This assert fails a lot because not all machines will exceed 64 visible sessions
            // One machine of mine stops at 45
            Assert.True(activeSessionNames.Count >= 65, $"Expected 65 or more sessions but had {activeSessionNames.Count}");
        }
    }
}
