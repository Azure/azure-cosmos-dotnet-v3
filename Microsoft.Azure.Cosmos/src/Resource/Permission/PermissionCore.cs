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
        private readonly Uri linkUri;
        private readonly CosmosClientContext clientContext;

        internal PermissionCore(
            CosmosClientContext clientContext,
            UserCore user,
            string userId)
        {
            this.Id = userId;
            this.clientContext = clientContext;
            this.linkUri = clientContext.CreateLink(
                parentLink: user.LinkUri.OriginalString,
                uriPathSegment: Paths.PermissionsPathSegment,
                id: userId);
        }

        /// <inheritdoc/>
        public override string Id { get; }

        /// <inheritdoc/>
        public override async Task<PermissionResponse> DeleteAsync(RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ResponseMessage response = await this.DeletePermissionStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreatePermissionResponse(this, response);
        }

        public Task<ResponseMessage> DeletePermissionStreamAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task<PermissionResponse> ReadAsync(int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ResponseMessage response = await this.ReadPermissionStreamAsync(
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreatePermissionResponse(this, response);
        }

        public Task<ResponseMessage> ReadPermissionStreamAsync(int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task<PermissionResponse> ReplaceAsync(PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.clientContext.ValidateResource(permissionProperties.Id);
            ResponseMessage response = await this.ReplaceStreamInternalAsync(
                streamPayload: this.clientContext.SerializerCore.ToStream(permissionProperties),
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreatePermissionResponse(this, response);
        }

        public Task<ResponseMessage> ReplacePermissionStreamAsync(PermissionProperties permissionProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.clientContext.ValidateResource(permissionProperties.Id);
            return this.ReplaceStreamInternalAsync(
                streamPayload: this.clientContext.SerializerCore.ToStream(permissionProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ReplaceStreamInternalAsync(
            Stream streamPayload,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
                streamPayload: streamPayload,
                operationType: OperationType.Replace,
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessResourceOperationStreamAsync(
                streamPayload: streamPayload,
                operationType: operationType,
                linkUriString: this.linkUri.OriginalString,
                resourceType: ResourceType.Permission,
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessResourceOperationStreamAsync(
           Stream streamPayload,
           OperationType operationType,
           string linkUriString,
           ResourceType resourceType,
           int? tokenExpiryInSeconds = null,
           RequestOptions requestOptions = null,
           CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.clientContext.ProcessResourceOperationStreamAsync(
              resourceUri: linkUriString,
              resourceType: resourceType,
              operationType: operationType,
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
    }
}
