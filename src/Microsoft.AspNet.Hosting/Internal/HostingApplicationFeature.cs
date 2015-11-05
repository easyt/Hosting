// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNet.Hosting.Internal
{
    public class HostingApplicationFeature
    {
        public HostingApplicationFeature(IDisposable logScope, int startTime)
        {
            if (logScope == null)
            {
                throw new ArgumentNullException(nameof(logScope));
            }

            LogScope = logScope;
            StartTime = startTime;
        }

        public IDisposable LogScope { get; }
        public int StartTime { get; }
    }
}
