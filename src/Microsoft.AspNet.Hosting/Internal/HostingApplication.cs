// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNet.Hosting.Server;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNet.Hosting.Internal
{
    public class HostingApplication : IHttpApplication
    {
        private readonly IServiceProvider _applicationServices;
        private readonly RequestDelegate _application;
        private readonly ILogger _logger;
        private readonly DiagnosticSource _diagnosticSource;
        private readonly IHttpContextFactory _httpContextFactory;

        public HostingApplication(
            IServiceProvider applicationServices,
            RequestDelegate application)
        {
            _applicationServices = applicationServices;
            _application = application;
            _logger = _applicationServices.GetRequiredService<ILogger<HostingEngine>>();
            _diagnosticSource = _applicationServices.GetRequiredService<DiagnosticSource>();
            _httpContextFactory = _applicationServices.GetRequiredService<IHttpContextFactory>();
        }

        public HttpContext CreateHttpContext(IFeatureCollection contextFeatures)
        {
            return _httpContextFactory.Create(contextFeatures);
        }

        public void DisposeHttpContext(HttpContext httpContext)
        {
            _httpContextFactory.Dispose(httpContext);
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            httpContext.ApplicationServices = _applicationServices;

            if (_diagnosticSource.IsEnabled("Microsoft.AspNet.Hosting.BeginRequest"))
            {
                _diagnosticSource.Write("Microsoft.AspNet.Hosting.BeginRequest", new { httpContext = httpContext });
            }

            using (_logger.RequestScope(httpContext))
            {
                int startTime = 0;
                try
                {
                    _logger.RequestStarting(httpContext);

                    startTime = Environment.TickCount;
                    await _application(httpContext);

                    _logger.RequestFinished(httpContext, startTime);
                }
                catch (Exception ex)
                {
                    _logger.RequestFailed(httpContext, startTime);

                    if (_diagnosticSource.IsEnabled("Microsoft.AspNet.Hosting.UnhandledException"))
                    {
                        _diagnosticSource.Write("Microsoft.AspNet.Hosting.UnhandledException", new { httpContext = httpContext, exception = ex });
                    }
                    throw;
                }
            }
            if (_diagnosticSource.IsEnabled("Microsoft.AspNet.Hosting.EndRequest"))
            {
                _diagnosticSource.Write("Microsoft.AspNet.Hosting.EndRequest", new { httpContext = httpContext });
            }
        }
    }
}
