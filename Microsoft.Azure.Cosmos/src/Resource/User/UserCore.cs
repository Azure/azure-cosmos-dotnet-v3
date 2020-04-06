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
    internal sealed class UserCore
    {
        private readonly UserInlineCore user;

        internal UserCore(
            UserInlineCore user,
            CosmosClientContext clientContext,
            DatabaseInlineCore database,
            string userId)
        {
            this.Id = userId;
            this.ClientContext = clientContext;
            this.LinkUri = clientContext.CreateLink(
                parentLink: database.LinkUri.OriginalString,
                uriPathSegment: Paths.UsersPathSegment,
                id: userId);

            this.user = user;
        }

        internal string Id { get; }

        internal Uri LinkUri { get; }

        internal CosmosClientContext ClientContext { get; }

        public Task<UserResponse> ReadAsync(
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Task<ResponseMessage> response = this.ReadStreamAsync(
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponseAsync(this.user, response);
        }

        public Task<ResponseMessage> ReadStreamAsync(
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        public Task<UserResponse> ReplaceAsync(UserProperties userProperties,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (userProperties == null)
            {
                throw new ArgumentNullException(nameof(userProperties));
            }

            this.ClientContext.ValidateResource(userProperties.Id);
            Task<ResponseMessage> response = this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(userProperties),
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponseAsync(this.user, response);
        }

        public Task<ResponseMessage> ReplaceStreamAsync(UserProperties userProperties,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (userProperties == null)
            {
                throw new ArgumentNullException(nameof(userProperties));
            }

            this.ClientContext.ValidateResource(userProperties.Id);
            return this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(userProperties),
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        public Task<UserResponse> DeleteAsync(
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Task<ResponseMessage> response = this.DeleteStreamAsync(
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponseAsync(this.user, response);
        }

        public Task<ResponseMessage> DeleteStreamAsync(
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        public Permission GetPermission(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return new PermissionInlineCore(
                    this.ClientContext,
                    this.user,
                    id);
        }

        public Task<PermissionResponse> CreatePermissionAsync(PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);

            Task<ResponseMessage> response = this.CreatePermissionStreamInternalAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(permissionProperties),
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponseAsync(this.GetPermission(permissionProperties.Id), response);
        }

        public Task<ResponseMessage> CreatePermissionStreamAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
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
                diagnosticsContext: diagnosticsContext,
                cancellationToken);
        }

        public Task<PermissionResponse> UpsertPermissionAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);

            Task<ResponseMessage> response = this.UpsertPermissionStreamInternalAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(permissionProperties),
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponseAsync(this.GetPermission(permissionProperties.Id), response);
        }

        public FeedIterator<T> GetPermissionQueryIterator<T>(
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
            return FeedIteratorCore.CreateForNonPartitionedResource(
               clientContext: this.ClientContext,
               this.LinkUri,
               resourceType: ResourceType.Permission,
               queryDefinition: queryDefinition,
               continuationToken: continuationToken,
               options: requestOptions);
        }

        public FeedIterator<T> GetPermissionQueryIterator<T>(
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
            Stream streamPayload,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
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
            Stream streamPayload,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
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
               diagnosticsContext: diagnosticsContext,
               cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ReplaceStreamInternalAsync(
            Stream streamPayload,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return this.ProcessStreamAsync(
                streamPayload: streamPayload,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return this.ProcessResourceOperationStreamAsync(
                streamPayload: streamPayload,
                operationType: operationType,
                linkUri: this.LinkUri,
                resourceType: ResourceType.User,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessResourceOperationStreamAsync(
           Stream streamPayload,
           OperationType operationType,
           Uri linkUri,
           ResourceType resourceType,
           RequestOptions requestOptions,
           CosmosDiagnosticsContext diagnosticsContext,
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
            Stream streamPayload,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return this.ProcessPermissionCreateAsync(
                streamPayload: streamPayload,
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> UpsertPermissionStreamInternalAsync(
            Stream streamPayload,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return this.ProcessPermissionUpsertAsync(
                streamPayload: streamPayload,
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }
    }
}
