// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System.Threading;
    using Microsoft.AspNetCore.Hosting;

    internal sealed class ApplicationLifetime : IApplicationLifetime
    {
        public ApplicationLifetime()
        {
            this.ApplicationStarted = CancellationToken.None;
            this.ApplicationStopping = CancellationToken.None;
            this.ApplicationStopped = CancellationToken.None;
        }

        public CancellationToken ApplicationStarted { get; }

        public CancellationToken ApplicationStopping { get; }

        public CancellationToken ApplicationStopped { get; }

        public void StopApplication()
        {
        }
    }
}
