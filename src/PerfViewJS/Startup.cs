// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Diagnostics.Symbols;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Options;
    using Microsoft.QuickInject;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public sealed class Startup
    {
        private readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };

        private QuickInjectContainer dependencyInjectionContainer;

        private string contentRoot;

        private string indexFile;

        public void SetupQuickInjectContainer(QuickInjectContainer container)
        {
            this.dependencyInjectionContainer = container;
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            this.contentRoot = Path.Combine(env.ContentRootPath, "spa", "build");
            this.indexFile = Path.GetFullPath(Path.Join(this.contentRoot, "index.html"));

            var container = this.dependencyInjectionContainer;

            var configuration = new ConfigurationBuilder().SetBasePath(env.ContentRootPath).AddJsonFile("appsettings.json", optional: false, reloadOnChange: false).Build();
            container.RegisterInstance<IConfiguration>(configuration);

            var symbolReader = new SymbolReader(new EventSourceTextWriter());

            container.RegisterInstance(symbolReader);
            container.RegisterTypeAsResolutionContext<HttpContext>();

            container.RegisterType<IDeserializedDataCache, DeserializedDataCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<TextWriter, EventSourceTextWriter>(new ContainerControlledLifetimeManager());
            container.RegisterType<CallTreeDataCache, CallTreeDataCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<ICacheExpirationTimeProvider, CacheExpirationTimeProvider>(new ContainerControlledLifetimeManager());

            container.RegisterType<StackViewerModel, StackViewerModel>();
            container.RegisterType<ICallTreeData, CallTreeData>();
        }

        public async Task HandleRequest(HttpContext context)
        {
            string requestPath = context.Request.Path.Value;

            if (requestPath.StartsWith("/api"))
            {
                if (requestPath.StartsWith(@"/api/eventdata"))
                {
                    var controller = this.dependencyInjectionContainer.Resolve<EventViewerController>(context);
                    await WriteJsonResponse(context, this.jsonSerializerSettings, await controller.EventsAPI());
                }
                else
                {
                    var controller = this.dependencyInjectionContainer.Resolve<StackViewerController>(context);
                    context.Response.ContentType = "application/json; charset=UTF-8";

                    if (requestPath.StartsWith(@"/api/callerchildren"))
                    {
                        var name = (string)context.Request.Query["name"] ?? string.Empty;
                        var path = (string)context.Request.Query["path"] ?? string.Empty;

                        await WriteJsonResponse(context, this.jsonSerializerSettings, await controller.CallerChildrenAPI(name, path));
                    }
                    else if (requestPath.StartsWith(@"/api/treenode"))
                    {
                        var name = (string)context.Request.Query["name"] ?? string.Empty;
                        await WriteJsonResponse(context, this.jsonSerializerSettings, await controller.TreeNodeAPI(name));
                    }
                    else if (requestPath.StartsWith(@"/api/hotspots"))
                    {
                        await WriteJsonResponse(context, this.jsonSerializerSettings, await controller.HotspotsAPI());
                    }
                    else if (requestPath.StartsWith(@"/api/eventlist"))
                    {
                        await WriteJsonResponse(context, this.jsonSerializerSettings, await controller.EventListAPI());
                    }
                    else if (requestPath.StartsWith(@"/api/processlist"))
                    {
                        await WriteJsonResponse(context, this.jsonSerializerSettings, await controller.ProcessListAPI());
                    }
                    else if (requestPath.StartsWith(@"/api/drillinto"))
                    {
                        bool exclusive = requestPath.StartsWith(@"/api/drillinto/exclusive");

                        var name = (string)context.Request.Query["name"] ?? string.Empty;
                        var path = (string)context.Request.Query["path"] ?? string.Empty;

                        await WriteJsonResponse(context, this.jsonSerializerSettings, await controller.DrillIntoAPI(exclusive, name, path));
                    }
                    else if (requestPath.StartsWith(@"/api/lookupwarmsymbols"))
                    {
                        await WriteJsonResponse(context, this.jsonSerializerSettings, await controller.LookupWarmSymbolsAPI(1000));
                    }
                }
            }
            else if (requestPath.StartsWith("/ui"))
            {
                await SendIndexFile(context, this.indexFile);
            }
            else
            {
                var fullPath = Path.GetFullPath(Path.Join(this.contentRoot.AsSpan(), requestPath.AsSpan(1)));
                if (fullPath.StartsWith(this.contentRoot) && File.Exists(fullPath))
                {
                    var ext = Path.GetExtension(fullPath);
                    if (ext.EndsWith("js"))
                    {
                        context.Response.ContentType = "application/javascript; charset=UTF-8";
                    }
                    else if (ext.EndsWith("css"))
                    {
                        context.Response.ContentType = "text/css; charset=UTF-8";
                    }
                    else if (ext.EndsWith("html"))
                    {
                        context.Response.ContentType = "text/html; charset=UTF-8";
                    }

                    await context.Response.SendFileAsync(fullPath);
                }
                else
                {
                    if (requestPath.Equals("/"))
                    {
                        await SendIndexFile(context, this.indexFile);
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsync("404 Not Found");
                    }
                }
            }
        }

        private static async Task SendIndexFile(HttpContext context, string indexFile)
        {
            context.Response.ContentType = "text/html; charset=UTF-8";
            await context.Response.SendFileAsync(indexFile);
        }

        private static async Task WriteJsonResponse(HttpContext context, JsonSerializerSettings settings, object data)
        {
            await context.Response.WriteAsync(JsonConvert.SerializeObject(data, settings));
        }
    }
}
