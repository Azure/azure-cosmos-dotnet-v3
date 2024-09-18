//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;

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
                operationName: nameof(ReadAsync),
                containerName: null,
                databaseName: this.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: requestOptions,
                task: (trace) => base.ReadAsync(tokenExpiryInSeconds, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadPermission, (response) => new OpenTelemetryResponse<PermissionProperties>(response)));
        }

        public override Task<PermissionResponse> ReplaceAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReplaceAsync),
                containerName: null,
                databaseName: this.Id,
                operationType: Documents.OperationType.Replace,
                requestOptions: requestOptions,
                task: (trace) => base.ReplaceAsync(permissionProperties, tokenExpiryInSeconds, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReplacePermission, (response) => new OpenTelemetryResponse<PermissionProperties>(response)));
        }

        public override Task<PermissionResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(DeleteAsync),
                containerName: null,
                databaseName: this.Id,
                operationType: Documents.OperationType.Delete,
                requestOptions,
                task: (trace) => base.DeleteAsync(requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.DeletePermission, (response) => new OpenTelemetryResponse<PermissionProperties>(response)));
        }
    }
}
