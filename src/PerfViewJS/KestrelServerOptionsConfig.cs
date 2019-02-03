// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.AspNetCore.Server.Kestrel.Transport.Abstractions.Internal;
    using Microsoft.Extensions.Options;

    internal sealed class KestrelServerOptionsConfig : IOptions<KestrelServerOptions>
    {
        public KestrelServerOptionsConfig(IServiceProvider serviceProvider, int port)
        {
            this.Value = new KestrelServerOptions
            {
                AddServerHeader = false,
                AllowSynchronousIO = false,
                ApplicationServices = serviceProvider,
                ApplicationSchedulingMode = SchedulingMode.Default,
                ConfigurationLoader = null
            };

            this.Value.ConfigureEndpointDefaults(options =>
            {
                options.Protocols = HttpProtocols.Http1;
                options.NoDelay = true;
            });

            this.Value.ListenAnyIP(port);
        }

        public KestrelServerOptions Value { get; }
    }
}
