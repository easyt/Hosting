// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNet.Http.Features;

namespace Microsoft.AspNet.Hosting.Server
{
    /// <summary>
    /// Represents an HttpApplication.
    /// </summary>
    public interface IHttpApplication
    {
        /// <summary>
        /// Create an HttpContext given a collection of HTTP features.
        /// </summary>
        /// <param name="contextFeatures">A collection of HTTP features to be used for creating the HttpContext.</param>
        /// <returns>The created HttpContext.</returns>
        object CreateContext(IFeatureCollection contextFeatures);

        /// <summary>
        /// Asynchronously processes an HttpContext.
        /// </summary>
        /// <param name="context">The HttpContext that the operation will process.</param>
        Task ProcessRequestAsync(object context);

        /// <summary>
        /// Dispose a given HttpContext.
        /// </summary>
        /// <param name="context">The HttpContext to be disposed.</param>
        void DisposeContext(object context);
        
        /// <summary>
        /// Dispose a given HttpContext.
        /// </summary>
        /// <param name="context">The HttpContext to be disposed.</param>
        /// <param name="exception">The Exception thrown when processing did not complete successfully, otherwise null.</param>
        void DisposeContext(object context, Exception exception);
    }
}
