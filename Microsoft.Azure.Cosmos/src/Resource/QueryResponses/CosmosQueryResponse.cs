//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Internal;

    /// <summary>
    /// The cosmos query response
    /// </summary>
    public class CosmosQueryResponse : IDisposable
    {
        private bool _isDisposed = false;
        private INameValueCollection _responseHeaders = null;
        private readonly IReadOnlyDictionary<string, QueryMetrics> _queryMetrics;

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
            IReadOnlyDictionary<string, QueryMetrics> queryMetrics = null)
        {
            this._responseHeaders = responseHeaders;
            this._queryMetrics = queryMetrics;
            this.Content = content;
            this.Count = count;
            this.ContinuationToken = continuationToken;
            this.StatusCode = HttpStatusCode.OK;
        }

        internal CosmosQueryResponse(
            string errorMessage,
            HttpStatusCode httpStatusCode,
            TimeSpan retryAfter,
            INameValueCollection responseHeaders = null)
        {
            this.ContinuationToken = null;
            this.Content = null;
            this._responseHeaders = responseHeaders;
            this.StatusCode = httpStatusCode;
            this.RetryAfter = retryAfter;
            this.ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Gets the continuation token
        /// </summary>
        public virtual string ContinuationToken { get; protected set; }

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
        /// Get <see cref="Microsoft.Azure.Cosmos.QueryMetrics"/> for each individual partition in the Azure Cosmos DB service
        /// </summary>
        internal IReadOnlyDictionary<string, QueryMetrics> QueryMetrics
        {
            get
            {
                return this._queryMetrics;
            }
        }

        /// <summary>
        /// Returns true if the operation succeeded
        /// </summary>
        public virtual bool IsSuccess => this.StatusCode == HttpStatusCode.OK;

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
        protected CosmosQueryResponse()
        {
        }

        /// <summary>
        /// Gets the continuation token
        /// </summary>
        public virtual string ContinuationToken { get; protected set; }

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
            return GetEnumerator();
        }

        internal static CosmosQueryResponse<TInput> CreateResponse<TInput>(
            Stream stream,
            CosmosJsonSerializer jsonSerializer,
            string continuationToken,
            bool hasMoreResults)
        {
            using (stream)
            {
                CosmosQueryResponse<TInput> queryResponse = new CosmosQueryResponse<TInput>()
                {
                    ContinuationToken = continuationToken,
                    HasMoreResults = hasMoreResults
                };

                queryResponse.InitializeResource(stream, jsonSerializer);
                return queryResponse;
            }
        }

        internal static CosmosQueryResponse<TInput> CreateResponse<TInput>(
            IEnumerable<TInput> resources,
            string continuationToken,
            bool hasMoreResults)
        {
            CosmosQueryResponse<TInput> queryResponse = new CosmosQueryResponse<TInput>();
            queryResponse.SetProperties(resources, continuationToken, hasMoreResults);
            return queryResponse;
        }

        private void InitializeResource(
            Stream stream,
            CosmosJsonSerializer jsonSerializer)
        {
            this.Resources = jsonSerializer.FromStream<CosmosFeedResponse<T>>(stream).Data;
        }

        private void SetProperties(
            IEnumerable<T> resources,
            string continuationToken,
            bool hasMoreResults)
        {
            this.ContinuationToken = continuationToken;
            this.Resources = resources;
            this.HasMoreResults = hasMoreResults;
        }

        internal bool GetHasMoreResults()
        {
            return this.HasMoreResults;
        }
    }
}