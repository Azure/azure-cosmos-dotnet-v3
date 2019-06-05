//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    
    internal class CosmosOfferResult
    {
        public CosmosOfferResult(int? requestUnits)
        {
            this.RequestUnits = requestUnits;
            this.StatusCode = requestUnits.HasValue ? HttpStatusCode.OK : HttpStatusCode.NotFound;
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

        public int? RequestUnits { get; }
    }
}
