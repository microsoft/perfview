// <copyright file="KestrelServerOptionsConfig.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.Options;

    internal sealed class KestrelServerOptionsConfig : IOptions<KestrelServerOptions>
    {
        public KestrelServerOptionsConfig(int port)
        {
            this.Value = new KestrelServerOptions
            {
                AddServerHeader = false,
                AllowSynchronousIO = false,
                ApplicationServices = null,
                ConfigurationLoader = null,
            };

            this.Value.ConfigureEndpointDefaults(options =>
            {
                options.Protocols = HttpProtocols.Http1;
            });

            this.Value.ListenAnyIP(port);
        }

        public KestrelServerOptions Value { get; }
    }
}
