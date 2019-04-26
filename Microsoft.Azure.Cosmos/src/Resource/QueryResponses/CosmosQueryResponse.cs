//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// The cosmos query response
    /// </summary>
    public class CosmosQueryResponse : IDisposable
    {
        private bool _isDisposed = false;
        private readonly INameValueCollection _responseHeaders = null;
        private readonly IReadOnlyDictionary<string, QueryMetrics> _queryMetrics;
        private readonly string disallowContinuationTokenMessage;
        private readonly string continuationToken;

        /// <summary>
        /// Empty constructor that can be used for unit testing
        /// </summary>
        public CosmosQueryResponse()
        {

        }

        /// <summary>
        /// Create a <see cref="CosmosQueryResponse{T}"/>
        /// </summary>
        internal CosmosQueryResponse(
            INameValueCollection responseHeaders,
            Stream content,
            int count,
            string continuationToken,
            string disallowContinuationTokenMessage,
            IReadOnlyDictionary<string, QueryMetrics> queryMetrics = null)
        {
            this._responseHeaders = responseHeaders;
            this._queryMetrics = queryMetrics;
            this.Content = content;
            this.Count = count;
            this.StatusCode = HttpStatusCode.OK;
            this.disallowContinuationTokenMessage = disallowContinuationTokenMessage;
            this.continuationToken = continuationToken;
        }

        internal CosmosQueryResponse(
            string errorMessage,
            HttpStatusCode httpStatusCode,
            TimeSpan retryAfter,
            INameValueCollection responseHeaders = null)
        {
            this.continuationToken = null;
            this.Content = null;
            this._responseHeaders = responseHeaders;
            this.StatusCode = httpStatusCode;
            this.RetryAfter = retryAfter;
            this.ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Gets the continuation token
        /// </summary>
        public virtual string ContinuationToken
        {
            get
            {
                if (this.disallowContinuationTokenMessage != null)
                {
                    throw new ArgumentException(this.disallowContinuationTokenMessage);
                }

                return this.continuationToken;
            }
        }

        /// <summary>
        /// Contains the stream response of the operation
        /// </summary>
        public virtual Stream Content { get; protected set; }

        /// <summary>
        /// Gets the <see cref="HttpStatusCode"/> of the current response.
        /// </summary>
        public virtual HttpStatusCode StatusCode { get; private set; }

        /// <summary>
        /// The exception if the operation failed.
        /// </summary>
        public virtual string ErrorMessage { get; }

        /// <summary>
        /// The number of items in the query response
        /// </summary>
        public virtual int Count { get; }

        internal TimeSpan? RetryAfter { get; }

        /// <summary>
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in request units.
        /// </value>
        public virtual double RequestCharge
        {
            get
            {
                if (this._responseHeaders == null)
                {
                    return 0;
                }

                return Helpers.GetHeaderValueDouble(
                    this._responseHeaders,
                    HttpConstants.HttpHeaders.RequestCharge,
                    0);
            }
        }

        /// <summary>
        /// Gets the activity ID for the request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        public virtual string ActivityId
        {
            get
            {
                if (this._responseHeaders == null)
                {
                    return null;
                }

                return this._responseHeaders[HttpConstants.HttpHeaders.ActivityId];
            }
        }

        /// <summary>
        /// Returns true if the operation succeeded
        /// </summary>
        public virtual bool IsSuccess => this.StatusCode == HttpStatusCode.OK;

        internal static CosmosQueryResponse CreateResponse(
            FeedResponse<CosmosElement> feedResponse,
            CosmosSerializationOptions cosmosSerializationOptions)
        {
            return FeedResponseBinder.ConvertToCosmosQueryResponse(feedResponse, null);
        }

        /// <summary>
        /// Dispose of the response content
        /// </summary>
        public void Dispose()
        {
            if (!this._isDisposed && this.Content != null)
            {
                this._isDisposed = true;
                this.Content.Dispose();
            }
        }

        internal bool GetHasMoreResults()
        {
            return !string.IsNullOrEmpty(this.ContinuationToken);
        }
    }

    /// <summary>
    /// The cosmos query response
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CosmosQueryResponse<T> : IEnumerable<T>
    {
        private IEnumerable<T> Resources;
        private bool HasMoreResults;

        /// <summary>
        /// Create a <see cref="CosmosQueryResponse{T}"/>
        /// </summary>
        protected CosmosQueryResponse(
            bool hasMoreResults,
            string continuationToken,
            string disallowContinuationTokenMessage)
        {
            this.HasMoreResults = hasMoreResults;
            this.DisallowContinuationTokenMessage = disallowContinuationTokenMessage;
            this.InternalContinuationToken = continuationToken;
        }

        internal virtual string DisallowContinuationTokenMessage { get; }

        internal virtual string InternalContinuationToken { get; }

        /// <summary>
        /// Gets the continuation token
        /// </summary>
        public virtual string ContinuationToken
        {
            get
            {
                if (this.DisallowContinuationTokenMessage != null)
                {
                    throw new ArgumentException(this.DisallowContinuationTokenMessage);
                }

                return this.InternalContinuationToken;
            }
        }

        /// <summary>
        /// Get the enumerators to iterate through the results
        /// </summary>
        /// <returns>An enumerator of the response objects</returns>
        public virtual IEnumerator<T> GetEnumerator()
        {
            if (this.Resources == null)
            {
                return Enumerable.Empty<T>().GetEnumerator();
            }

            return this.Resources.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal static CosmosQueryResponse<TInput> CreateResponse<TInput>(
            Stream stream,
            CosmosJsonSerializer jsonSerializer,
            string continuationToken,
            bool hasMoreResults)
        {
            using (stream)
            {
                CosmosQueryResponse<TInput> queryResponse = new CosmosQueryResponse<TInput>(
                    hasMoreResults: hasMoreResults,
                    continuationToken: continuationToken,
                    disallowContinuationTokenMessage: null);

                queryResponse.InitializeResource(stream, jsonSerializer);
                return queryResponse;
            }
        }

        internal static CosmosQueryResponse<TInput> CreateResponse<TInput>(
            IEnumerable<TInput> resources,
            string continuationToken,
            bool hasMoreResults)
        {
            CosmosQueryResponse<TInput> queryResponse = new CosmosQueryResponse<TInput>(
                hasMoreResults: hasMoreResults,
                continuationToken: continuationToken,
                disallowContinuationTokenMessage: null);
            queryResponse.Resources = resources;
            return queryResponse;
        }

        private void InitializeResource(
            Stream stream,
            CosmosJsonSerializer jsonSerializer)
        {
            this.Resources = jsonSerializer.FromStream<CosmosFeedResponse<T>>(stream).Data;
        }

        internal static CosmosQueryResponse<TInput> CreateResponse<TInput>(
            FeedResponse<CosmosElement> feedResponse,
            CosmosJsonSerializer jsonSerializer,
            bool hasMoreResults,
            ResourceType resourceType)
        {
            CosmosQueryResponse<TInput> queryResponse = new CosmosQueryResponse<TInput>(
                hasMoreResults: hasMoreResults,
                continuationToken: feedResponse.InternalResponseContinuation,
                disallowContinuationTokenMessage: feedResponse.DisallowContinuationTokenMessage);

            queryResponse.InitializeResource(feedResponse, jsonSerializer, resourceType);
            return queryResponse;
        }

        private void InitializeResource(
            FeedResponse<CosmosElement> feedResponse,
            CosmosJsonSerializer jsonSerializer,
            ResourceType resourceType)
        {
            this.Resources = FeedResponseBinder.ConvertCosmosElementFeed<T>(
                feedResponse,
                resourceType,
                jsonSerializer);
        }

        internal bool GetHasMoreResults()
        {
            return this.HasMoreResults;
        }
    }
}