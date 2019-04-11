//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal abstract class CosmosClientContext
    {
        /// <summary>
        /// The Cosmos client that is used for the request
        /// </summary>
        internal abstract CosmosClient Client { get; }

        internal abstract DocumentClient DocumentClient { get; }

        internal abstract CosmosJsonSerializer JsonSerializer { get; }

        internal abstract CosmosResponseFactory ResponseFactory { get; }

        internal abstract CosmosRequestHandler RequestHandler { get; }

        /// <summary>
        /// Generates the URI link for the resource
        /// </summary>
        /// <param name="parentLink">The parent link URI (/dbs/mydbId) </param>
        /// <param name="uriPathSegment">The URI path segment</param>
        /// <param name="id">The id of the resource</param>
        /// <returns>A resource link in the format of {parentLink}/this.UriPathSegment/this.Name with this.Name being a Uri escaped version</returns>
        internal abstract Uri CreateLink(
            string parentLink,
            string uriPathSegment,
            string id);

        internal abstract void ValidateResource(string resourceId);

        internal abstract Task<CosmosResponseMessage> ProcessResourceOperationStreamAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            Object partitionKey,
            Stream streamPayload,
            Action<CosmosRequestMessage> requestEnricher,
            CancellationToken cancellationToken);

        internal abstract Task<T> ProcessResourceOperationAsync<T>(
           Uri resourceUri,
           ResourceType resourceType,
           OperationType operationType,
           CosmosRequestOptions requestOptions,
           Object partitionKey,
           Stream streamPayload,
           Action<CosmosRequestMessage> requestEnricher,
           Func<CosmosResponseMessage, T> responseCreator,
           CancellationToken cancellationToken);
    }
}
