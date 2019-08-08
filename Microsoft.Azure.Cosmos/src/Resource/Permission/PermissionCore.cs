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
    internal class PermissionCore : Permission
    {
        /// <summary>
        /// Only used for unit testing
        /// </summary>
        internal PermissionCore()
        {
        }

        internal PermissionCore(
            CosmosClientContext clientContext,
            UserCore user,
            string userId)
        {
            this.Id = userId;
            this.ClientContext = clientContext;
            this.LinkUri = clientContext.CreateLink(
                parentLink: user.LinkUri.OriginalString,
                uriPathSegment: Paths.PermissionsPathSegment,
                id: userId);

            this.User = user;
        }

        /// <inheritdoc/>
        public override string Id { get; }

        /// <summary>
        /// Returns a reference to a user object. 
        /// </summary>
        public User User { get; }

        internal virtual Uri LinkUri { get; }

        internal virtual CosmosClientContext ClientContext { get; }

        /// <inheritdoc/>
        public override Task<PermissionResponse> DeleteAsync(RequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<ResponseMessage> response = this.DeletePermissionStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponseAsync(this, response);
        }

        public Task<ResponseMessage> DeletePermissionStreamAsync(RequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
               streamPayload: null,
               operationType: OperationType.Delete,
               requestOptions: requestOptions,
               cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override Task<PermissionResponse> ReadAsync(int? resourceTokenExpirySeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<ResponseMessage> response = this.ReadPermissionStreamAsync(
                resourceTokenExpirySeconds: resourceTokenExpirySeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponseAsync(this, response);
        }

        public Task<ResponseMessage> ReadPermissionStreamAsync(int? resourceTokenExpirySeconds = null,
            RequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                resourceTokenExpirySeconds: resourceTokenExpirySeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override Task<PermissionResponse> ReplaceAsync(PermissionProperties permissionProperties,
            int? resourceTokenExpirySeconds = null,
            RequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);
            Task<ResponseMessage> response = this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.PropertiesSerializer.ToStream(permissionProperties),
                resourceTokenExpirySeconds: resourceTokenExpirySeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponseAsync(this, response);
        }

        public Task<ResponseMessage> ReplacePermissionStreamAsync(PermissionProperties permissionProperties, 
            RequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);
            return this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.PropertiesSerializer.ToStream(permissionProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ReplaceStreamInternalAsync(
            Stream streamPayload,
            int? resourceTokenExpirySeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
                streamPayload: streamPayload,
                operationType: OperationType.Replace,
                resourceTokenExpirySeconds: resourceTokenExpirySeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            int? resourceTokenExpirySeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessResourceOperationStreamAsync(
                streamPayload: streamPayload,
                operationType: operationType,
                linkUri: this.LinkUri,
                resourceType: ResourceType.Permission,
                resourceTokenExpirySeconds: resourceTokenExpirySeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessResourceOperationStreamAsync(
           Stream streamPayload,
           OperationType operationType,
           Uri linkUri,
           ResourceType resourceType,
           int? resourceTokenExpirySeconds = null,
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
              requestEnricher: (requestMessage) =>
              {
                  if (resourceTokenExpirySeconds.HasValue)
                  {
                      requestMessage.Headers.Add(HttpConstants.HttpHeaders.ResourceTokenExpiry, resourceTokenExpirySeconds.Value.ToString());
                  }
              },
              cancellationToken: cancellationToken);
        }
    }
}
