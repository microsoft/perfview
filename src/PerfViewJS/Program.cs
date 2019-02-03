// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
    using Microsoft.QuickInject;

    public static class Program
    {
        public static async Task Main(string[] args)
        {
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Console.WriteLine("Unobserved exception: {0}", e.Exception);
            };

            var container = new QuickInjectContainer();
            var hostingEnvironment = new HostingEnvironment
            {
                ContentRootPath = Directory.GetCurrentDirectory()
            };

            var defaultEventSourceLoggerFactory = new DefaultEventSourceLoggerFactory();
            var startup = new Startup();

            startup.SetupQuickInjectContainer(container);
            startup.Configure(null, hostingEnvironment);

            var requestDelegate = new RequestDelegate(startup.HandleRequest);

            var server = new KestrelServer(new KestrelServerOptionsConfig(container, 5000), new SocketTransportFactory(new SocketTransportOptionsConfig(), new ApplicationLifetime(), defaultEventSourceLoggerFactory), defaultEventSourceLoggerFactory);

            await server.StartAsync(new HttpApplication(requestDelegate), CancellationToken.None);

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
