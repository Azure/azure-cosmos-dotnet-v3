//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Handler;
    using Microsoft.Azure.Cosmos.Tracing;
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
        public override Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            DiagnosticsHandlerHelper.Instance.RecordCpuDiagnostics(request, DiagnosticsHandlerHelper.Diagnostickey);
            return base.SendAsync(request, cancellationToken);
        }
    }
}