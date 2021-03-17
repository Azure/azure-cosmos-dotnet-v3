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
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents a request in the processing pipeline of the Azure Cosmos DB SDK.
    /// </summary>
    /// <remarks>
    /// It is expected that direct property access is used for properties that will be read and used within the Azure Cosmos SDK pipeline, for example <see cref="RequestMessage.OperationType"/>.
    /// <see cref="RequestMessage.Properties"/> should be used for any other property that needs to be sent to the backend but will not be read nor used within the Azure Cosmos DB SDK pipeline.
    /// <see cref="RequestMessage.Headers"/> should be used for HTTP headers that need to be passed down and sent to the backend.
    /// </remarks>
    public class RequestMessage : IDisposable
    {
        /// <summary>
        /// Create a <see cref="RequestMessage"/>
        /// </summary>
        public RequestMessage()
        {
            this.Trace = NoOpTrace.Singleton;
        }

        /// <summary>
        /// Create a <see cref="RequestMessage"/>
        /// </summary>
        /// <param name="method">The http method</param>
        /// <param name="requestUri">The requested URI</param>
        public RequestMessage(HttpMethod method, Uri requestUri)
        {
            this.Method = method;
            this.RequestUriString = requestUri?.OriginalString;
            this.InternalRequestUri = requestUri;
            this.Trace = NoOpTrace.Singleton;
        }

        /// <summary>
        /// Create a <see cref="RequestMessage"/>
        /// </summary>
        /// <param name="method">The http method</param>
        /// <param name="requestUriString">The requested URI</param>
        /// /// <param name="trace">The trace node to append traces to.</param>
        internal RequestMessage(
            HttpMethod method,
            string requestUriString,
            ITrace trace)
        {
            this.Method = method;
            this.RequestUriString = requestUriString;
            this.Trace = trace ?? throw new ArgumentNullException(nameof(trace));
        }

        /// <summary>
        /// Gets the <see cref="HttpMethod"/> for the current request.
        /// </summary>
        public virtual HttpMethod Method { get; private set; }

        /// <summary>
        /// Gets the <see cref="Uri"/> for the current request.
        /// </summary>
        public virtual Uri RequestUri
        {
            get
            {
                if (this.InternalRequestUri == null)
                {
                    this.InternalRequestUri = new Uri(this.RequestUriString, UriKind.Relative);
                }

                return this.InternalRequestUri;
            }
        }

        /// <summary>
        /// Gets the current <see cref="RequestMessage"/> HTTP headers.
        /// </summary>
        public virtual Headers Headers => this.headers.Value;

        /// <summary>
        /// Gets or sets the current <see cref="RequestMessage"/> payload.
        /// </summary>
        public virtual Stream Content
        {
            get => this.content;
            set
            {
                this.CheckDisposed();
                this.content = value;
            }
        }

        internal string RequestUriString { get; }

        internal Uri InternalRequestUri { get; private set; }

        internal ITrace Trace { get; set; }

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
            this.ResourceType.IsPartitioned() && this.PartitionKeyRangeId == null &&
            this.Headers.PartitionKey == null;

        /// <summary>
        /// Request properties Per request context available to handlers. 
        /// These will not be automatically included into the wire.
        /// </summary>
        public virtual Dictionary<string, object> Properties => this.properties.Value;

        private readonly Lazy<Dictionary<string, object>> properties = new Lazy<Dictionary<string, object>>(RequestMessage.CreateDictionary);

        private readonly Lazy<Headers> headers = new Lazy<Headers>(RequestMessage.CreateHeaders);

        private bool disposed;

        private Stream content;

        /// <summary>
        /// Disposes the current <see cref="RequestMessage"/>.
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
                this.Headers.OfferThroughput = throughputValue.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        internal void AddThroughputPropertiesHeader(ThroughputProperties throughputProperties)
        {
            if (throughputProperties == null)
            {
                return;
            }

            if (throughputProperties.Throughput.HasValue &&
                (throughputProperties.AutoscaleMaxThroughput.HasValue || throughputProperties.AutoUpgradeMaxThroughputIncrementPercentage.HasValue))
            {
                throw new InvalidOperationException("Autoscale provisioned throughput can not be configured with fixed offer");
            }

            if (throughputProperties.Throughput.HasValue)
            {
                this.AddThroughputHeader(throughputProperties.Throughput);
            }
            else if (throughputProperties?.Content?.OfferAutoscaleSettings != null)
            {
                this.Headers.Add(HttpConstants.HttpHeaders.OfferAutopilotSettings, throughputProperties.Content.OfferAutoscaleSettings.GetJsonString());
            }
        }

        internal async Task AssertPartitioningDetailsAsync(CosmosClient client, CancellationToken cancellationToken, ITrace trace)
        {
            if (this.IsMasterOperation())
            {
                return;
            }

#if DEBUG
            try
            {
                CollectionCache collectionCache = await client.DocumentClient.GetCollectionCacheAsync(trace);
                ContainerProperties collectionFromCache =
                    await collectionCache.ResolveCollectionAsync(this.ToDocumentServiceRequest(), cancellationToken, trace);
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
                        body: this.Content,
                        headers: this.Headers.CosmosMessageHeaders,
                        isNameBased: false,
                        authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);
                }
                else
                {
                    serviceRequest = new DocumentServiceRequest(
                        this.OperationType, 
                        this.ResourceType,
                        this.RequestUriString, 
                        this.Content, 
                        AuthorizationTokenType.PrimaryMasterKey, 
                        this.Headers.CosmosMessageHeaders);
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

        private static Headers CreateHeaders()
        {
            return new Headers();
        }

        private void OnBeforeRequestHandler(DocumentServiceRequest serviceRequest)
        {
            this.OnBeforeSendRequestActions?.Invoke(serviceRequest);
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

            bool partitionKeyRangeIdExists = !string.IsNullOrEmpty(this.Headers.PartitionKeyRangeId);
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
    }
}