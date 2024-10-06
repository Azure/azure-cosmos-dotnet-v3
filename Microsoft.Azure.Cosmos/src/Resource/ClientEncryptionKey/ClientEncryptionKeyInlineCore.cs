//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;

    /// <summary>
    /// This class acts as a wrapper over <see cref="ClientEncryptionKeyCore"/> for environments that use SynchronizationContext.
    /// </summary>
    internal sealed class ClientEncryptionKeyInlineCore : ClientEncryptionKeyCore
    {
        internal ClientEncryptionKeyInlineCore(
            CosmosClientContext clientContext,
            DatabaseInternal database,
            string keyId)
            : base(
                  clientContext,
                  database,
                  keyId)
        {
        }

        /// <inheritdoc/>
        public override Task<ClientEncryptionKeyResponse> ReadAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: requestOptions,
                task: (trace) => base.ReadAsync(requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadClientEncryptionKey, (response) => new OpenTelemetryResponse<ClientEncryptionKeyProperties>(response)));
        }

        public override Task<ClientEncryptionKeyResponse> ReplaceAsync(
            ClientEncryptionKeyProperties clientEncryptionKeyProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReplaceAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Replace,
                requestOptions: requestOptions,
                task: (trace) => base.ReplaceAsync(clientEncryptionKeyProperties, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReplaceClientEncryptionKey, (response) => new OpenTelemetryResponse<ClientEncryptionKeyProperties>(response)));
        }
    }
}