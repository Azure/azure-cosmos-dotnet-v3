//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;

    public class PreProcessingTestHandler : CosmosRequestHandler
    {
        internal const string StatusCodeName = "x-test-requesting-statuscode";

        public override Task<CosmosResponseMessage> SendAsync(CosmosRequestMessage request, CancellationToken cancellation)
        {
            CosmosResponseMessage httpResponse = null;
            if (request.Properties.TryGetValue(PreProcessingTestHandler.StatusCodeName, out object statusCodeOut))
            {
                httpResponse = new CosmosResponseMessage((HttpStatusCode)statusCodeOut);
            }

            return Task.FromResult(httpResponse);
        }
    }
}
