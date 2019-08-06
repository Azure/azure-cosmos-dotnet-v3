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
        public Database Database { get; }

        internal virtual Uri LinkUri { get; }

        internal virtual CosmosClientContext ClientContext { get; }

        /// <inheritdoc/>
        public override Task<UserResponse> ReadAsync(RequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<ResponseMessage> response = this.ReadStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponseAsync(this, response);
        }

        /// <inheritdoc/>
        public override Task<ResponseMessage> ReadStreamAsync(RequestOptions requestOptions = null, 
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
            Task<ResponseMessage> response = this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.PropertiesSerializer.ToStream(userProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponseAsync(this, response);
        }

        /// <inheritdoc/>
        public override Task<ResponseMessage> ReplaceStreamAsync(UserProperties userProperties, 
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
            Task<ResponseMessage> response = this.DeleteStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponseAsync(this, response);
        }

        /// <inheritdoc/>
        public override Task<ResponseMessage> DeleteStreamAsync(RequestOptions requestOptions = null, 
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

            return new PermissionCore(
                    this.ClientContext,
                    this,
                    id);
        }

        /// <inheritdoc/>
        public override Task<PermissionResponse> CreatePermissionAsync(PermissionProperties permissionProperties, 
            PermissionRequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);

            Task<ResponseMessage> response = this.CreatePermissionStreamInternalAsync(
                streamPayload: this.ClientContext.PropertiesSerializer.ToStream(permissionProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponseAsync(this.GetPermission(permissionProperties.Id), response);
        }

        /// <inheritdoc/>
        public override Task<ResponseMessage> CreatePermissionStreamAsync(PermissionProperties permissionProperties, 
            PermissionRequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);

            Stream streamPayload = this.ClientContext.PropertiesSerializer.ToStream(permissionProperties);
            return this.CreatePermissionStreamInternalAsync(streamPayload,
                requestOptions,
                cancellationToken);
        }

        /// <inheritdoc/>
        public override FeedIterator<T> GetPermissionQueryIterator<T>(QueryDefinition queryDefinition, 
            string continuationToken = null, 
            QueryRequestOptions requestOptions = null)
        {
            FeedIterator permissionStreamIterator = this.GetPermissionQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions);

            return new FeedIteratorCore<T>(
                permissionStreamIterator,
                this.ClientContext.ResponseFactory.CreateQueryFeedResponse<T>);
        }

        /// <inheritdoc/>
        public override FeedIterator GetPermissionQueryStreamIterator(QueryDefinition queryDefinition, string continuationToken = null, QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
               this.ClientContext,
               this.LinkUri,
               ResourceType.Permission,
               queryDefinition,
               continuationToken,
               requestOptions);
        }

        /// <inheritdoc/>
        public override FeedIterator<T> GetPermissionQueryIterator<T>(string queryText = null, string continuationToken = null, QueryRequestOptions requestOptions = null)
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

        /// <inheritdoc/>
        public override FeedIterator GetPermissionQueryStreamIterator(string queryText = null, string continuationToken = null, QueryRequestOptions requestOptions = null)
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
            PermissionRequestOptions requestOptions = null,
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
               requestEnricher: null,
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
            return ProcessResourceOperationStreamAsync(
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

        private Task<ResponseMessage> CreatePermissionStreamInternalAsync(
            Stream streamPayload,
            PermissionRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessPermissionCreateAsync(
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }
    }
}
