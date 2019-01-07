// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System;
    using Microsoft.Extensions.Logging;

    internal sealed class DefaultEventSourceLogger : ILogger
    {
        private readonly FakeDisposable fakeDisposable = new FakeDisposable();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return this.fakeDisposable;
        }

        private sealed class FakeDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
