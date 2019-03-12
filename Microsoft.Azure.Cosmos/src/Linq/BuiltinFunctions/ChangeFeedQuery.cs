//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using System.Diagnostics;
    using System.Net;
    using System.Collections.Generic;
    using System.Linq;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Newtonsoft.Json;
    using System.IO;

    /// <summary>
    /// Provides interface for historical change feed.
    /// </summary>
    /// <typeparam name="TResource">Source Resource Type (e.g. Document)</typeparam>
    internal sealed class ChangeFeedQuery<TResource> : IDocumentQuery<TResource> where TResource : CosmosResource, new()
    {
        #region Fields
        private const string IfNoneMatchAllHeaderValue = "*";   // This means start from current.
        private readonly ResourceType resourceType;
        private readonly DocumentClient client;
        private readonly string resourceLink;
        private readonly ChangeFeedOptions feedOptions;
        private HttpStatusCode lastStatusCode = HttpStatusCode.OK;
        private string nextIfNoneMatch;
        private string ifModifiedSince;
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
                this.ifModifiedSince = ConvertToHttpTime(feedOptions.StartTime.Value);
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
        public bool HasMoreResults
        {
            get
            {
                return this.lastStatusCode != HttpStatusCode.NotModified;
            }
        }

        /// <summary>
        /// Read feed and retrieves the next page of results in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="TResult">The type of the object returned in the query result.</typeparam>
        /// <returns>The Task object for the asynchronous response from query execution.</returns>
        public async Task<FeedResponse<TResult>> ExecuteNextAsync<TResult>(CancellationToken cancellationToken = default(CancellationToken))
        {
            FeedResponse<CosmosElement> feedResponse = await this.ExecuteNextAsync(cancellationToken);
            return FeedResponseBinder.Convert<TResult>(
                feedResponse,
                ContentSerializationFormat.JsonText,
                new JsonSerializerSettings());
        }

        /// <summary>
        /// Executes the query and retrieves the next page of results as dynamic objects in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="cancellationToken">(Optional) The <see cref="CancellationToken"/> allows for notification that operations should be cancelled.</param>
        /// <returns>The Task object for the asynchronous response from query execution.</returns>
        public async Task<FeedResponse<CosmosElement>> ExecuteNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ReadDocumentChangeFeedAsync(
                this.resourceLink, 
                cancellationToken);
        }
        #endregion IDocumentQuery<TResource>

        #region Private
        public Task<FeedResponse<CosmosElement>> ReadDocumentChangeFeedAsync(
            string resourceLink, 
            CancellationToken cancellationToken)
        {
            IDocumentClientRetryPolicy retryPolicy = this.client.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadDocumentChangeFeedPrivateAsync(resourceLink, retryPolicy, cancellationToken), retryPolicy, cancellationToken);
        }

        private async Task<FeedResponse<CosmosElement>> ReadDocumentChangeFeedPrivateAsync(
            string link, 
            IDocumentClientRetryPolicy retryPolicyInstance, 
            CancellationToken cancellationToken)
        {
            using (DocumentServiceResponse response = await this.GetFeedResponseAsync(link, resourceType, retryPolicyInstance, cancellationToken))
            {
                this.lastStatusCode = response.StatusCode;
                this.nextIfNoneMatch = response.Headers[HttpConstants.HttpHeaders.ETag];

                MemoryStream memoryStream = new MemoryStream();
                response.ResponseBody.CopyTo(memoryStream);
                long responseLengthBytes = memoryStream.Length;
                Microsoft.Azure.Cosmos.Json.IJsonNavigator jsonNavigator = Microsoft.Azure.Cosmos.Json.JsonNavigator.Create(memoryStream.ToArray());
                string resourceName = resourceType.ToResourceTypeString() + "s";

                if (!jsonNavigator.TryGetObjectProperty(
                    jsonNavigator.GetRootNode(),
                    resourceName,
                    out Microsoft.Azure.Cosmos.Json.ObjectProperty objectProperty))
                {
                    throw new InvalidOperationException($"Response Body Contract was violated. QueryResponse did not have property: {resourceName}");
                }

                Microsoft.Azure.Cosmos.Json.IJsonNavigatorNode cosmosElements = objectProperty.ValueNode;
                if (!(CosmosElement.Dispatch(
                    jsonNavigator,
                    cosmosElements) is CosmosArray cosmosArray))
                {
                    throw new InvalidOperationException($"QueryResponse did not have an array of : {resourceName}");
                }

                int itemCount = cosmosArray.Count;
                return new FeedResponse<CosmosElement>(
                    cosmosArray,
                    itemCount,
                    response.Headers,
                    response.RequestStats,
                    responseLengthBytes);
            }
        }

        private async Task<DocumentServiceResponse> GetFeedResponseAsync(
            string resourceLink, 
            ResourceType resourceType, 
            IDocumentClientRetryPolicy retryPolicyInstance, 
            CancellationToken cancellationToken)
        {
            INameValueCollection headers = new StringKeyValueCollection();

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
                PartitionKeyInternal partitionKey = feedOptions.PartitionKey.InternalKey;
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
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                if (resourceType.IsPartitioned() && this.feedOptions.PartitionKeyRangeId != null)
                {
                    request.RouteTo(new PartitionKeyRangeIdentity(this.feedOptions.PartitionKeyRangeId));
                }

                return await this.client.ReadFeedAsync(request, cancellationToken);
            }
        }

        private string ConvertToHttpTime(DateTime time)
        {
            return time.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
        }
        #endregion Private
    }
}
