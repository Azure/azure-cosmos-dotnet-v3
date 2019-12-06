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

        public static Permission CreateInlineIfNeeded(PermissionCore permission)
        {
            if (SynchronizationContext.Current == null)
            {
                return permission;
            }

            return new PermissionInlineCore(permission);
        }

        public override Task<PermissionResponse> ReadAsync(
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.permission.ReadAsync(tokenExpiryInSeconds, requestOptions, cancellationToken));
        }

        public override Task<PermissionResponse> ReplaceAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.permission.ReplaceAsync(permissionProperties, tokenExpiryInSeconds, requestOptions, cancellationToken));
        }

        public override Task<PermissionResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.permission.DeleteAsync(requestOptions, cancellationToken));
        }

        private PermissionInlineCore(PermissionCore database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            this.permission = database;
        }
    }
}
