using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;

namespace PerfDataService
{
    using Microsoft.AspNetCore.Hosting.Server.Features;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    public class Program
    {
        //private static IServerAddressesFeature ServerAddressesFeature;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddMemoryCache();
            services.AddTransient<ICallTreeDataProvider, CallTreeDataProvider>();
            services.AddTransient<ICallTreeDataProviderFactory, CallTreeDataProviderFactory>();
            services.AddSingleton<EtlxCache, EtlxCache>();
            services.AddSingleton<StackViewerSessionCache, StackViewerSessionCache>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<ITemporaryPathProvider, TemporaryPathProvider>();
            services.AddSingleton<ICacheExpirationTimeProvider, CacheExpirationTimeProvider>();
            services.AddSingleton<TextWriter, EventSourceTextWriter>();
            //services.AddSingleton(ServerAddressesFeature);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseStaticFiles();
            app.UseMvc();
        }

        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseUrls("http://localhost:5000")
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
