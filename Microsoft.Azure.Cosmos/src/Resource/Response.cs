//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;

    /// <summary>
    /// The cosmos resource response class
    /// </summary>
    public abstract class Response<T>
    {
        /// <summary>
        /// Gets the current <see cref="ResponseMessage"/> HTTP headers.
        /// </summary>
        public abstract Headers Headers { get; }

        /// <summary>
        /// The content of the response.
        /// </summary>
        public abstract T Resource { get; }

        /// <summary>
        /// Get Resource implicitly from <see cref="Response{T}"/>
        /// </summary>
        /// <param name="response">The Azure Cosmos DB service response.</param>
        public static implicit operator T(Response<T> response)
        {
            return response.Resource;
        }

        /// <summary>
        /// Gets the request completion status code from the Azure Cosmos DB service.
        /// This can be used to in scenario like CreateDatabaseIfNotExists to help determine if the database was created or already existed.
        /// </summary>
        /// <remarks>This is only used for success scenarios. The operation will throw a CosmosException on failures. For more info: https://aka.ms/cosmosdb-dot-net-exceptions</remarks>
        /// <value>The request completion status code</value>
        public abstract HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in request units.
        /// </value>
        public abstract double RequestCharge { get; }

        /// <summary>
        /// Gets the activity ID for the request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        public abstract string ActivityId { get; }

        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources. 
        /// </remarks>
        public abstract string ETag { get; }

        /// <summary>
        /// Gets the cosmos diagnostics information for the current request to Azure Cosmos DB service
        /// </summary>
        public abstract CosmosDiagnostics Diagnostics { get; }
    }
}