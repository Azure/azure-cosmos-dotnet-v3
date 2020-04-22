//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    // This class acts as a wrapper for environments that use SynchronizationContext.
    internal sealed class UserInlineCore : User
    {
        private readonly UserCore user;

        public override string Id => this.user.Id;

        internal UserInlineCore(UserCore database)
        {
            this.user = database ?? throw new ArgumentNullException(nameof(database));
        }

        public override Task<UserResponse> ReadAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.user.ReadAsync(requestOptions, cancellationToken));
        }

        public override Task<UserResponse> ReplaceAsync(
            UserProperties userProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.user.ReplaceAsync(userProperties, requestOptions, cancellationToken));
        }

        public override Task<UserResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.user.DeleteAsync(requestOptions, cancellationToken));
        }

        public override Permission GetPermission(string id)
        {
            return this.user.GetPermission(id);
        }

        public override Task<PermissionResponse> CreatePermissionAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.user.CreatePermissionAsync(permissionProperties, tokenExpiryInSeconds, requestOptions, cancellationToken));
        }

        public override Task<PermissionResponse> UpsertPermissionAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.user.UpsertPermissionAsync(permissionProperties, tokenExpiryInSeconds, requestOptions, cancellationToken));
        }

        public override FeedIterator<T> GetPermissionQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.user.GetPermissionQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator<T> GetPermissionQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.user.GetPermissionQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public static implicit operator UserCore(UserInlineCore userInlineCore) => userInlineCore.user;
    }
}
