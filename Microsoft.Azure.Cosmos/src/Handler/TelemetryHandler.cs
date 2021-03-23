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
          /*  if (this.client.ClientOptions.EnableClientTelemetry)
            {
              this.client.DocumentClient.clientTelemetry.Collect(
              this.client,
              response.Diagnostics,
              response.StatusCode,
              Marshal.SizeOf(response),
              null,
              null,
              request.OperationType,
              request.ResourceType,
              this.client.DocumentClient.ConsistencyLevel,
              request.Headers.RequestCharge);
            }*/
           
            return response;
            
        }

    }
}