//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Net;

    internal class CosmosOfferResult
    {
        public CosmosOfferResult(int? requestUnitsPerSecond)
        {
            this.RequestUnitsPerSecond = requestUnitsPerSecond;
            this.StatusCode = requestUnitsPerSecond.HasValue ? HttpStatusCode.OK : HttpStatusCode.NotFound;
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

        public int? RequestUnitsPerSecond { get; }
    }
}
