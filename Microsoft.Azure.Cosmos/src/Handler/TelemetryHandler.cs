//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Telemetry;

    internal class TelemetryHandler : RequestHandler
    {
        private readonly ClientTelemetry telemetry;

        public TelemetryHandler(ClientTelemetry telemetry)
        {
            this.telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        }

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await base.SendAsync(request, cancellationToken);
            if (this.IsAllowed(request))
            {
                try
                {
                  /*  this.telemetry
                        .Collect(
                                cosmosDiagnostics: response.Diagnostics,
                                statusCode: response.StatusCode,
                                responseSizeInBytes: this.GetPayloadSize(response),
                                containerId: request.ContainerId,
                                databaseId: request.DatabaseId,
                                operationType: request.OperationType,
                                resourceType: request.ResourceType,
                                consistencyLevel: request.Headers?[Documents.HttpConstants.HttpHeaders.ConsistencyLevel],
                                requestCharge: response.Headers.RequestCharge);*/
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceError("Error while collecting telemetry information : " + ex.Message);
                }
            }
            return response;
        }

        private bool IsAllowed(RequestMessage request)
        { 
            return ClientTelemetryOptions.AllowedResourceTypes.Equals(request.ResourceType);
        }
    }
}
