//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

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
                nameof(ReadAsync),
                requestOptions,
                (trace) => base.ReadAsync(requestOptions, cancellationToken));
        }

        public override Task<ClientEncryptionKeyResponse> ReplaceAsync(
            ClientEncryptionKeyProperties clientEncryptionKeyProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReplaceAsync),
                requestOptions,
                (trace) => base.ReplaceAsync(clientEncryptionKeyProperties, requestOptions, cancellationToken));
        }
    }
}