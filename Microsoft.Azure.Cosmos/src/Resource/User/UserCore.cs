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
    internal abstract class UserCore : User
    {
        internal UserCore(
            CosmosClientContext clientContext,
            DatabaseInternal database,
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

        public async Task<UserResponse> ReadAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ReadStreamAsync(
                diagnosticsContext: diagnosticsContext,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponse(this, response);
        }

        public Task<ResponseMessage> ReadStreamAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.ProcessStreamAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public async Task<UserResponse> ReplaceAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            UserProperties userProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (userProperties == null)
            {
                throw new ArgumentNullException(nameof(userProperties));
            }

            this.ClientContext.ValidateResource(userProperties.Id);
            ResponseMessage response = await this.ReplaceStreamInternalAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: this.ClientContext.SerializerCore.ToStream(userProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponse(this, response);
        }

        public Task<ResponseMessage> ReplaceStreamAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            UserProperties userProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (userProperties == null)
            {
                throw new ArgumentNullException(nameof(userProperties));
            }

            this.ClientContext.ValidateResource(userProperties.Id);
            return this.ReplaceStreamInternalAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: this.ClientContext.SerializerCore.ToStream(userProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public async Task<UserResponse> DeleteAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.DeleteStreamAsync(
                diagnosticsContext: diagnosticsContext,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponse(this, response);
        }

        public Task<ResponseMessage> DeleteStreamAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.ProcessStreamAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: null,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

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

        public async Task<PermissionResponse> CreatePermissionAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);

            ResponseMessage response = await this.CreatePermissionStreamInternalAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: this.ClientContext.SerializerCore.ToStream(permissionProperties),
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponse(this.GetPermission(permissionProperties.Id), response);
        }

        public Task<ResponseMessage> CreatePermissionStreamAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);

            Stream streamPayload = this.ClientContext.SerializerCore.ToStream(permissionProperties);
            return this.CreatePermissionStreamInternalAsync(
                diagnosticsContext,
                streamPayload,
                tokenExpiryInSeconds,
                requestOptions,
                cancellationToken);
        }

        public async Task<PermissionResponse> UpsertPermissionAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);

            ResponseMessage response = await this.UpsertPermissionStreamInternalAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: this.ClientContext.SerializerCore.ToStream(permissionProperties),
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponse(this.GetPermission(permissionProperties.Id), response);
        }

        public override FeedIterator<T> GetPermissionQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions)
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

        public FeedIterator GetPermissionQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            return new FeedIteratorCore(
               clientContext: this.ClientContext,
               this.LinkUri,
               resourceType: ResourceType.Permission,
               queryDefinition: queryDefinition,
               continuationToken: continuationToken,
               options: requestOptions);
        }

        public override FeedIterator<T> GetPermissionQueryIterator<T>(
            string queryText,
            string continuationToken,
            QueryRequestOptions requestOptions)
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

        public FeedIterator GetPermissionQueryStreamIterator(
            string queryText,
            string continuationToken,
            QueryRequestOptions requestOptions)
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
            CosmosDiagnosticsContext diagnosticsContext,
            Stream streamPayload,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
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
               diagnosticsContext: diagnosticsContext,
               cancellationToken: cancellationToken);
        }

        internal Task<ResponseMessage> ProcessPermissionUpsertAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            Stream streamPayload,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
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
            CosmosDiagnosticsContext diagnosticsContext,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.ProcessStreamAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: streamPayload,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessStreamAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            Stream streamPayload,
            OperationType operationType,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.ProcessResourceOperationStreamAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: streamPayload,
                operationType: operationType,
                linkUri: this.LinkUri,
                resourceType: ResourceType.User,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessResourceOperationStreamAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            Stream streamPayload,
            OperationType operationType,
            Uri linkUri,
            ResourceType resourceType,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
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
              diagnosticsContext: diagnosticsContext,
              cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> CreatePermissionStreamInternalAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            Stream streamPayload,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.ProcessPermissionCreateAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: streamPayload,
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> UpsertPermissionStreamInternalAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            Stream streamPayload,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.ProcessPermissionUpsertAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: streamPayload,
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }
    }
}
