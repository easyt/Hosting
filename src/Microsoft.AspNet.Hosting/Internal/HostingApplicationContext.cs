// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNet.Http;

namespace Microsoft.AspNet.Hosting.Internal
{
    public class HostingApplicationContext
    {
        public HttpContext HttpContext { get; set; }
        public IDisposable Scope { get; set; }
        public int StartTick { get; set; }
    }
}
