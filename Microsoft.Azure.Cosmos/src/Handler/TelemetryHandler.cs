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
    using Microsoft.Azure.Cosmos.Tracing;

    internal class TelemetryHandler : RequestHandler
    {
        private readonly CosmosClient Client;
        
        public TelemetryHandler(CosmosClient client)
        {
            this.Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await base.SendAsync(request, cancellationToken);

            // Check if this particular operation is eligible for client telemetry collection
            if (this.IsEligibleForTelemetryCollection(request, out ClientTelemetry clientTelemetryJob))
            {
                try
                {
                    clientTelemetryJob
                        .CollectOperationInfo(
                                cosmosDiagnostics: response.Diagnostics,
                                statusCode: response.StatusCode,
                                responseSizeInBytes: this.GetPayloadSize(response),
                                containerId: request.ContainerId,
                                databaseId: request.DatabaseId,
                                operationType: request.OperationType,
                                resourceType: request.ResourceType,
                                consistencyLevel: request.Headers?[Documents.HttpConstants.HttpHeaders.ConsistencyLevel],
                                requestCharge: response.Headers.RequestCharge,
                                subStatusCode: response.Headers.SubStatusCode,
                                trace: response.Trace);
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceError("Error while collecting telemetry information : {0}", ex);
                }
            }

            return response;
        }

        /// <summary>
        /// Check if Collection should happen or not, if yes then return the client job instance where information needs to send
        /// </summary>
        /// <param name="request"></param>
        /// <param name="clientTelemetryJob"></param>
        /// <returns>true/false</returns>
        private bool IsEligibleForTelemetryCollection(RequestMessage request, out ClientTelemetry clientTelemetryJob)
        {
            return this.IsClientTelemetryJobRunning(out clientTelemetryJob) && this.IsRequestAllowed(request);
        }

        /// <summary>
        /// Check if Client Telemetry Job is running in background.
        /// </summary>
        /// <param name="clientTelemetryJob"></param>
        /// <returns>true/false</returns>
        private bool IsClientTelemetryJobRunning(out ClientTelemetry clientTelemetryJob)
        {
            clientTelemetryJob = this.Client.DocumentClient.ClientTelemetryInstance;
            if (clientTelemetryJob == null)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Check if Passed request id eligible for client telemetry collection
        /// </summary>
        /// <param name="request"></param>
        /// <returns>true/false</returns>
        private bool IsRequestAllowed(RequestMessage request)
        { 
            return ClientTelemetryOptions.AllowedResourceTypes.Equals(request.ResourceType);
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
            if (response != null)
            {
                if (response.Content != null && response.Content is MemoryStream)
                {
                    return response.Content.Length;
                }

                if (response.Headers != null && response.Headers.ContentLength != null)
                {
                    return long.Parse(response.Headers.ContentLength);
                }
            }

            return 0;
        }
    }
}
