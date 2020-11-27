//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This class acts as a wrapper over <see cref="ClientEncryptionKeyCore"/> for environments that use SynchronizationContext.
    /// </summary>
    internal sealed class ClientEncryptionKeyInlineCore : ClientEncryptionKeyCore
    {
        private readonly ClientEncryptionKeyCore clientEncryptionKey;

        public ClientEncryptionKeyInlineCore(ClientEncryptionKeyCore clientEncryptionKey)
        {
            this.clientEncryptionKey = clientEncryptionKey ?? throw new ArgumentException(nameof(clientEncryptionKey));
        }

        /// <inheritdoc/>
        public override string Id => this.clientEncryptionKey.Id;

        public override string LinkUri => this.clientEncryptionKey.LinkUri;

        /// <inheritdoc/>
        public override Task<ClientEncryptionKeyResponse> ReadAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadAsync),
                requestOptions,
                (diagnostics) => base.ReadAsync(requestOptions, cancellationToken));
        }
    }
}