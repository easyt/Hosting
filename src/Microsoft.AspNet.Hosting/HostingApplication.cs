// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNet.Hosting.Server;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;

namespace Microsoft.AspNet.Hosting
{
    public class HostingApplication : IHttpApplication
    {
        public HttpContext CreateHttpContext(IFeatureCollection contextFeatures)
        {
            throw new NotImplementedException();
        }

        public void DisposeHttpContext(HttpContext httpContext)
        {
            throw new NotImplementedException();
        }

        public Task InvokeAsync(HttpContext httpContext)
        {
            throw new NotImplementedException();
        }
    }
}
