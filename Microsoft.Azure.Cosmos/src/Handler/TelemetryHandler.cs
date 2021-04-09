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
    using Newtonsoft.Json.Linq;

    internal class TelemetryHandler : RequestHandler
    {
        private readonly CosmosClient client;

        public TelemetryHandler(CosmosClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await base.SendAsync(request, cancellationToken);
            if (this.isTelemetryEnabled(request))
            {
                this.client.DocumentClient.clientTelemetry.Collect(
                  response.Diagnostics, response.StatusCode,
                  this.GetPayloadSize(response),
                  request.TelemetryInfo?.ContainerId,
                  request.TelemetryInfo?.DatabaseId,
                  request.OperationType,
                  request.ResourceType,
                  this.GetConsistencyLevel(request),
                  request.Headers.RequestCharge);
            }
            return response;
        }

        private bool isTelemetryEnabled(RequestMessage request)
        {
            return ConfigurationManager
                .GetEnvironmentVariableInBoolean(ClientTelemetry.EnvPropsClientTelemetryEnabled, 
                this.client
                .ClientOptions
                .EnableClientTelemetry && request.TelemetryInfo != null);
        }

        private ConsistencyLevel? GetConsistencyLevel(RequestMessage request)
        {
            ConsistencyLevel? defaultConsistencyLevel = request.RequestOptions?.BaseConsistencyLevel.GetValueOrDefault();
            if (defaultConsistencyLevel == null)
                return this.client.ClientOptions.ConsistencyLevel.GetValueOrDefault();
            return defaultConsistencyLevel;     
        }

        private int GetPayloadSize(ResponseMessage response)
        {
            return (int)(response.Content == null ? 0 : response.Content.Length);
        }
    }
}