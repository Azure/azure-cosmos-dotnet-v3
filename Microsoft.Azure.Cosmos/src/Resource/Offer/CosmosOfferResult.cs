//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Net;

    internal sealed class CosmosOfferResult
    {
        public CosmosOfferResult(int? throughput)
        {
            this.Throughput = throughput;
            this.StatusCode = throughput.HasValue ? HttpStatusCode.OK : HttpStatusCode.NotFound;
        }

        public CosmosOfferResult(
            HttpStatusCode statusCode,
            CosmosException cosmosRequestException)
        {
            this.StatusCode = statusCode;
            this.CosmosException = cosmosRequestException;
        }

        public CosmosException CosmosException { get; }

        public HttpStatusCode StatusCode { get; }

        public int? Throughput { get; }
    }
}
