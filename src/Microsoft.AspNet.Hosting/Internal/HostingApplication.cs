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
        private IDisposable _logScope;

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

        public object CreateHttpContext(object contextFeatures)
        {
            var httpContext = _httpContextFactory.Create((IFeatureCollection)contextFeatures);
            
            httpContext.ApplicationServices = _applicationServices;

            if (_diagnosticSource.IsEnabled("Microsoft.AspNet.Hosting.BeginRequest"))
            {
                _diagnosticSource.Write("Microsoft.AspNet.Hosting.BeginRequest", new { httpContext = httpContext });
            }

            _logScope = _logger.RequestScope(httpContext);

            return httpContext;
        }

        public void DisposeHttpContext(object httpContext)
        {
            if (_logScope != null)
            {
                _logScope.Dispose();
            }

            _httpContextFactory.Dispose((HttpContext)httpContext);
        }

        public async Task InvokeAsync(object httpContext)
        {
            int startTime = 0;
            try
            {
                _logger.RequestStarting((HttpContext)httpContext);

                startTime = Environment.TickCount;
                await _application((HttpContext)httpContext);

                _logger.RequestFinished((HttpContext)httpContext, startTime);
            }
            catch (Exception ex)
            {
                if (_diagnosticSource.IsEnabled("Microsoft.AspNet.Hosting.UnhandledException"))
                {
                    _diagnosticSource.Write("Microsoft.AspNet.Hosting.UnhandledException", new { httpContext = httpContext, exception = ex });
                }
                throw;
            }
            if (_diagnosticSource.IsEnabled("Microsoft.AspNet.Hosting.EndRequest"))
            {
                _diagnosticSource.Write("Microsoft.AspNet.Hosting.EndRequest", new { httpContext = (HttpContext)httpContext });
            }
        }
    }
}
