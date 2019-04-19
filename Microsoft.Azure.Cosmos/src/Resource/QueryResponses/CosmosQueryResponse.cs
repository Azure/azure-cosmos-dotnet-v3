//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents the template class used by feed methods (enumeration operations) for the Azure Cosmos DB service.
    /// </summary>
    public class CosmosQueryResponse
    {
        internal readonly string disallowContinuationTokenMessage;

        /// <summary>
        /// Constructor exposed for mocking purposes.
        /// </summary>
        public CosmosQueryResponse()
        {
        }

        internal CosmosQueryResponse(
            IEnumerable<CosmosElement> result,
            int count,
            CosmosResponseMessageHeaders responseHeaders,
            bool useETagAsContinuation = false,
            string disallowContinuationTokenMessage = null,
            long responseLengthBytes = 0)
        {
            this.StatusCode = HttpStatusCode.Accepted;
            this.IsSuccess = true;
            this.CosmosElements = result;
            this.Count = count;
            this.UseETagAsContinuation = useETagAsContinuation;
            this.disallowContinuationTokenMessage = disallowContinuationTokenMessage;
            this.ResponseLengthBytes = responseLengthBytes;
            this.Headers = responseHeaders;
        }

        internal CosmosQueryResponse(
            CosmosResponseMessageHeaders responseHeaders,
            HttpStatusCode statusCode,
            string errorMessage,
            Error error)
        {
            this.StatusCode = statusCode;
            this.IsSuccess = false;
            this.ErrorMessage = errorMessage;
            this.Error = error;
            this.CosmosElements = Enumerable.Empty<CosmosElement>();
            this.Count = 0;
            this.disallowContinuationTokenMessage = null;
            this.ResponseLengthBytes = 0;
            this.Headers = responseHeaders;
        }

        /// <summary>
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in reqest units.
        /// </value>
        public virtual double RequestCharge => this.Headers.RequestCharge;

        /// <summary>
        /// Gets the activity ID for the request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        public virtual string ActivityId => this.Headers[HttpConstants.HttpHeaders.ActivityId];

        /// <summary>
        /// Gets the continuation token to be used for continuing enumeration of the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The continuation token to be used for continuing enumeration.
        /// </value>
        public virtual string ResponseContinuation
        {
            get
            {
                if (this.disallowContinuationTokenMessage != null)
                {
                    throw new ArgumentException(this.disallowContinuationTokenMessage);
                }

                return this.InternalResponseContinuation;
            }

            internal set
            {
                if (this.disallowContinuationTokenMessage != null)
                {
                    throw new ArgumentException(this.disallowContinuationTokenMessage);
                }

                Debug.Assert(!this.UseETagAsContinuation);
                this.Headers.Continuation = value;
            }
        }

        /// <summary>
        /// Gets the session token for use in sesssion consistency reads from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The session token for use in session consistency.
        /// </value>
        public virtual string SessionToken => this.Headers[HttpConstants.HttpHeaders.SessionToken];

        /// <summary>
        /// Gets the content parent location, for example, dbs/foo/colls/bar, from the Azure Cosmos DB service.
        /// </summary>
        public virtual string ContentLocation => this.Headers[HttpConstants.HttpHeaders.OwnerFullName];

        /// <summary>
        /// Gets the entity tag associated with last transaction in the Azure Cosmos DB service,
        /// which can be used as If-Non-Match Access condition for ReadFeed REST request or 
        /// ContinuationToken property of <see cref="ChangeFeedOptions"/> parameter for
        /// <see cref="DocumentClient.CreateDocumentChangeFeedQuery(string, ChangeFeedOptions)"/> 
        /// to get feed changes since the transaction specified by this entity tag.
        /// </summary>
        public virtual string ETag => this.Headers.ETag;

        /// <summary>
        /// The headers of the response
        /// </summary>
        public virtual CosmosResponseMessageHeaders Headers { get; }

        /// <summary>
        /// The number of items in the stream.
        /// </summary>
        public virtual int Count { get; }

        /// <summary>
        /// Gets the <see cref="HttpStatusCode"/> of the current response.
        /// </summary>
        public virtual HttpStatusCode StatusCode { get; private set; }

        /// <summary>
        /// The exception if the operation failed.
        /// </summary>
        public virtual string ErrorMessage { get; }

        /// <summary>
        /// The stream containing the content
        /// </summary>
        public Stream Content => this.GetStream();

        /// <summary>
        /// Asserts if the current <see cref="HttpStatusCode"/> is a success.
        /// </summary>
        public virtual bool IsSuccess { get; }

        internal virtual IEnumerable<CosmosElement> CosmosElements { get; }

        internal virtual Error Error { get; }

        /// <summary>
        /// Gets the response length in bytes
        /// </summary>
        /// <remarks>
        /// This value is only set for Direct mode.
        /// </remarks>
        internal long ResponseLengthBytes { get; private set; }

        /// <summary>
        /// Get the client side request statistics for the current request.
        /// </summary>
        /// <remarks>
        /// This value is currently used for tracking replica Uris.
        /// </remarks>
        internal ClientSideRequestStatistics RequestStatistics { get; private set; }

        internal virtual CosmosSerializationOptions CosmosSerializationOptions { get; set; }

        /// <summary>
        /// Gets the continuation token to be used for continuing enumeration of the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The continuation token to be used for continuing enumeration.
        /// </value>
        internal string InternalResponseContinuation => this.UseETagAsContinuation ?
                    this.ETag :
                    this.Headers.Continuation;

        // This is used by CosmosElementResponseBinder.
        internal bool UseETagAsContinuation { get; }

        internal string DisallowContinuationTokenMessage => this.disallowContinuationTokenMessage;

        /// <summary>
        /// Checks if the current <see cref="CosmosResponseMessage"/> has a successful status code, otherwise, throws.
        /// </summary>
        /// <exception cref="CosmosException"></exception>
        /// <returns>The current <see cref="CosmosResponseMessage"/>.</returns>
        internal virtual CosmosQueryResponse EnsureSuccessStatusCode()
        {
            if (!this.IsSuccess)
            {
                string message = $"Response status code does not indicate success: {(int)this.StatusCode} Substatus: {(int)this.Headers.SubStatusCode} Reason: ({this.ErrorMessage}).";

                throw new CosmosException(
                    message: message,
                    statusCode: this.StatusCode,
                    subStatusCode: (int)this.Headers.SubStatusCode,
                    activityId: this.ActivityId,
                    requestCharge: this.RequestCharge);
            }

            return this;
        }

        internal bool GetHasMoreResults()
        {
            return !string.IsNullOrEmpty(this.ResponseContinuation);
        }

        private Stream GetStream()
        {
            IJsonWriter jsonWriter;
            if (this.CosmosSerializationOptions != null)
            {
                jsonWriter = this.CosmosSerializationOptions.CreateCustomWriterCallback();
            }
            else
            {
                jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            }

            jsonWriter.WriteArrayStart();

            foreach (CosmosElement cosmosElement in this.CosmosElements)
            {
                cosmosElement.WriteTo(jsonWriter);
            }

            jsonWriter.WriteArrayEnd();

            return new MemoryStream(jsonWriter.GetResult());
        }

        internal IEnumerable<T> Convert<T>(
            ResourceType resourceType,
            CosmosJsonSerializer jsonSerializer)
        {
            // Throw the exception if it exists for type base responses
            this.EnsureSuccessStatusCode();

            if (this.Count == 0)
            {
                return Enumerable.Empty<T>();
            }

            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);

            jsonWriter.WriteArrayStart();

            foreach (CosmosElement cosmosElement in this.CosmosElements)
            {
                cosmosElement.WriteTo(jsonWriter);
            }

            jsonWriter.WriteArrayEnd();
            MemoryStream stream = new MemoryStream(jsonWriter.GetResult());
            IEnumerable<T> typedResults;

            // If the resource type is an offer and the requested type is either a Offer or OfferV2 or dynamic
            // create a OfferV2 object and cast it to T. This is a temporary fix until offers is moved to v3 API. 
            if (resourceType == ResourceType.Offer &&
                (typeof(T).IsSubclassOf(typeof(Resource)) || typeof(T) == typeof(object)))
            {
                typedResults = jsonSerializer.FromStream<List<OfferV2>>(stream).Cast<T>();
            }
            else
            {
                typedResults = jsonSerializer.FromStream<List<T>>(stream);
            }

            return typedResults;
        }
    }

    /// <summary>
    /// The cosmos query response
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CosmosQueryResponse<T> : IEnumerable<T>
    {
        private readonly CosmosResponseMessageHeaders responseHeaders = null;
        private IEnumerable<T> resources;
        private bool hasMoreResults;

        /// <summary>
        /// Create a <see cref="CosmosQueryResponse{T}"/>
        /// </summary>
        protected CosmosQueryResponse(
            CosmosResponseMessageHeaders responseMessageHeaders,
            bool hasMoreResults,
            string continuationToken,
            string disallowContinuationTokenMessage)
        {
            this.responseHeaders = responseMessageHeaders;
            this.hasMoreResults = hasMoreResults;
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
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in request units.
        /// </value>
        public virtual double RequestCharge
        {
            get
            {
                if (this.responseHeaders == null)
                {
                    return 0;
                }

                return this.responseHeaders.RequestCharge;
            }
        }

        /// <summary>
        /// Get the enumerators to iterate through the results
        /// </summary>
        /// <returns>An enumerator of the response objects</returns>
        public virtual IEnumerator<T> GetEnumerator()
        {
            if (this.resources == null)
            {
                return Enumerable.Empty<T>().GetEnumerator();
            }

            return this.resources.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal static CosmosQueryResponse<TInput> CreateResponse<TInput>(
            CosmosResponseMessageHeaders responseMessageHeaders,
            Stream stream,
            CosmosJsonSerializer jsonSerializer,
            string continuationToken,
            bool hasMoreResults)
        {
            using (stream)
            {
                CosmosQueryResponse<TInput> queryResponse = new CosmosQueryResponse<TInput>(
                    responseMessageHeaders: responseMessageHeaders,
                    hasMoreResults: hasMoreResults,
                    continuationToken: continuationToken,
                    disallowContinuationTokenMessage: null);

                queryResponse.InitializeResource(stream, jsonSerializer);
                return queryResponse;
            }
        }

        internal static CosmosQueryResponse<TInput> CreateResponse<TInput>(
            CosmosResponseMessageHeaders responseMessageHeaders,
            IEnumerable<TInput> resources,
            string continuationToken,
            bool hasMoreResults)
        {
            CosmosQueryResponse<TInput> queryResponse = new CosmosQueryResponse<TInput>(
                responseMessageHeaders: responseMessageHeaders,
                hasMoreResults: hasMoreResults,
                continuationToken: continuationToken,
                disallowContinuationTokenMessage: null)
            {
                resources = resources
            };

            return queryResponse;
        }

        private void InitializeResource(
            Stream stream,
            CosmosJsonSerializer jsonSerializer)
        {
            this.resources = jsonSerializer.FromStream<CosmosFeedResponse<T>>(stream).Data;
        }

        internal static CosmosQueryResponse<TInput> CreateResponse<TInput>(
            CosmosQueryResponse cosmosQueryResponse,
            CosmosJsonSerializer jsonSerializer,
            bool hasMoreResults,
            ResourceType resourceType)
        {
            CosmosQueryResponse<TInput> queryResponse = new CosmosQueryResponse<TInput>(
                responseMessageHeaders: cosmosQueryResponse.Headers,
                hasMoreResults: hasMoreResults,
                continuationToken: cosmosQueryResponse.InternalResponseContinuation,
                disallowContinuationTokenMessage: cosmosQueryResponse.DisallowContinuationTokenMessage);

            queryResponse.InitializeResource(cosmosQueryResponse, jsonSerializer, resourceType);
            return queryResponse;
        }

        private void InitializeResource(
            CosmosQueryResponse feedResponse,
            CosmosJsonSerializer jsonSerializer,
            ResourceType resourceType)
        {
            this.resources = feedResponse.Convert<T>(
                resourceType,
                jsonSerializer);
        }

        internal bool GetHasMoreResults()
        {
            return this.hasMoreResults;
        }
    }
}