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
        private readonly CosmosClient cosmosClient;

        private static string AccountLevelConsistency;

        public TelemetryHandler(CosmosClient client, ClientTelemetry telemetry)
        {
            this.telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            this.cosmosClient = client ?? throw new ArgumentNullException(nameof(client));
        }

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await base.SendAsync(request, cancellationToken);
            if (request.IsTelemetryAllowed())
            {
                try
                {
                    this.telemetry
                        .Collect(
                                cosmosDiagnostics: response.Diagnostics,
                                statusCode: response.StatusCode,
                                responseSizeInBytes: TelemetryHandler.GetPayloadSize(response),
                                containerId: request.ContainerId,
                                databaseId: request.DatabaseId,
                                operationType: request.OperationType,
                                resourceType: request.ResourceType,
                                consistencyLevel: await TelemetryHandler.GetConsistencyLevelAsync(this.cosmosClient, request.ConsistencyLevel),
                                requestCharge: response.Headers.RequestCharge);
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceError("Error while collecting telemetry information : " + ex.Message);
                }
            }
            return response;
        }

        /// <summary>
        /// Get Consistency level from header (if available) otherwise account level
        /// </summary>
        /// <param name="client"></param>
        /// <param name="requestConsistencyLevel"></param>
        /// <returns>Consistency level</returns>
        private static async Task<string> GetConsistencyLevelAsync(CosmosClient client, string requestConsistencyLevel)
        {
            // Send whatever set to request header
            if (requestConsistencyLevel == null)
            {
                if (TelemetryHandler.AccountLevelConsistency == null)
                {
                    TelemetryHandler.AccountLevelConsistency = (await client.GetAccountConsistencyLevelAsync()).ToString();
                }
                return TelemetryHandler.AccountLevelConsistency;
            }
            return requestConsistencyLevel;
        }

        /// <summary>
        /// It returns the payload size after reading it from the Response content stream. 
        /// To avoid blocking IO calls to get the stream length, it will return response content length if stream is of Memory Type
        /// otherwise it will return the content length from the response header (if it is there)
        /// </summary>
        /// <param name="response"></param>
        /// <returns>Size of Payload</returns>
        private static long GetPayloadSize(ResponseMessage response)
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
