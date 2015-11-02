// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNet.Hosting.Server
{
    /// <summary>
    /// Represents a server.
    /// </summary>
    public interface IServer : IDisposable
    {
        /// <summary>
        /// Start the server with an HttpApplication.
        /// </summary>
        /// <param name="app">An instance of <see cref="IHttpApplication"/>.</param>
        void Start(IHttpApplication app);
    }
}
