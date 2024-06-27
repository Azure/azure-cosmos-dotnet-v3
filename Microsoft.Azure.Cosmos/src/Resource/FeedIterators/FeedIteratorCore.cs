//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    /// <summary>
    /// Cosmos feed stream iterator. This is used to get the query responses with a Stream content for non-partitioned results
    /// </summary>
    internal sealed class FeedIteratorCore : FeedIteratorInternal
    {
        private readonly CosmosClientContext clientContext;
        private readonly string resourceLink;
        private readonly ResourceType resourceType;
        private readonly SqlQuerySpec querySpec;
        private bool hasMoreResultsInternal;

        public FeedIteratorCore(
            CosmosClientContext clientContext,
            string resourceLink,
            ResourceType resourceType,
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions options,
            ContainerInternal container,
            string databaseId = null)
        {
            this.resourceLink = resourceLink;
            this.clientContext = clientContext;
            this.resourceType = resourceType;
            this.querySpec = queryDefinition?.ToSqlQuerySpec();
            this.ContinuationToken = continuationToken;
            this.requestOptions = options;
            this.hasMoreResultsInternal = true;

            this.databaseName = databaseId;
            this.container = container;
        }

        public override bool HasMoreResults => this.hasMoreResultsInternal;

        /// <summary>
        /// The query options for the result set
        /// </summary>
        public QueryRequestOptions requestOptions { get; }

        /// <summary>
        /// The Continuation Token
        /// </summary>
        public string ContinuationToken { get; set; }

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            return this.ReadNextAsync(NoOpTrace.Singleton);
        }

        public override async Task<ResponseMessage> ReadNextAsync(
            ITrace trace, 
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            Stream stream;
            OperationType operation = OperationType.ReadFeed;
            if (this.querySpec != null)
            {
                using (ITrace querySpecStreamTrace = trace.StartChild("QuerySpec to Stream", TraceComponent.Poco, TraceLevel.Info))
                {
                    stream = this.clientContext.SerializerCore.ToStreamSqlQuerySpec(this.querySpec, this.resourceType);
                }

                operation = OperationType.Query;
            }
            else
            {
                stream = null;
            }

            ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.resourceLink,
               resourceType: this.resourceType,
               operationType: operation,
               requestOptions: this.requestOptions,
               cosmosContainerCore: null,
               feedRange: this.requestOptions?.PartitionKey.HasValue ?? false ? new FeedRangePartitionKey(this.requestOptions.PartitionKey.Value) : null,
               streamPayload: stream,
               requestEnricher: request =>
               {
                   QueryRequestOptions.FillContinuationToken(request, this.ContinuationToken);
                   if (this.querySpec != null)
                   {
                       request.Headers.Add(HttpConstants.HttpHeaders.ContentType, MediaTypes.QueryJson);
                       request.Headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                   }
               },
               trace: trace,
               cancellationToken: cancellationToken);

            this.ContinuationToken = responseMessage.Headers.ContinuationToken;
            this.hasMoreResultsInternal = this.ContinuationToken != null && responseMessage.StatusCode != HttpStatusCode.NotModified;

            if (responseMessage.Content != null)
            {
                await RewriteStreamAsTextAsync(responseMessage, this.requestOptions, trace);
            }

            return responseMessage;
        }

        private static async Task RewriteStreamAsTextAsync(ResponseMessage responseMessage, QueryRequestOptions requestOptions, ITrace trace)
        {
            using (ITrace rewriteTrace = trace.StartChild("Rewrite Stream as Text", TraceComponent.Json, TraceLevel.Info))
            {
                // Rewrite the payload to be in the specified format.
                // If it's already in the correct format, then the following will be a memcpy.
                MemoryStream memoryStream;
                if (responseMessage.Content is MemoryStream responseContentAsMemoryStream)
                {
                    memoryStream = responseContentAsMemoryStream;
                }
                else
                {
                    memoryStream = new MemoryStream();
                    await responseMessage.Content.CopyToAsync(memoryStream);
                }

                ReadOnlyMemory<byte> buffer;
                if (memoryStream.TryGetBuffer(out ArraySegment<byte> segment))
                {
                    buffer = segment.Array.AsMemory().Slice(start: segment.Offset, length: segment.Count);
                }
                else
                {
                    buffer = memoryStream.ToArray();
                }

                IJsonNavigator jsonNavigator = JsonNavigator.Create(buffer);
                if (jsonNavigator.SerializationFormat == JsonSerializationFormat.Text)
                {
                    // Exit to avoid the memory allocation.
                    return;
                }

                IJsonWriter jsonWriter;
                if (requestOptions?.CosmosSerializationFormatOptions != null)
                {
                    jsonWriter = requestOptions.CosmosSerializationFormatOptions.CreateCustomWriterCallback();
                }
                else
                {
                    jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
                }

                jsonNavigator.WriteNode(jsonNavigator.GetRootNode(), jsonWriter);

                ReadOnlyMemory<byte> result = jsonWriter.GetResult();
                MemoryStream rewrittenMemoryStream;
                if (MemoryMarshal.TryGetArray(result, out ArraySegment<byte> rewrittenSegment))
                {
                    rewrittenMemoryStream = new MemoryStream(rewrittenSegment.Array, index: rewrittenSegment.Offset, count: rewrittenSegment.Count, writable: false, publiclyVisible: true);
                }
                else
                {
                    byte[] toArray = result.ToArray();
                    rewrittenMemoryStream = new MemoryStream(toArray, index: 0, count: toArray.Length, writable: false, publiclyVisible: true);
                }

                responseMessage.Content = rewrittenMemoryStream;
            }
        }
    }

    /// <summary>
    /// Cosmos feed iterator that keeps track of the continuation token when retrieving results form a query.
    /// </summary>
    /// <typeparam name="T">The response object type that can be deserialized</typeparam>
    internal sealed class FeedIteratorCore<T> : FeedIteratorInternal<T>
    {
        private readonly FeedIteratorInternal feedIterator;
        private readonly Func<ResponseMessage, FeedResponse<T>> responseCreator;

        internal FeedIteratorCore(
            FeedIteratorInternal feedIterator,
            Func<ResponseMessage, FeedResponse<T>> responseCreator)
        {
            this.responseCreator = responseCreator;
            this.feedIterator = feedIterator;

            this.databaseName = feedIterator.databaseName;
            this.container = feedIterator.container;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.ReadNextWithRootTraceAsync(cancellationToken));
        }

        private async Task<FeedResponse<T>> ReadNextWithRootTraceAsync(CancellationToken cancellationToken = default)
        {
            using (ITrace trace = Trace.GetRootTrace("FeedIteratorCore ReadNextAsync", TraceComponent.Unknown, TraceLevel.Info))
            {
                return await this.ReadNextAsync(trace, cancellationToken);
            }
        }

        public override async Task<FeedResponse<T>> ReadNextAsync(ITrace trace, CancellationToken cancellationToken = default)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            ResponseMessage response = await this.feedIterator.ReadNextAsync(trace, cancellationToken);
            return this.responseCreator(response);
        }

        protected override void Dispose(bool disposing)
        {
            this.feedIterator.Dispose();
            base.Dispose(disposing);
        }
    }
}
