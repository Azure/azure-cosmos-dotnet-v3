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

        public DatabaseInlineCore Database { get; }

        internal Uri LinkUri => this.user.LinkUri;

        internal CosmosClientContext ClientContext => this.user.ClientContext;

        internal UserInlineCore(
            CosmosClientContext clientContext,
            DatabaseInlineCore database,
            string userId)
        {
            if (clientContext == null)
            {
                throw new ArgumentNullException(nameof(clientContext));
            }

            this.Database = database;

            this.user = new UserCore(
                this,
                clientContext,
                database,
                userId);
        }

        public override Task<UserResponse> ReadAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.user.ReadAsync(requestOptions, diagnostics, cancellationToken));
        }

        public override Task<UserResponse> ReplaceAsync(
            UserProperties userProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.user.ReplaceAsync(userProperties, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<UserResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.user.DeleteAsync(requestOptions, diagnostics, cancellationToken));
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
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.user.CreatePermissionAsync(permissionProperties, tokenExpiryInSeconds, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<PermissionResponse> UpsertPermissionAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.user.UpsertPermissionAsync(permissionProperties, tokenExpiryInSeconds, requestOptions, diagnostics, cancellationToken));
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

        public FeedIterator GetPermissionQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            return new FeedIteratorInlineCore(this.user.GetPermissionQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public FeedIterator GetPermissionQueryStreamIterator(
            string queryText,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            return new FeedIteratorInlineCore(this.user.GetPermissionQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions));
        }
    }
}
