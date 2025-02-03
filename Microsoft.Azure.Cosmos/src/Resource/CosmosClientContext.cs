//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Telemetry;

    /// <summary>
    /// This class is used to get access to different client level operations without directly referencing the client object.
    /// This makes it easy to pass a reference to the client, and it makes it easy to mock for unit tests.
    /// </summary>
    internal abstract class CosmosClientContext : IDisposable
    {
        /// <summary>
        /// The Cosmos client that is used for the request
        /// </summary>
        internal abstract CosmosClient Client { get; }

        internal abstract DocumentClient DocumentClient { get; }

        internal abstract CosmosSerializerCore SerializerCore { get; }

        internal abstract CosmosResponseFactoryInternal ResponseFactory { get; }

        internal abstract RequestInvokerHandler RequestHandler { get; }

        internal abstract CosmosClientOptions ClientOptions { get; }

        internal abstract string UserAgent { get; }

        internal abstract BatchAsyncContainerExecutor GetExecutorForContainer(
            ContainerInternal container);

        /// <summary>
        /// Generates the URI link for the resource
        /// </summary>
        /// <param name="parentLink">The parent link URI (/dbs/mydbId) </param>
        /// <param name="uriPathSegment">The URI path segment</param>
        /// <param name="id">The id of the resource</param>
        /// <returns>A resource link in the format of {parentLink}/this.UriPathSegment/this.Name with this.Name being a Uri escaped version</returns>
        internal abstract string CreateLink(
            string parentLink,
            string uriPathSegment,
            string id);

        internal abstract void ValidateResource(string id);

        internal abstract Task<ContainerProperties> GetCachedContainerPropertiesAsync(
            string containerUri,
            ITrace trace,
            CancellationToken cancellationToken);

        internal abstract Task<TResult> OperationHelperAsync<TResult>(
            string operationName,
            string containerName,
            string databaseName,
            OperationType operationType,
            RequestOptions requestOptions,
            Func<ITrace, Task<TResult>> task,
            (string OperationName, Func<TResult, OpenTelemetryAttributes> GetAttributes)? openTelemetry = null,
            ResourceType? resourceType = null,
            TraceComponent traceComponent = TraceComponent.Transport,
            TraceLevel traceLevel = TraceLevel.Info);

        /// <summary>
        /// This is a wrapper around ExecUtil method. This allows the calls to be mocked so logic done 
        /// in a resource can be unit tested.
        /// </summary>
        internal abstract Task<ResponseMessage> ProcessResourceOperationStreamAsync(
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerInternal cosmosContainerCore,
            PartitionKey? partitionKey,
            string itemId,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            ITrace trace,
            CancellationToken cancellationToken);

        /// <summary>
        /// This is a wrapper around ExecUtil method. This allows the calls to be mocked so logic done 
        /// in a resource can be unit tested.
        /// </summary>
        internal abstract Task<ResponseMessage> ProcessResourceOperationStreamAsync(
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerInternal cosmosContainerCore,
            FeedRange feedRange,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            ITrace trace,
            CancellationToken cancellationToken);

        /// <summary>
        /// This is a wrapper around request invoker method. This allows the calls to be mocked so logic done 
        /// in a resource can be unit tested.
        /// </summary>
        internal abstract Task<T> ProcessResourceOperationAsync<T>(
           string resourceUri,
           ResourceType resourceType,
           OperationType operationType,
           RequestOptions requestOptions,
           ContainerInternal containerInternal,
           FeedRange feedRange,
           Stream streamPayload,
           Action<RequestMessage> requestEnricher,
           Func<ResponseMessage, T> responseCreator,
           ITrace trace,
           CancellationToken cancellationToken);

        /// <summary>
        /// Initializes the given container by establishing the
        /// Rntbd connection to all of the backend replica nodes.
        /// </summary>
        /// <param name="databaseId">A string containing the cosmos database identifier.</param>
        /// <param name="containerLinkUri">A string containing the cosmos container link uri.</param>
        /// <param name="cancellationToken">An instance of the <see cref="CancellationToken"/>.</param>
        internal abstract Task InitializeContainerUsingRntbdAsync(
            string databaseId,
            string containerLinkUri,
            CancellationToken cancellationToken);

        public abstract void Dispose();
    }
}