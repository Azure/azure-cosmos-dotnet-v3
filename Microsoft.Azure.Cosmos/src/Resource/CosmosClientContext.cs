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

    internal class CosmosClientContext
    {
        private static readonly char[] InvalidCharacters = new char[] { '/', '\\', '?', '#' };

        internal CosmosClientContext(
            CosmosClient client,
            CosmosJsonSerializer cosmosJsonSerializer,
            CosmosResponseFactory cosmosResponseFactory,
            CosmosRequestHandler requestHandler,
            DocumentClient documentClient)
        {
            this.Client = client;
            this.JsonSerializer = cosmosJsonSerializer;
            this.ResponseFactory = cosmosResponseFactory;
            this.RequestHandler = requestHandler;
            this.DocumentClient = documentClient;
        }

        /// <summary>
        /// The Cosmos client that is used for the request
        /// </summary>
        internal CosmosClient Client { get; }

        internal DocumentClient DocumentClient { get; }

        internal CosmosJsonSerializer JsonSerializer { get; }

        internal CosmosResponseFactory ResponseFactory { get; }

        internal CosmosRequestHandler RequestHandler { get; }

        /// <summary>
        /// Generates the URI link for the resource
        /// </summary>
        /// <param name="parentLink">The parent link URI (/dbs/mydbId) </param>
        /// <param name="uriPathSegment">The URI path segment</param>
        /// <param name="id">The id of the resource</param>
        /// <returns>A resource link in the format of {parentLink}/this.UriPathSegment/this.Name with this.Name being a Uri escaped version</returns>
        internal Uri CreateLink(
            string parentLink,
            string uriPathSegment,
            string id)
        {
            int parentLinkLength = parentLink?.Length ?? 0;
            string idUriEscaped = Uri.EscapeUriString(id);

            StringBuilder stringBuilder = new StringBuilder(parentLinkLength + 2 + uriPathSegment.Length + idUriEscaped.Length);
            if (parentLinkLength > 0)
            {
                stringBuilder.Append(parentLink);
            }

            stringBuilder.Append("/");
            stringBuilder.Append(uriPathSegment);
            stringBuilder.Append("/");
            stringBuilder.Append(idUriEscaped);
            return new Uri(stringBuilder.ToString(), UriKind.Relative);
        }

        internal void ValidateResource(string resourceId)
        {
            this.DocumentClient.ValidateResource(resourceId);
        }

        internal Task<CosmosResponseMessage> ProcessResourceOperationStreamAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            Object partitionKey,
            Stream streamPayload,
            Action<CosmosRequestMessage> requestEnricher,
            CancellationToken cancellationToken)
        {
            return ExecUtils.ProcessResourceOperationStreamAsync(
                requestHandler: this.RequestHandler,
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestEnricher: requestEnricher,
                cancellationToken: cancellationToken);
        }

        internal Task<T> ProcessResourceOperationAsync<T>(
           Uri resourceUri,
           ResourceType resourceType,
           OperationType operationType,
           CosmosRequestOptions requestOptions,
           Object partitionKey,
           Stream streamPayload,
           Action<CosmosRequestMessage> requestEnricher,
           Func<CosmosResponseMessage, T> responseCreator,
           CancellationToken cancellationToken)
        {
            return ExecUtils.ProcessResourceOperationAsync<T>(
                requestHandler: this.RequestHandler,
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestEnricher: requestEnricher,
                responseCreator: responseCreator,
                cancellationToken: cancellationToken);
        }
    }
}
