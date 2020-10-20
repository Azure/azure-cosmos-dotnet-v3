//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Provides interface for historical change feed.
    /// </summary>
    /// <typeparam name="TResource">Source Resource Type (e.g. Document)</typeparam>
    internal sealed class ChangeFeedQuery<TResource> : IDocumentQuery<TResource>
        where TResource : new()
    {
        #region Fields
        private const string IfNoneMatchAllHeaderValue = "*";   // This means start from current.
        private readonly ResourceType resourceType;
        private readonly DocumentClient client;
        private readonly string resourceLink;
        private readonly ChangeFeedOptions feedOptions;
        private readonly string ifModifiedSince;
        private HttpStatusCode lastStatusCode = HttpStatusCode.OK;
        private string nextIfNoneMatch;
        
        #endregion Fields

        #region Constructor
        public ChangeFeedQuery(DocumentClient client, ResourceType resourceType, string resourceLink, ChangeFeedOptions feedOptions)
        {
            Debug.Assert(client != null);

            this.client = client;
            this.resourceType = resourceType;
            this.resourceLink = resourceLink;
            this.feedOptions = feedOptions ?? new ChangeFeedOptions();

            if (feedOptions.PartitionKey != null && !string.IsNullOrEmpty(feedOptions.PartitionKeyRangeId))
            {
                throw new ArgumentException(RMResources.PartitionKeyAndPartitionKeyRangeRangeIdBothSpecified, "feedOptions");
            }

            bool canUseStartFromBeginning = true;
            if (feedOptions.RequestContinuation != null)
            {
                this.nextIfNoneMatch = feedOptions.RequestContinuation;
                canUseStartFromBeginning = false;
            }

            if (feedOptions.StartTime.HasValue)
            {
                this.ifModifiedSince = this.ConvertToHttpTime(feedOptions.StartTime.Value);
                canUseStartFromBeginning = false;
            }

            if (canUseStartFromBeginning && !feedOptions.StartFromBeginning)
            {
                this.nextIfNoneMatch = IfNoneMatchAllHeaderValue;
            }
        }
        #endregion Constructor

        #region IDisposable
        public void Dispose()
        {
        }
        #endregion IDisposable

        #region IDocumentQuery<TResource>
        /// <summary>
        /// Gets a value indicating whether there are potentially additional results that can be retrieved.
        /// </summary>
        /// <value>Boolean value representing if whether there are potentially additional results that can be retrieved.</value>
        /// <remarks>Initially returns true. This value is set based on whether the last execution returned a continuation token.</remarks>
        public bool HasMoreResults => this.lastStatusCode != HttpStatusCode.NotModified;

        /// <summary>
        /// Read feed and retrieves the next page of results in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="TResult">The type of the object returned in the query result.</typeparam>
        /// <returns>The Task object for the asynchronous response from query execution.</returns>
        public Task<DocumentFeedResponse<TResult>> ExecuteNextAsync<TResult>(CancellationToken cancellationToken = default)
        {
            return this.ReadDocumentChangeFeedAsync<TResult>(this.resourceLink, cancellationToken);
        }

        /// <summary>
        /// Executes the query and retrieves the next page of results as dynamic objects in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="cancellationToken">(Optional) The <see cref="CancellationToken"/> allows for notification that operations should be cancelled.</param>
        /// <returns>The Task object for the asynchronous response from query execution.</returns>
        public Task<DocumentFeedResponse<dynamic>> ExecuteNextAsync(CancellationToken cancellationToken = default)
        {
            return this.ExecuteNextAsync<dynamic>(cancellationToken);
        }
        #endregion IDocumentQuery<TResource>

        #region Private
        public Task<DocumentFeedResponse<TResult>> ReadDocumentChangeFeedAsync<TResult>(string resourceLink, CancellationToken cancellationToken)
        {
            IDocumentClientRetryPolicy retryPolicy = this.client.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadDocumentChangeFeedPrivateAsync<TResult>(resourceLink, retryPolicy, cancellationToken), retryPolicy, cancellationToken);
        }

        private async Task<DocumentFeedResponse<TResult>> ReadDocumentChangeFeedPrivateAsync<TResult>(string link, IDocumentClientRetryPolicy retryPolicyInstance, CancellationToken cancellationToken)
        {
            using (DocumentServiceResponse response = await this.GetFeedResponseAsync(link, this.resourceType, retryPolicyInstance, cancellationToken))
            {
                this.lastStatusCode = response.StatusCode;
                this.nextIfNoneMatch = response.Headers[HttpConstants.HttpHeaders.ETag];
                if (response.ResponseBody != null && response.ResponseBody.Length > 0)
                {
                    long responseLengthInBytes = response.ResponseBody.Length;
                    IEnumerable<dynamic> feedResource = response.GetQueryResponse(typeof(TResource), out int itemCount);
                    DocumentFeedResponse<dynamic> feedResponse = new DocumentFeedResponse<dynamic>(
                        feedResource,
                        itemCount,
                        response.Headers,
                        true,
                        null,
                        response.RequestStats,
                        responseLengthBytes: responseLengthInBytes);
                    return (dynamic)feedResponse;
                }
                else
                {
                    return new DocumentFeedResponse<TResult>(
                        Enumerable.Empty<TResult>(),
                        0,
                        response.Headers,
                        true,
                        null,
                        response.RequestStats);
                }
            }
        }

        private async Task<DocumentServiceResponse> GetFeedResponseAsync(string resourceLink, ResourceType resourceType, IDocumentClientRetryPolicy retryPolicyInstance, CancellationToken cancellationToken)
        {
            INameValueCollection headers = new StoreResponseNameValueCollection();

            if (this.feedOptions.MaxItemCount.HasValue)
            {
                headers.Set(HttpConstants.HttpHeaders.PageSize, this.feedOptions.MaxItemCount.ToString());
            }

            if (this.feedOptions.SessionToken != null)
            {
                headers.Set(HttpConstants.HttpHeaders.SessionToken, this.feedOptions.SessionToken);
            }

            if (resourceType.IsPartitioned() && this.feedOptions.PartitionKeyRangeId == null && this.feedOptions.PartitionKey == null)
            {
                throw new ForbiddenException(RMResources.PartitionKeyRangeIdOrPartitionKeyMustBeSpecified);
            }

            // On REST level, change feed is using IfNoneMatch/ETag instead of continuation.
            if (this.nextIfNoneMatch != null)
            {
                headers.Set(HttpConstants.HttpHeaders.IfNoneMatch, this.nextIfNoneMatch);
            }

            if (this.ifModifiedSince != null)
            {
                headers.Set(HttpConstants.HttpHeaders.IfModifiedSince, this.ifModifiedSince);
            }

            headers.Set(HttpConstants.HttpHeaders.A_IM, HttpConstants.A_IMHeaderValues.IncrementalFeed);

            if (this.feedOptions.PartitionKey != null)
            {
                PartitionKeyInternal partitionKey = this.feedOptions.PartitionKey.InternalKey;
                headers.Set(HttpConstants.HttpHeaders.PartitionKey, partitionKey.ToJsonString());
            }

            if (this.feedOptions.IncludeTentativeWrites)
            {
                headers.Set(HttpConstants.HttpHeaders.IncludeTentativeWrites, bool.TrueString);
            }

            using (DocumentServiceRequest request = this.client.CreateDocumentServiceRequest(
                OperationType.ReadFeed,
                resourceLink,
                resourceType,
                headers))
            {
                if (resourceType.IsPartitioned() && this.feedOptions.PartitionKeyRangeId != null)
                {
                    request.RouteTo(new PartitionKeyRangeIdentity(this.feedOptions.PartitionKeyRangeId));
                }

                return await this.client.ReadFeedAsync(request, retryPolicyInstance, cancellationToken);
            }
        }

        private string ConvertToHttpTime(DateTime time)
        {
            return time.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
        }
        #endregion Private
    }
}
