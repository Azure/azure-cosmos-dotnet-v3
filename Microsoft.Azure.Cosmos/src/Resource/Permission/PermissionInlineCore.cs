//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    // This class acts as a wrapper for environments that use SynchronizationContext.
    internal sealed class PermissionInlineCore : PermissionCore
    {
        internal PermissionInlineCore(
           CosmosClientContext clientContext,
           UserCore user,
           string userId)
            : base(
               clientContext,
               user,
               userId)
        {
        }

        public override Task<PermissionResponse> ReadAsync(
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadAsync),
                requestOptions,
                (trace) => base.ReadAsync(tokenExpiryInSeconds, requestOptions, trace, cancellationToken));
        }

        public override Task<PermissionResponse> ReplaceAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReplaceAsync),
                requestOptions,
                (trace) => base.ReplaceAsync(permissionProperties, tokenExpiryInSeconds, requestOptions, trace, cancellationToken));
        }

        public override Task<PermissionResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(DeleteAsync),
                requestOptions,
                (trace) => base.DeleteAsync(requestOptions, trace, cancellationToken));
        }
    }
}
