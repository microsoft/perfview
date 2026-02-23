// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace PerfView.TestUtilities
{
    // This listener converts Debug.Assert/Trace.Assert failures into xUnit test failures
    // by throwing from the Fail() method.
    //
    // On .NET Framework (net462), this must be registered via app.config <system.diagnostics>:
    //
    // <configuration>
    //  <system.diagnostics>
    //    <trace>
    //      <listeners>
    //        <remove name="Default" />
    //        <add name="ThrowingTraceListener" type="PerfView.TestUtilities.ThrowingTraceListener, PerfView.TestUtilities" />
    //      </listeners>
    //    </trace>
    //  </system.diagnostics>
    //</configuration>
    //
    // On .NET 5+, the app.config <system.diagnostics> section is NOT
    // processed, so this listener is never registered. However, the DefaultTraceListener
    // on .NET 5+ already throws on assert failures, so no additional configuration is
    // needed — Debug.Assert and Trace.Assert will throw without this listener.
    // Should this behavior change, the ThrowingTraceListener tests will fail, which will tell us we
    // need to do something to re-enable this listener.
    public sealed class ThrowingTraceListener : TraceListener
    {
        public override void Fail(string message, string detailMessage)
        {
            Xunit.Assert.Fail(message + Environment.NewLine + detailMessage);
            throw new DebugAssertFailureException(message + Environment.NewLine + detailMessage);
        }

        public override void Write(object o)
        {
            if (Debugger.IsLogging())
            {
                Debugger.Log(0, null, o?.ToString());
            }
        }

        public override void Write(object o, string category)
        {
            if (Debugger.IsLogging())
            {
                Debugger.Log(0, category, o?.ToString());
            }
        }

        public override void Write(string message)
        {
            if (Debugger.IsLogging())
            {
                Debugger.Log(0, null, message);
            }
        }

        public override void Write(string message, string category)
        {
            if (Debugger.IsLogging())
            {
                Debugger.Log(0, category, message);
            }
        }

        public override void WriteLine(object o)
        {
            if (Debugger.IsLogging())
            {
                Debugger.Log(0, null, o?.ToString() + Environment.NewLine);
            }
        }

        public override void WriteLine(object o, string category)
        {
            if (Debugger.IsLogging())
            {
                Debugger.Log(0, category, o?.ToString() + Environment.NewLine);
            }
        }

        public override void WriteLine(string message)
        {
            if (Debugger.IsLogging())
            {
                Debugger.Log(0, null, message + Environment.NewLine);
            }
        }

        public override void WriteLine(string message, string category)
        {
            if (Debugger.IsLogging())
            {
                Debugger.Log(0, category, message + Environment.NewLine);
            }
        }

        [Serializable]
        public class DebugAssertFailureException : Exception
        {
            public DebugAssertFailureException() { }
            public DebugAssertFailureException(string message) : base(message) { }
            public DebugAssertFailureException(string message, Exception inner) : base(message, inner) { }
        }
    }
}
