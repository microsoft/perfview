// <copyright file="SocketTransportOptionsConfig.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
    using Microsoft.Extensions.Options;

    internal sealed class SocketTransportOptionsConfig : IOptions<SocketTransportOptions>
    {
        public SocketTransportOptionsConfig()
        {
            this.Value = new SocketTransportOptions();
        }

        public SocketTransportOptions Value { get; }
    }
}
