// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    }
}
