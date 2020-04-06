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
    internal sealed class PermissionCore
    {
        internal PermissionCore(
            Permission permission,
            CosmosClientContext clientContext,
            UserInlineCore user,
            string userId)
        {
            this.Id = userId;
            this.ClientContext = clientContext;
            this.LinkUri = clientContext.CreateLink(
                parentLink: user.LinkUri.OriginalString,
                uriPathSegment: Paths.PermissionsPathSegment,
                id: userId);

            this.Permission = permission;
            this.User = user;
        }

        public string Id { get; }

        /// <summary>
        /// Returns a reference to a user object. 
        /// </summary>
        public UserInlineCore User { get; }

        internal Uri LinkUri { get; }

        internal CosmosClientContext ClientContext { get; }

        /// <summary>
        /// Public instance of permission contract. Used in creating response objects
        /// </summary>
        private Permission Permission { get; }

        public Task<PermissionResponse> DeleteAsync(
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Task<ResponseMessage> response = this.DeletePermissionStreamAsync(
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponseAsync(this.Permission, response);
        }

        public Task<ResponseMessage> DeletePermissionStreamAsync(
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Delete,
                tokenExpiryInSeconds: null,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        public Task<PermissionResponse> ReadAsync(
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Task<ResponseMessage> response = this.ReadPermissionStreamAsync(
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponseAsync(this.Permission, response);
        }

        public Task<ResponseMessage> ReadPermissionStreamAsync(int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        public Task<PermissionResponse> ReplaceAsync(
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
            Task<ResponseMessage> response = this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(permissionProperties),
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreatePermissionResponseAsync(this.Permission, response);
        }

        public Task<ResponseMessage> ReplacePermissionStreamAsync(
            PermissionProperties permissionProperties,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
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
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ReplaceStreamInternalAsync(
            Stream streamPayload,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return this.ProcessStreamAsync(
                streamPayload: streamPayload,
                operationType: OperationType.Replace,
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            int? tokenExpiryInSeconds,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return this.ProcessResourceOperationStreamAsync(
                streamPayload: streamPayload,
                operationType: operationType,
                linkUri: this.LinkUri,
                resourceType: ResourceType.Permission,
                tokenExpiryInSeconds: tokenExpiryInSeconds,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessResourceOperationStreamAsync(
           Stream streamPayload,
           OperationType operationType,
           Uri linkUri,
           ResourceType resourceType,
           int? tokenExpiryInSeconds,
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
    }
}
