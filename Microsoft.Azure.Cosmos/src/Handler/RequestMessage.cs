//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core.Http;
    using global::Azure.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents a request in the processing pipeline of the Azure Cosmos DB SDK.
    /// </summary>
    /// <remarks>
    /// It is expected that direct property access is used for properties that will be read and used within the Azure Cosmos SDK pipeline, for example <see cref="RequestMessage.OperationType"/>.
    /// <see cref="RequestMessage.Properties"/> should be used for any other property that needs to be sent to the backend but will not be read nor used within the Azure Cosmos DB SDK pipeline.
    /// </remarks>
    public class RequestMessage : Request
    {
        /// <summary>
        /// Create a <see cref="RequestMessage"/>
        /// </summary>
        public RequestMessage()
        {
        }

        /// <summary>
        /// Create a <see cref="RequestMessage"/>
        /// </summary>
        /// <param name="method">The http method</param>
        /// <param name="requestUri">The requested URI</param>
        public RequestMessage(RequestMethod method, Uri requestUri)
        {
            this.Method = method;
            this.RequestUri = requestUri;
        }

        /// <summary>
        /// Gets the <see cref="Uri"/> for the current request.
        /// </summary>
        public virtual Uri RequestUri { get; private set; }

        /// <summary>
        /// Gets the current <see cref="RequestMessage"/> HTTP headers.
        /// </summary>
        internal virtual CosmosHeaders CosmosHeaders => this.headers.Value;

        internal RequestOptions RequestOptions { get; set; }

        internal ResourceType ResourceType { get; set; }

        internal OperationType OperationType { get; set; }

        internal PartitionKeyRangeIdentity PartitionKeyRangeId { get; set; }

        /// <summary>
        /// Used to override the client default. This is used for scenarios
        /// in query where the service interop is not present.
        /// </summary>
        internal bool? UseGatewayMode { get; set; }

        internal DocumentServiceRequest DocumentServiceRequest { get; set; }

        internal Action<DocumentServiceRequest> OnBeforeSendRequestActions { get; set; }

        internal bool IsPropertiesInitialized => this.properties.IsValueCreated;

        /// <summary>
        /// The partition key range handler is only needed for read feed on partitioned resources 
        /// where the partition key range needs to be computed. 
        /// </summary>
        internal bool IsPartitionKeyRangeHandlerRequired => this.OperationType == OperationType.ReadFeed && 
            (this.ResourceType == ResourceType.Document || this.ResourceType == ResourceType.Conflict) && 
            this.PartitionKeyRangeId == null && this.CosmosHeaders.PartitionKey == null;

        /// <summary>
        /// Request properties Per request context available to handlers. 
        /// These will not be automatically included into the wire.
        /// </summary>
        internal virtual Dictionary<string, object> Properties => this.properties.Value;

        /// <summary>
        /// TBD
        /// </summary>
        public override string ClientRequestId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        private readonly Lazy<Dictionary<string, object>> properties = new Lazy<Dictionary<string, object>>(RequestMessage.CreateDictionary);

        private readonly Lazy<CosmosHeaders> headers = new Lazy<CosmosHeaders>(RequestMessage.CreateHeaders);

        private bool disposed;

        /// <summary>
        /// Disposes the current <see cref="RequestMessage"/>.
        /// </summary>
        public override void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the request message content
        /// </summary>
        /// <param name="disposing">True to dispose content</param>
        protected virtual void Dispose(bool disposing)
        {
            // The reason for this type to implement IDisposable is that it contains instances of types that implement
            // IDisposable (content). 
            if (disposing && !this.disposed)
            {
                this.disposed = true;
                if (this.Content != null)
                {
                    this.Content.Dispose();
                }
            }
        }

        internal void AddThroughputHeader(int? throughputValue)
        {
            if (throughputValue.HasValue)
            {
                this.CosmosHeaders.OfferThroughput = throughputValue.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        internal async Task AssertPartitioningDetailsAsync(CosmosClient client, CancellationToken cancellationToken)
        {
            if (this.IsMasterOperation())
            {
                return;
            }

#if DEBUG
            try
            {
                CollectionCache collectionCache = await client.DocumentClient.GetCollectionCacheAsync();
                ContainerProperties collectionFromCache =
                    await collectionCache.ResolveCollectionAsync(this.ToDocumentServiceRequest(), cancellationToken);
                if (collectionFromCache.PartitionKey?.Paths?.Count > 0)
                {
                    Debug.Assert(this.AssertPartitioningPropertiesAndHeaders());
                }
            }
            catch (DocumentClientException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Ignore container non-existence
            }
#else
            await Task.CompletedTask;
#endif
        }

        internal DocumentServiceRequest ToDocumentServiceRequest()
        {
            if (this.DocumentServiceRequest == null)
            {
                DocumentServiceRequest serviceRequest;
                if (this.OperationType == OperationType.ReadFeed && this.ResourceType == ResourceType.Database)
                {
                    serviceRequest = new DocumentServiceRequest(
                        operationType: this.OperationType,
                        resourceIdOrFullName: null,
                        resourceType: this.ResourceType,
                        body: this.Content.GetStream(),
                        headers: this.CosmosHeaders.CosmosMessageHeaders,
                        isNameBased: false,
                        authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);
                }
                else
                {
                    serviceRequest = new DocumentServiceRequest(
                        this.OperationType,
                        this.ResourceType,
                        this.RequestUri?.ToString(),
                        this.Content.GetStream(),
                        AuthorizationTokenType.PrimaryMasterKey,
                        this.CosmosHeaders.CosmosMessageHeaders);
                }

                if (this.UseGatewayMode.HasValue)
                {
                    serviceRequest.UseGatewayMode = this.UseGatewayMode.Value;
                }

                serviceRequest.UseStatusCodeForFailures = true;
                serviceRequest.UseStatusCodeFor429 = true;
                serviceRequest.Properties = this.Properties;
                this.DocumentServiceRequest = serviceRequest;
            }

            // Routing to a particular PartitionKeyRangeId
            if (this.PartitionKeyRangeId != null)
            {
                this.DocumentServiceRequest.RouteTo(this.PartitionKeyRangeId);
            }

            this.OnBeforeRequestHandler(this.DocumentServiceRequest);
            return this.DocumentServiceRequest;
        }

        private static Dictionary<string, object> CreateDictionary()
        {
            return new Dictionary<string, object>();
        }

        private static CosmosHeaders CreateHeaders()
        {
            return new CosmosHeaders();
        }

        private void OnBeforeRequestHandler(DocumentServiceRequest serviceRequest)
        {
            if (this.OnBeforeSendRequestActions != null)
            {
                this.OnBeforeSendRequestActions(serviceRequest);
            }
        }

        private bool AssertPartitioningPropertiesAndHeaders()
        {
            // Either PK/key-range-id is assumed
            bool pkExists = !string.IsNullOrEmpty(this.CosmosHeaders.PartitionKey);
            bool epkExists = this.Properties.ContainsKey(WFConstants.BackendHeaders.EffectivePartitionKeyString);
            if (pkExists && epkExists)
            {
                throw new ArgumentNullException(RMResources.PartitionKeyAndEffectivePartitionKeyBothSpecified);
            }

            bool isPointOperation = this.OperationType != OperationType.ReadFeed;
            if (!pkExists && !epkExists && this.OperationType.IsPointOperation())
            {
                throw new ArgumentNullException(RMResources.MissingPartitionKeyValue);
            }

            bool partitionKeyRangeIdExists = !string.IsNullOrEmpty(this.CosmosHeaders.PartitionKeyRangeId);
            if (partitionKeyRangeIdExists)
            {
                // Assert operation type is not write
                if (this.OperationType != OperationType.Query && this.OperationType != OperationType.ReadFeed && this.OperationType != OperationType.Batch)
                {
                    throw new ArgumentOutOfRangeException(RMResources.UnexpectedPartitionKeyRangeId);
                }
            }

            if (pkExists && partitionKeyRangeIdExists)
            {
                throw new ArgumentOutOfRangeException(RMResources.PartitionKeyAndPartitionKeyRangeRangeIdBothSpecified);
            }

            return true;
        }

        private bool IsMasterOperation()
        {
            return this.ResourceType != ResourceType.Document;
        }

        private void CheckDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.GetType().ToString());
            }
        }

        /// <inheritdoc />
        protected override void AddHeader(string name, string value)
        {
            this.CosmosHeaders.Add(name, value);
        }

        /// <inheritdoc />
        protected override bool TryGetHeader(string name, out string value)
        {
            return this.CosmosHeaders.TryGetValue(name, out value);
        }

        /// <inheritdoc />
        protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
        {
            values = null;
            string singleValue;
            bool retValue = this.CosmosHeaders.TryGetValue(name, out singleValue);
            if (retValue)
            {
                values = new List<string>() { singleValue };
            }

            return retValue;
        }

        /// <inheritdoc />
        protected override bool ContainsHeader(string name)
        {
            string singleValue;
            return this.CosmosHeaders.TryGetValue(name, out singleValue);
        }

        /// <inheritdoc />
        protected override bool RemoveHeader(string name)
        {
            this.CosmosHeaders.Remove(name);
            return true;
        }

        /// <inheritdoc />
        protected override IEnumerable<HttpHeader> EnumerateHeaders()
        {
            throw new NotImplementedException();
        }
    }

    internal static class CoreExtensions
    {
        public static Stream GetStream(this HttpPipelineRequestContent content)
        {
            CosmosStreamContent cosmosContent = (CosmosStreamContent)content;
            if (cosmosContent != null)
            {
                return cosmosContent.Detach();
            }

            // Return stream
            throw new NotImplementedException();
        }

        ////public static CosmosMessageHeadersInternal GetHeaders(this Request request)
        ////{
        ////    RequestMessage requestMessage = (RequestMessage)request;
        ////    if (requestMessage != null)
        ////    {
        ////        return requestMessage.Headers;
        ////    }

        ////    throw new NotImplementedException();
        ////}
    }

    internal sealed class CosmosStreamContent : HttpPipelineRequestContent
    {
        private const int CopyToBufferSize = 81920;
        private readonly long Origin;

        private Stream stream;

        public CosmosStreamContent(Stream stream)
        {
            if (!stream.CanSeek) throw new ArgumentException("stream must be seekable", nameof(stream));

            this.Origin = stream.Position;
            this.stream = stream;
        }

        public override void WriteTo(Stream stream, CancellationToken cancellationToken)
        {
            this.stream.Seek(this.Origin, SeekOrigin.Begin);

            // this is not using CopyTo so that we can honor cancellations.
            byte[] buffer = ArrayPool<byte>.Shared.Rent(CopyToBufferSize);
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int read = this.stream.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        break;
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    stream.Write(buffer, 0, read);
                }
            }
            finally
            {
                stream.Flush();
                ArrayPool<byte>.Shared.Return(buffer, true);
            }
        }

        public override bool TryComputeLength(out long length)
        {
            if (this.stream.CanSeek)
            {
                length = this.stream.Length - this.Origin;
                return true;
            }
            length = 0;
            return false;
        }

        public override async Task WriteToAsync(Stream stream, CancellationToken cancellation)
        {
            this.stream.Seek(this.Origin, SeekOrigin.Begin);
            await this.stream.CopyToAsync(stream, CopyToBufferSize, cancellation).ConfigureAwait(false);
        }

        public Stream Detach()
        {
            Stream response = this.stream;
            this.stream = null;

            return response;
        }

        public override void Dispose()
        {
            if (this.stream == null)
            {
                this.stream.Dispose();
            }
        }
    }
}