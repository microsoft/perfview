// <copyright file="Program.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;

    public static class Program
    {
        public static async Task Main(string[] args)
        {
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Console.WriteLine("Unobserved exception: {0}", e.Exception);
            };

            if (args.Length != 2)
            {
                Console.WriteLine("Usage: PerfViewJS portNumber DataRoot");
                return;
            }

            string defaultAuthorizationHeaderForSourceLink = Environment.GetEnvironmentVariable("PerfViewJS_DefaultAuthorizationHeaderForSourceLink");

            var defaultEventSourceLoggerFactory = new DefaultEventSourceLoggerFactory();
            var startup = new Startup(Directory.GetCurrentDirectory(), args[1], defaultAuthorizationHeaderForSourceLink);

            var server = new KestrelServer(new KestrelServerOptionsConfig(int.Parse(args[0])), new SocketTransportFactory(new SocketTransportOptionsConfig(), defaultEventSourceLoggerFactory), defaultEventSourceLoggerFactory);

            await server.StartAsync(new HttpApplication(startup.HandleRequest), CancellationToken.None);

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
