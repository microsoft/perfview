// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
