//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    // This class acts as a wrapper for environments that use SynchronizationContext.
    internal sealed class PermissionInlineCore : Permission
    {
        private readonly PermissionCore permission;

        public override string Id => this.permission.Id;

        internal PermissionInlineCore(
            CosmosClientContext clientContext,
            UserInlineCore user,
            string userId)
        {
            if (clientContext == null)
            {
                throw new ArgumentNullException(nameof(clientContext));
            }

            this.permission = new PermissionCore(
                this,
                clientContext,
                user,
                userId);
        }

        public override Task<PermissionResponse> ReadAsync(
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.permission.ReadAsync(tokenExpiryInSeconds, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<PermissionResponse> ReplaceAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.permission.ReplaceAsync(permissionProperties, tokenExpiryInSeconds, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<PermissionResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.permission.DeleteAsync(requestOptions, diagnostics, cancellationToken));
        }
    }
}
