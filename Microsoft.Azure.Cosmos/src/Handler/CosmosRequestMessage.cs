//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents a request in the processing pipeline of the Azure Cosmos DB SDK.
    /// </summary>
    /// <remarks>
    /// It is expected that direct property access is used for properties that will be read and used within the Azure Cosmos SDK pipeline, for example <see cref="CosmosRequestMessage.OperationType"/>.
    /// <see cref="CosmosRequestMessage.Properties"/> should be used for any other property that needs to be sent to the backend but will not be read nor used within the Azure Cosmos DB SDK pipeline.
    /// <see cref="CosmosRequestMessage.Headers"/> should be used for HTTP headers that need to be passed down and sent to the backend.
    /// </remarks>
    public class CosmosRequestMessage : IDisposable
    {
        /// <summary>
        /// Create a <see cref="CosmosRequestMessage"/>
        /// </summary>
        public CosmosRequestMessage() { }

        /// <summary>
        /// Create a <see cref="CosmosRequestMessage"/>
        /// </summary>
        /// <param name="method">The http method</param>
        /// <param name="requestUri">The requested URI</param>
        public CosmosRequestMessage(HttpMethod method, Uri requestUri)
        {
            this.Method = method;
            this.RequestUri = requestUri;
        }

        /// <summary>
        /// Gets the <see cref="HttpMethod"/> for the current request.
        /// </summary>
        public virtual HttpMethod Method { get; private set; }

        /// <summary>
        /// Gets the <see cref="Uri"/> for the current request.
        /// </summary>
        public virtual Uri RequestUri { get; private set; }

        /// <summary>
        /// Gets the current <see cref="CosmosRequestMessage"/> HTTP headers.
        /// </summary>
        public virtual CosmosRequestMessageHeaders Headers => this.headers.Value;

        /// <summary>
        /// Gets or sets the current <see cref="CosmosRequestMessage"/> payload.
        /// </summary>
        public virtual Stream Content
        {
            get => this._content;
            set
            {
                this.CheckDisposed();
                this._content = value;
            }
        }

        internal RequestOptions RequestOptions { get; set; }

        internal ResourceType ResourceType { get; set; }

        internal OperationType OperationType { get; set; }

        internal string PartitionKeyRangeId { get; set; }

        /// <summary>
        /// Used to override the client default. This is used for scenarios
        /// in query where the service interop is not present.
        /// </summary>
        internal bool? UseGatewayMode { get; set; }

        internal DocumentServiceRequest DocumentServiceRequest { get; set; }

        internal IDocumentClientRetryPolicy DocumentClientRetryPolicy { get; set; }

        internal bool IsPropertiesInitialized => this.properties.IsValueCreated;

        internal bool IsDocumentFeedOperation => this.OperationType == OperationType.ReadFeed && this.ResourceType == ResourceType.Document && string.IsNullOrEmpty(this.PartitionKeyRangeId);

        /// <summary>
        /// Request properties Per request context available to handlers. 
        /// These will not be automatically included into the wire.
        /// </summary>
        public virtual Dictionary<string, object> Properties => this.properties.Value;

        private bool _disposed;

        private Stream _content;

        private readonly Lazy<Dictionary<string, object>> properties = new Lazy<Dictionary<string, object>>(CosmosRequestMessage.CreateDictionary);

        private readonly Lazy<CosmosRequestMessageHeaders> headers = new Lazy<CosmosRequestMessageHeaders>(CosmosRequestMessage.CreateHeaders);

        /// <summary>
        /// Disposes the current <see cref="CosmosRequestMessage"/>.
        /// </summary>
        public void Dispose()
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
            if (disposing && !this._disposed)
            {
                this._disposed = true;
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
                this.Headers.OfferThroughput = throughputValue.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        internal async Task AssertPartitioningDetailsAsync(CosmosClient client, CancellationToken cancellationToken)
        {
            if (this.IsMasterOperation())
            {
                return;
            }

#if DEBUG
            CollectionCache collectionCache = await client.DocumentClient.GetCollectionCacheAsync();
            CosmosContainerSettings collectionFromCache =
                await collectionCache.ResolveCollectionAsync(this.ToDocumentServiceRequest(), cancellationToken);
            if (collectionFromCache.PartitionKey?.Paths?.Count > 0)
            {
                Debug.Assert(this.AssertPartitioningPropertiesAndHeaders());
            }
#endif
#if !DEBUG
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
                    serviceRequest = new DocumentServiceRequest(this.OperationType, null, this.ResourceType, this.Content, this.Headers.CosmosMessageHeaders, false, AuthorizationTokenType.PrimaryMasterKey);
                }
                else
                {
                    serviceRequest = new DocumentServiceRequest(this.OperationType, this.ResourceType, this.RequestUri?.ToString(), this.Content, AuthorizationTokenType.PrimaryMasterKey, this.Headers.CosmosMessageHeaders);
                }

                if (this.UseGatewayMode.HasValue)
                {
                    serviceRequest.UseGatewayMode = this.UseGatewayMode.Value;
                }

                // Routing to a particular PartitionKeyRangeId
                if (!string.IsNullOrEmpty(this.PartitionKeyRangeId))
                {
                    serviceRequest.RouteTo(new PartitionKeyRangeIdentity(this.PartitionKeyRangeId));
                }

                serviceRequest.UseStatusCodeForFailures = true;
                serviceRequest.UseStatusCodeFor429 = true;
                serviceRequest.Properties = this.Properties;
                this.DocumentServiceRequest = serviceRequest;
            }

            this.OnBeforeRequestHandler(this.DocumentServiceRequest);
            return this.DocumentServiceRequest;
        }

        private void OnBeforeRequestHandler(DocumentServiceRequest serviceRequest)
        {
            if (this.DocumentClientRetryPolicy != null)
            {
                this.DocumentClientRetryPolicy.OnBeforeSendRequest(serviceRequest);
            }
        }

        private bool AssertPartitioningPropertiesAndHeaders()
        {
            // Either PK/key-range-id is assumed
            bool pkExists = !string.IsNullOrEmpty(this.Headers.PartitionKey);
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

            bool partitonKeyRangeIdExists = !string.IsNullOrEmpty(this.Headers.PartitionKeyRangeId);
            if (partitonKeyRangeIdExists)
            {
                // Assert operation type is not write
                if (this.OperationType != OperationType.Query && this.OperationType != OperationType.ReadFeed)
                {
                    throw new ArgumentOutOfRangeException(RMResources.UnexpectedPartitionKeyRangeId);
                }
            }

            if (pkExists && partitonKeyRangeIdExists)
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
            if (this._disposed)
            {
                throw new ObjectDisposedException(this.GetType().ToString());
            }
        }

        private static Dictionary<string, object> CreateDictionary()
        {
            return new Dictionary<string, object>();
        }

        private static CosmosRequestMessageHeaders CreateHeaders()
        {
            return new CosmosRequestMessageHeaders();
        }
    }
}