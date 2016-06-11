namespace TraceEventAPIServer
{
    using System.IO;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Formatters;
    using Microsoft.Extensions.DependencyInjection;

    public sealed class Program
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(SetupAction);
            services.AddMemoryCache();
            services.AddTransient<ICallTreeDataProvider, CallTreeDataProvider>();
            services.AddTransient<ICallTreeDataProviderFactory, CallTreeDataProviderFactory>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<ITemporaryPathProvider, TemporaryPathProvider>();
            services.AddSingleton<ICacheExpirationTimeProvider, CacheExpirationTimeProvider>();
            services.AddSingleton<TextWriter, EventSourceTextWriter>();
        }

        private void SetupAction(MvcOptions mvcOptions)
        {
            mvcOptions.InputFormatters.Clear();
            mvcOptions.OutputFormatters.Clear();
            mvcOptions.OutputFormatters.Add(new JsonOutputFormatter());
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc();
        }

        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseStartup<Program>()
                .Build();

            host.Run();
        }
    }
}