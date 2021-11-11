//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// Handler which add process level info like CPU usage to the
    /// diagnostics. This is a best effort scenario. It will not
    /// add or attempt to add it if an exception occurs to avoid
    /// impacting users.
    /// </summary>
    internal class DiagnosticsHandler : RequestHandler
    {
        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            ResponseMessage responseMessage = await base.SendAsync(request, cancellationToken);

            // Record the diagnostics on the response to get the CPU of when the request was executing
            SystemUsageHistory systemUsageHistory = DiagnosticsHandlerHelper.Instance.GetDiagnosticsSystemHistory();
            if (systemUsageHistory != null)
            {
                request.Trace.AddDatum(
                    "System Info",
                    new CpuHistoryTraceDatum(systemUsageHistory));
            }

            return responseMessage;
        }
    }
}