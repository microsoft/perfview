// <copyright file="HttpApplication.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Hosting.Server;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;

    internal sealed class HttpApplication : IHttpApplication<HttpContext>
    {
        private readonly RequestDelegate application;

        public HttpApplication(RequestDelegate application) => this.application = application;

        public HttpContext CreateContext(IFeatureCollection contextFeatures) => new DefaultHttpContext(contextFeatures);

        public Task ProcessRequestAsync(HttpContext context) => this.application(context);

        public void DisposeContext(HttpContext context, Exception exception)
        {
        }
    }
}
