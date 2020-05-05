//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing user by id.
    /// 
    /// <see cref="Cosmos.CosmosUser"/> for creating new users, and reading/querying all user;
    /// </summary>
    internal class UserCore : CosmosUser
    {
        /// <summary>
        /// Only used for unit testing
        /// </summary>
        internal UserCore()
        {
        }

        internal UserCore(
            CosmosClientContext clientContext,
            DatabaseCore database,
            string userId)
        {
            this.Id = userId;
            this.ClientContext = clientContext;
            this.LinkUri = clientContext.CreateLink(
                parentLink: database.LinkUri.OriginalString,
                uriPathSegment: Paths.UsersPathSegment,
                id: userId);

            this.Database = database;
        }

        /// <inheritdoc/>
        public override string Id { get; }

        /// <summary>
        /// Returns a reference to a database object. 
        /// </summary>
        public CosmosDatabase Database { get; }

        internal virtual Uri LinkUri { get; }

        internal virtual CosmosClientContext ClientContext { get; }

        /// <inheritdoc/>
        public override Task<UserResponse> ReadAsync(RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<Response> response = this.ReadStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponseAsync(this, response, cancellationToken);
        }

        public Task<Response> ReadStreamAsync(RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override Task<UserResponse> ReplaceAsync(UserProperties userProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (userProperties == null)
            {
                throw new ArgumentNullException(nameof(userProperties));
            }

            this.ClientContext.ValidateResource(userProperties.Id);
            Task<Response> response = this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.PropertiesSerializer.ToStream(userProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponseAsync(this, response, cancellationToken);
        }

        public Task<Response> ReplaceStreamAsync(UserProperties userProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (userProperties == null)
            {
                throw new ArgumentNullException(nameof(userProperties));
            }

            this.ClientContext.ValidateResource(userProperties.Id);
            return this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.PropertiesSerializer.ToStream(userProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override Task<UserResponse> DeleteAsync(RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<Response> response = this.DeleteStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponseAsync(this, response, cancellationToken);
        }

        public Task<Response> DeleteStreamAsync(RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
               streamPayload: null,
               operationType: OperationType.Delete,
               requestOptions: requestOptions,
               cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override CosmosPermission GetPermission(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return new PermissionCore(
                    this.ClientContext,
                    this,
                    id);
        }

        /// <inheritdoc/>
        public override Task<PermissionResponse> CreatePermissionAsync(PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);

            Task<Response> response = this.CreatePermissionStreamInternalAsync(
                streamPayload: this.ClientContext.PropertiesSerializer.ToStream(permissionProperties),
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponseAsync(this.GetPermission(permissionProperties.Id), response, cancellationToken);
        }

        public Task<Response> CreatePermissionStreamAsync(PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);

            Stream streamPayload = this.ClientContext.PropertiesSerializer.ToStream(permissionProperties);
            return this.CreatePermissionStreamInternalAsync(streamPayload,
                tokenExpiryInSeconds,
                requestOptions,
                cancellationToken);
        }

        public override Task<PermissionResponse> UpsertPermissionAsync(PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);

            Task<Response> response = this.UpsertPermissionStreamInternalAsync(
                streamPayload: this.ClientContext.PropertiesSerializer.ToStream(permissionProperties),
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponseAsync(this.GetPermission(permissionProperties.Id), response, cancellationToken);
        }

        public override AsyncPageable<T> GetPermissionQueryResultsAsync<T>(QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            FeedIterator permissionStreamIterator = this.GetPermissionFeedIterator(
                queryDefinition,
                continuationToken,
                requestOptions);

            PageIteratorCore<T> pageIterator = new PageIteratorCore<T>(
                feedIterator: permissionStreamIterator,
                responseCreator: this.ClientContext.ResponseFactory.CreateQueryFeedResponseWithPropertySerializer<T>);

            return PageResponseEnumerator.CreateAsyncPageable(continuation => pageIterator.GetPageAsync(continuation, cancellationToken));
        }

        public async IAsyncEnumerable<Response> GetPermissionQueryStreamResultsAsync(QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
        {
            FeedIterator permissionStreamIterator = this.GetPermissionFeedIterator(
                queryDefinition,
                continuationToken,
                requestOptions);

            while (permissionStreamIterator.HasMoreResults)
            {
                yield return await permissionStreamIterator.ReadNextAsync(cancellationToken);
            }
        }

        public override AsyncPageable<T> GetPermissionQueryResultsAsync<T>(string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetPermissionQueryResultsAsync<T>(
                queryDefinition,
                continuationToken,
                requestOptions,
                cancellationToken);
        }

        public IAsyncEnumerable<Response> GetPermissionQueryStreamResultsAsync(string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetPermissionQueryStreamResultsAsync(
                queryDefinition,
                continuationToken,
                requestOptions,
                cancellationToken);
        }

        internal Task<Response> ProcessPermissionCreateAsync(
            Stream streamPayload,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.LinkUri,
               resourceType: ResourceType.Permission,
               operationType: OperationType.Create,
               cosmosContainerCore: null,
               partitionKey: null,
               streamPayload: streamPayload,
               requestOptions: requestOptions,
               requestEnricher: (requestMessage) =>
               {
                   if (tokenExpiryInSeconds.HasValue)
                   {
                       requestMessage.Headers.Add(HttpConstants.HttpHeaders.ResourceTokenExpiry, tokenExpiryInSeconds.Value.ToString());
                   }
               },
               cancellationToken: cancellationToken);
        }

        internal Task<Response> ProcessPermissionUpsertAsync(
            Stream streamPayload,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.LinkUri,
               resourceType: ResourceType.Permission,
               operationType: OperationType.Upsert,
               cosmosContainerCore: null,
               partitionKey: null,
               streamPayload: streamPayload,
               requestOptions: requestOptions,
               requestEnricher: (requestMessage) =>
               {
                   if (tokenExpiryInSeconds.HasValue)
                   {
                       requestMessage.Headers.Add(HttpConstants.HttpHeaders.ResourceTokenExpiry, tokenExpiryInSeconds.Value.ToString());
                   }
               },
               cancellationToken: cancellationToken);
        }

        private Task<Response> ReplaceStreamInternalAsync(
            Stream streamPayload,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
                streamPayload: streamPayload,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<Response> ProcessStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessResourceOperationStreamAsync(
                streamPayload: streamPayload,
                operationType: operationType,
                linkUri: this.LinkUri,
                resourceType: ResourceType.User,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<Response> ProcessResourceOperationStreamAsync(
           Stream streamPayload,
           OperationType operationType,
           Uri linkUri,
           ResourceType resourceType,
           RequestOptions requestOptions = null,
           CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
              resourceUri: linkUri,
              resourceType: resourceType,
              operationType: operationType,
              cosmosContainerCore: null,
              partitionKey: null,
              streamPayload: streamPayload,
              requestOptions: requestOptions,
              requestEnricher: null,
              cancellationToken: cancellationToken);
        }

        private Task<Response> CreatePermissionStreamInternalAsync(
            Stream streamPayload,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessPermissionCreateAsync(
                streamPayload: streamPayload,
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<Response> UpsertPermissionStreamInternalAsync(
            Stream streamPayload,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessPermissionUpsertAsync(
                streamPayload: streamPayload,
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private FeedIterator GetPermissionFeedIterator(QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
               this.ClientContext,
               this.LinkUri,
               ResourceType.Permission,
               queryDefinition,
               continuationToken,
               requestOptions);
        }
    }
}
