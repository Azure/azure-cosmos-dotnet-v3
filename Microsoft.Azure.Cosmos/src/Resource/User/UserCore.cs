//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing user by id.
    /// 
    /// <see cref="Cosmos.User"/> for creating new users, and reading/querying all user;
    /// </summary>
    internal class UserCore : User
    {
        internal UserCore(
            CosmosClientContext clientContext,
            DatabaseInternal database,
            string userId)
        {
            this.Id = userId;
            this.ClientContext = clientContext;
            this.LinkUri = clientContext.CreateLink(
                parentLink: database.LinkUri,
                uriPathSegment: Paths.UsersPathSegment,
                id: userId);

            this.Database = database;
        }

        /// <inheritdoc/>
        public override string Id { get; }

        /// <summary>
        /// Returns a reference to a database object. 
        /// </summary>
        public Database Database { get; }

        internal virtual string LinkUri { get; }

        internal virtual CosmosClientContext ClientContext { get; }

        /// <inheritdoc/>
        public override async Task<UserResponse> ReadAsync(RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ResponseMessage response = await this.ReadStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponse(this, response);
        }

        public Task<ResponseMessage> ReadStreamAsync(RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task<UserResponse> ReplaceAsync(UserProperties userProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (userProperties == null)
            {
                throw new ArgumentNullException(nameof(userProperties));
            }

            this.ClientContext.ValidateResource(userProperties.Id);
            ResponseMessage response = await this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(userProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponse(this, response);
        }

        public Task<ResponseMessage> ReplaceStreamAsync(UserProperties userProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (userProperties == null)
            {
                throw new ArgumentNullException(nameof(userProperties));
            }

            this.ClientContext.ValidateResource(userProperties.Id);
            return this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(userProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task<UserResponse> DeleteAsync(RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ResponseMessage response = await this.DeleteStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponse(this, response);
        }

        public Task<ResponseMessage> DeleteStreamAsync(RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override Permission GetPermission(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return new PermissionInlineCore(
                    this.ClientContext,
                    this,
                    id);
        }

        /// <inheritdoc/>
        public override async Task<PermissionResponse> CreatePermissionAsync(PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);

            ResponseMessage response = await this.CreatePermissionStreamInternalAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(permissionProperties),
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponse(this.GetPermission(permissionProperties.Id), response);
        }

        public Task<ResponseMessage> CreatePermissionStreamAsync(PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);

            Stream streamPayload = this.ClientContext.SerializerCore.ToStream(permissionProperties);
            return this.CreatePermissionStreamInternalAsync(streamPayload,
                tokenExpiryInSeconds,
                requestOptions,
                cancellationToken);
        }

        public override async Task<PermissionResponse> UpsertPermissionAsync(PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);

            ResponseMessage response = await this.UpsertPermissionStreamInternalAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(permissionProperties),
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponse(this.GetPermission(permissionProperties.Id), response);
        }

        /// <inheritdoc/>
        public override FeedIterator<T> GetPermissionQueryIterator<T>(QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            if (!(this.GetPermissionQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions) is FeedIteratorInternal permissionStreamIterator))
            {
                throw new InvalidOperationException($"Expected a FeedIteratorInternal.");
            }

            return new FeedIteratorCore<T>(
                permissionStreamIterator,
                (response) => this.ClientContext.ResponseFactory.CreateQueryFeedResponse<T>(
                    responseMessage: response,
                    resourceType: ResourceType.Permission));
        }

        public FeedIterator GetPermissionQueryStreamIterator(QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
               clientContext: this.ClientContext,
               this.LinkUri,
               resourceType: ResourceType.Permission,
               queryDefinition: queryDefinition,
               continuationToken: continuationToken,
               options: requestOptions);
        }

        public override FeedIterator<T> GetPermissionQueryIterator<T>(string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetPermissionQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public FeedIterator GetPermissionQueryStreamIterator(string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetPermissionQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        internal Task<ResponseMessage> ProcessPermissionCreateAsync(
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
               diagnosticsContext: null,
               cancellationToken: cancellationToken);
        }

        internal Task<ResponseMessage> ProcessPermissionUpsertAsync(
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
               diagnosticsContext: null,
               cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ReplaceStreamInternalAsync(
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

        private Task<ResponseMessage> ProcessStreamAsync(
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

        private Task<ResponseMessage> ProcessResourceOperationStreamAsync(
           Stream streamPayload,
           OperationType operationType,
           string linkUri,
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
              diagnosticsContext: null,
              cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> CreatePermissionStreamInternalAsync(
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

        private Task<ResponseMessage> UpsertPermissionStreamInternalAsync(
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
    }
}
