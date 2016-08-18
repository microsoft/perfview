using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;

namespace PerfDataService
{

    public class Program
    {

        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseUrls(AppSettings.targetUrl)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }

    internal static class AppSettings
    {
        public static readonly String targetHost = "http://localhost";
        public static readonly int port = 5000;
        public static readonly String targetUrl = targetHost + ":" + port.ToString();
    }
}
