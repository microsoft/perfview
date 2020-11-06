// <copyright file="DefaultEventSourceLoggerFactory.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System;
    using Microsoft.Extensions.Logging;

    internal sealed class DefaultEventSourceLoggerFactory : ILoggerFactory
    {
        private readonly ILogger defaultLogger = new DefaultEventSourceLogger();

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return this.defaultLogger;
        }

        public void AddProvider(ILoggerProvider provider)
        {
            throw new NotImplementedException();
        }

        private sealed class DefaultEventSourceLogger : ILogger
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
}
