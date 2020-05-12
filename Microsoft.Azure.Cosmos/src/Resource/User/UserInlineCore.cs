//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
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
            return TaskHelper.RunInlineIfNeededAsync(() => base.ReadAsync(requestOptions, cancellationToken));
        }

        public override Task<UserResponse> ReplaceAsync(
            UserProperties userProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.ReplaceAsync(userProperties, requestOptions, cancellationToken));
        }

        public override Task<UserResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.DeleteAsync(requestOptions, cancellationToken));
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
            return TaskHelper.RunInlineIfNeededAsync(() => base.CreatePermissionAsync(permissionProperties, tokenExpiryInSeconds, requestOptions, cancellationToken));
        }

        public override Task<PermissionResponse> UpsertPermissionAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.UpsertPermissionAsync(permissionProperties, tokenExpiryInSeconds, requestOptions, cancellationToken));
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
