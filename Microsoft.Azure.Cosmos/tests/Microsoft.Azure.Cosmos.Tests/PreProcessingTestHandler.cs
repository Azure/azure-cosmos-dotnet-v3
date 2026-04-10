//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;

    public class PreProcessingTestHandler : RequestHandler
    {
        internal const string StatusCodeName = "x-test-requesting-statuscode";

        public override Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            ResponseMessage httpResponse = null;
            if (request.Properties.TryGetValue(PreProcessingTestHandler.StatusCodeName, out object statusCodeOut))
            {
                httpResponse = new ResponseMessage((HttpStatusCode)statusCodeOut);
            }

            return Task.FromResult(httpResponse);
        }
    }
}