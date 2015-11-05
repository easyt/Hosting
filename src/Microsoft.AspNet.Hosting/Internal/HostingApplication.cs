// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNet.Hosting.Server;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;
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
            RequestDelegate application,
            ILogger logger,
            DiagnosticSource diagnosticSource,
            IHttpContextFactory httpContextFactory)
        {
            _applicationServices = applicationServices;
            _application = application;
            _logger = logger;
            _diagnosticSource = diagnosticSource;
            _httpContextFactory = httpContextFactory;
        }

        public object CreateHttpContext(IFeatureCollection contextFeatures)
        {
            var httpContext = _httpContextFactory.Create(contextFeatures);
            httpContext.ApplicationServices = _applicationServices;

            if (_diagnosticSource.IsEnabled("Microsoft.AspNet.Hosting.BeginRequest"))
            {
                _diagnosticSource.Write("Microsoft.AspNet.Hosting.BeginRequest", new { httpContext = httpContext });
            }
            
            httpContext.Features.Set(new HostingApplicationFeature(_logger.RequestScope(httpContext), Environment.TickCount));
            _logger.RequestStarting(httpContext);

            return httpContext;
        }

        public void DisposeHttpContext(object httpContext)
        {
            var context = httpContext as HttpContext;
            var hostingApplicationFeature = context.Features.Get<HostingApplicationFeature>();

            _logger.RequestFinished(context, hostingApplicationFeature.StartTime);

            if (hostingApplicationFeature.LogScope != null)
            {
                hostingApplicationFeature.LogScope.Dispose();
            }

            if (_diagnosticSource.IsEnabled("Microsoft.AspNet.Hosting.EndRequest"))
            {
                _diagnosticSource.Write("Microsoft.AspNet.Hosting.EndRequest", new { httpContext = context });
            }

            _httpContextFactory.Dispose(context);
        }

        public async Task InvokeAsync(object httpContext)
        {
            var context = httpContext as HttpContext;
            try
            {
                await _application(context);
            }
            catch (Exception ex)
            {
                if (_diagnosticSource.IsEnabled("Microsoft.AspNet.Hosting.UnhandledException"))
                {
                    _diagnosticSource.Write("Microsoft.AspNet.Hosting.UnhandledException", new { httpContext = context, exception = ex });
                }
                throw;
            }
        }
    }
}
