//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Runtime.InteropServices;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// While ServicePoint is a NETStandard 2.0 API, not all runtimes support the operations and some Framework implementations might not support it.
    /// </summary>
    internal class ServicePointAccessor
    {
        // WebAssembly detection
        private static bool IsBrowser = RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER"));

        private readonly ServicePoint servicePoint;

        private ServicePointAccessor(ServicePoint servicePoint)
        {
            this.servicePoint = servicePoint ?? throw new ArgumentNullException(nameof(servicePoint));
        }

        internal static ServicePointAccessor FindServicePoint(Uri endpoint)
        {
            return new ServicePointAccessor(ServicePointManager.FindServicePoint(endpoint));
        }

        public int ConnectionLimit
        {
            get => this.servicePoint.ConnectionLimit;
            set => this.TrySetConnectionLimit(value);
        }

        private void TrySetConnectionLimit(int connectionLimit)
        {
            if (ServicePointAccessor.IsBrowser)
            {
                return; // Not supported
            }

            try
            {
                this.servicePoint.ConnectionLimit = connectionLimit;
            }
            catch (PlatformNotSupportedException)
            {
                DefaultTrace.TraceWarning("ServicePoint.set_ConnectionLimit - Platform does not support feature.");
            }
        }
    }
}
