//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Newtonsoft.Json.Linq;

    internal class TelemetryHandler : RequestHandler
    {
        private readonly ClientTelemetry telemetry;

        public TelemetryHandler(ClientTelemetry telemetry)
        {
            this.telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            telemetry.Start();
        }

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await base.SendAsync(request, cancellationToken);
            if (this.IsAllowed(request))
            {
                this.telemetry
                    .Collect(
                          cosmosDiagnostics: response.Diagnostics,
                          statusCode: response.StatusCode,
                          responseSizeInBytes: this.GetPayloadSize(response),
                          containerId: request.ContainerId,
                          databaseId: request.DatabaseId,
                          operationType: request.OperationType,
                          resourceType: request.ResourceType,
                          consistencyLevel: this.GetConsistencyLevel(request),
                          requestCharge: request.Headers.RequestCharge);
            }
            return response;
        }

        private bool IsAllowed(RequestMessage request)
        { 
            return ClientTelemetryOptions.AllowedResourceTypes.Contains(request.ResourceType);
        }

        private ConsistencyLevel? GetConsistencyLevel(RequestMessage request)
        {
            return request.RequestOptions?.BaseConsistencyLevel.GetValueOrDefault();   
        }

        private int GetPayloadSize(ResponseMessage response)
        {
            return (int)(response.Content == null ? 0 : response.Content.Length);
        }
    }
}
