//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
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
                        requestCharge: response.Headers.RequestCharge);
               
            }
            return response;
        }

        private bool IsAllowed(RequestMessage request)
        { 
            return ClientTelemetryOptions.AllowedResourceTypes.Equals(request.ResourceType);
        }

        private ConsistencyLevel? GetConsistencyLevel(RequestMessage request)
        {
            return request.RequestOptions?.BaseConsistencyLevel.GetValueOrDefault();   
        }

        /// <summary>
        /// It returns the payload size after reading it from the Response content stream. 
        /// To avoid blocking IO calls to get the stream length, it will return response content length if stream is of Memory Type
        /// otherwise it will return the content length from the response header (if it is there)
        /// </summary>
        /// <param name="response"></param>
        /// <returns>Size of Payload</returns>
        private long GetPayloadSize(ResponseMessage response)
        {
            if (response.Content == null)
            {
                return 0;
            }

            if (response.Content is MemoryStream)
            {
                return response.Content.Length;
            }

            return long.Parse(response.Headers.ContentLength);
        }
    }
}
