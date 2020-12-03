//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    // This class acts as a wrapper for environments that use SynchronizationContext.
    internal sealed class UserInlineCore : UserCore
    {
        internal UserInlineCore(
            CosmosClientContext clientContext,
            DatabaseInternal database,
            string userId)
            : base(
                  clientContext,
                  database,
                  userId)
        {
        }

        public override Task<UserResponse> ReadAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadAsync),
                requestOptions,
                (diagnostics, trace) => base.ReadAsync(diagnostics, requestOptions, trace, cancellationToken));
        }

        public override Task<UserResponse> ReplaceAsync(
            UserProperties userProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReplaceAsync),
                requestOptions,
                (diagnostics, trace) => base.ReplaceAsync(diagnostics, userProperties, requestOptions, trace, cancellationToken));
        }

        public override Task<UserResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(DeleteAsync),
                requestOptions,
                (diagnostics, trace) => base.DeleteAsync(diagnostics, requestOptions, trace, cancellationToken));
        }

        public override Permission GetPermission(string id)
        {
            return base.GetPermission(id);
        }

        public override Task<PermissionResponse> CreatePermissionAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(CreatePermissionAsync),
                requestOptions,
                (diagnostics, trace) => base.CreatePermissionAsync(diagnostics, permissionProperties, tokenExpiryInSeconds, requestOptions, trace, cancellationToken));
        }

        public override Task<PermissionResponse> UpsertPermissionAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(UpsertPermissionAsync),
                requestOptions,
                (diagnostics, trace) => base.UpsertPermissionAsync(diagnostics, permissionProperties, tokenExpiryInSeconds, requestOptions, trace, cancellationToken));
        }

        public override FeedIterator<T> GetPermissionQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetPermissionQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator<T> GetPermissionQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetPermissionQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions));
        }
    }
}
