//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// Handler which adds process level info like CPU usage to the
    /// diagnostics. This is a best effort scenario. It will not
    /// impact the request if an exception occurs while capturing the
    /// information. The system information is captured for both the
    /// successful and the failed request paths so that the diagnostics
    /// of failed operations (for example timeouts and cancellations) also
    /// contain CPU/memory usage, which is exactly when the data is most
    /// useful.
    /// </summary>
    internal class DiagnosticsHandler : RequestHandler
    {
        private const string SystemInfoKey = "System Info";

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                return await base.SendAsync(request, cancellationToken);
            }
            finally
            {
                // Record the diagnostics to capture the CPU of when the request was executing.
                // This is done in a finally block so the system information is attached on both
                // the success and the failure path. Operations that fail by exception (timeouts,
                // cancellations, exhausted retries) are exactly when CPU/memory usage matters most.
                DiagnosticsHandler.TryAddSystemInfoToTrace(request);
            }
        }

        /// <summary>
        /// Adds the process level system usage to the request trace. Capturing diagnostics is a
        /// best effort scenario and must never impact the request, so any failure here is swallowed.
        /// </summary>
        private static void TryAddSystemInfoToTrace(RequestMessage request)
        {
            if (request?.Trace == null)
            {
                return;
            }

            try
            {
                SystemUsageHistory systemUsageHistory = DiagnosticsHandlerHelper.GetInstance().GetDiagnosticsSystemHistory();

                if (systemUsageHistory != null)
                {
                    request.Trace.AddDatum(
                        DiagnosticsHandler.SystemInfoKey,
                        new CpuHistoryTraceDatum(systemUsageHistory));
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Failed to add '{0}' to the diagnostics. {1}", DiagnosticsHandler.SystemInfoKey, ex.Message);
            }
        }
    }
}
