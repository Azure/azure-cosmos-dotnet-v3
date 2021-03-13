//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing user by id.
    /// 
    /// <see cref="Cosmos.User"/> for creating new users, and reading/querying all user;
    /// </summary>
    internal abstract class PermissionCore : Permission
    {
        private readonly string linkUri;

        internal PermissionCore(
            CosmosClientContext clientContext,
            UserCore user,
            string userId)
        {
            this.Id = userId;
            this.ClientContext = clientContext;
            this.linkUri = clientContext.CreateLink(
                parentLink: user.LinkUri,
                uriPathSegment: Paths.PermissionsPathSegment,
                id: userId);
        }

        /// <inheritdoc/>
        public override string Id { get; }

        internal CosmosClientContext ClientContext { get; }

        public async Task<PermissionResponse> DeleteAsync(
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.DeletePermissionStreamAsync(
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponse(this, response);
        }

        public Task<ResponseMessage> DeletePermissionStreamAsync(
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Delete,
                tokenExpiryInSeconds: null,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<PermissionResponse> ReadAsync(
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ReadPermissionStreamAsync(
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponse(this, response);
        }

        public Task<ResponseMessage> ReadPermissionStreamAsync(
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<PermissionResponse> ReplaceAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);
            ResponseMessage response = await this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(permissionProperties),
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponse(this, response);
        }

        public Task<ResponseMessage> ReplacePermissionStreamAsync(
            PermissionProperties permissionProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (permissionProperties == null)
            {
                throw new ArgumentNullException(nameof(permissionProperties));
            }

            this.ClientContext.ValidateResource(permissionProperties.Id);
            return this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(permissionProperties),
                tokenExpiryInSeconds: null,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ReplaceStreamInternalAsync(
            Stream streamPayload,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.ProcessStreamAsync(
                streamPayload: streamPayload,
                operationType: OperationType.Replace,
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.ProcessResourceOperationStreamAsync(
                streamPayload: streamPayload,
                operationType: operationType,
                linkUri: this.linkUri,
                resourceType: ResourceType.Permission,
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessResourceOperationStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            string linkUri,
            ResourceType resourceType,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
              resourceUri: linkUri,
              resourceType: resourceType,
              operationType: operationType,
              cosmosContainerCore: null,
              feedRange: null,
              streamPayload: streamPayload,
              requestOptions: requestOptions,
              requestEnricher: (requestMessage) =>
              {
                  if (tokenExpiryInSeconds.HasValue)
                  {
                      requestMessage.Headers.Add(HttpConstants.HttpHeaders.ResourceTokenExpiry, tokenExpiryInSeconds.Value.ToString());
                  }
              },
              trace: trace,
              cancellationToken: cancellationToken);
        }
    }
}
