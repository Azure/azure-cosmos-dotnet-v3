//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;

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
                operationName: nameof(ReadAsync),
                containerName: null,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: requestOptions,
                task: (trace) => base.ReadAsync(requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadUser, (response) => new OpenTelemetryResponse<UserProperties>(response)));
        }

        public override Task<UserResponse> ReplaceAsync(
            UserProperties userProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReplaceAsync),
                containerName: null,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Replace,
                requestOptions: requestOptions,
                task: (trace) => base.ReplaceAsync(userProperties, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReplaceUser, (response) => new OpenTelemetryResponse<UserProperties>(response)));
        }

        public override Task<UserResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(DeleteAsync),
                containerName: null,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Delete,
                requestOptions: requestOptions,
                task: (trace) => base.DeleteAsync(requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.DeleteUser, (response) => new OpenTelemetryResponse<UserProperties>(response)));
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
                operationName: nameof(CreatePermissionAsync),
                containerName: null,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: (trace) => base.CreatePermissionAsync(permissionProperties, tokenExpiryInSeconds, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.CreatePermission, (response) => new OpenTelemetryResponse<PermissionProperties>(response)));
        }

        public override Task<PermissionResponse> UpsertPermissionAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(UpsertPermissionAsync),
                containerName: null,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Upsert,
                requestOptions: requestOptions,
                task: (trace) => base.UpsertPermissionAsync(permissionProperties, tokenExpiryInSeconds, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.UpsertPermission, (response) => new OpenTelemetryResponse<PermissionProperties>(response)));
        }

        public override FeedIterator<T> GetPermissionQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetPermissionQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator<T> GetPermissionQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetPermissionQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }
    }
}
