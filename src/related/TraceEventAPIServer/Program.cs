namespace TraceEventAPIServer
{
    using System.IO;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Hosting.Server.Features;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    public sealed class Program
    {
        private static IServerAddressesFeature ServerAddressesFeature;

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
            services.AddSingleton(ServerAddressesFeature);
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
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Program>()
                .Build();

            ServerAddressesFeature = host.ServerFeatures.Get<IServerAddressesFeature>();

            host.Run();
        }
    }
}