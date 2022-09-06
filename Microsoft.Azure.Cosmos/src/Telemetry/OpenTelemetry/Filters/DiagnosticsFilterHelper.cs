// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Net;

    internal static class DiagnosticsFilterHelper
    {
        private static readonly TimeSpan latencyThresholdInMs = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Allow only when either of below is <b>True</b><br></br>
        /// 1) Latency is not more than 100 ms<br></br>
        /// 3) HTTP status code is not Success<br></br>
        /// </summary>
        /// <returns>true or false</returns>
        public static bool IsAllowed(
            TimeSpan latency, 
            HttpStatusCode statuscode)
        {
            return latency > DiagnosticsFilterHelper.latencyThresholdInMs || !statuscode.IsSuccess();
        }
    }
}
