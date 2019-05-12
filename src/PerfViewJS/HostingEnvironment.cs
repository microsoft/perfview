// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.FileProviders;

    internal sealed class HostingEnvironment : IHostingEnvironment
    {
        public string EnvironmentName
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public string ApplicationName
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public string WebRootPath
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public IFileProvider WebRootFileProvider
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
    }
}
