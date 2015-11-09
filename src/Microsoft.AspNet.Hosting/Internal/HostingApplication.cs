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
    public class HostingApplication : IHttpApplication<HostingApplication.Context>
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

        public Context CreateContext(IFeatureCollection contextFeatures)
        {
            var httpContext = _httpContextFactory.Create(contextFeatures);

            var scope = _logger.RequestScope(httpContext);
            _logger.RequestStarting(httpContext);
            if (_diagnosticSource.IsEnabled("Microsoft.AspNet.Hosting.BeginRequest"))
            {
                _diagnosticSource.Write("Microsoft.AspNet.Hosting.BeginRequest", new { httpContext });
            }

            return new Context
            {
                HttpContext = httpContext,
                Scope = scope,
                TickCount = Environment.TickCount,
            };
        }

        public void DisposeContext(Context context, Exception exception = null)
        {
            var httpContext = context.HttpContext;
            var elapsed = new TimeSpan(Environment.TickCount - context.TickCount);

            if (exception == null)
            {
                if (_diagnosticSource.IsEnabled("Microsoft.AspNet.Hosting.EndRequest"))
                {
                    _diagnosticSource.Write("Microsoft.AspNet.Hosting.EndRequest", new { httpContext });
                }
                _logger.RequestFinished(httpContext, elapsed);
            }
            else
            {
                if (_diagnosticSource.IsEnabled("Microsoft.AspNet.Hosting.UnhandledException"))
                {
                    _diagnosticSource.Write("Microsoft.AspNet.Hosting.UnhandledException", new { httpContext, exception });
                }
            }

            context.Scope.Dispose();

            _httpContextFactory.Dispose(httpContext);
        }

        public async Task ProcessRequestAsync(Context context)
        {
            var httpContext = context.HttpContext;
            httpContext.ApplicationServices = _applicationServices;
            try
            {
                await _application(httpContext);
            }
            finally
            {
                httpContext.ApplicationServices = null;
            }
        }

        public struct Context
        {
            public HttpContext HttpContext;
            public IDisposable Scope;
            public int TickCount;
        }
    }
}
