//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;

    /// <summary>
    /// Response from the Cosmos DB service for a <see cref="Cosmos.ClientEncryptionKey"/> related request.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
        class ClientEncryptionKeyResponse : Response<ClientEncryptionKeyProperties>
    {
        /// <summary>
        /// Creates a client encryption key response as a no-op for mock testing.
        /// </summary>
        protected ClientEncryptionKeyResponse()
            : base()
        {
        }

        // A non-public constructor to ensure the factory is used to create the object.
        // This will prevent memory leaks when handling the HttpResponseMessage.
        internal ClientEncryptionKeyResponse(
            HttpStatusCode httpStatusCode,
            Headers headers,
            ClientEncryptionKeyProperties keyProperties,
            ClientEncryptionKey key,
            CosmosDiagnostics diagnostics)
        {
            this.StatusCode = httpStatusCode;
            this.Headers = headers;
            this.Resource = keyProperties;
            this.ClientEncryptionKey = key;
            this.Diagnostics = diagnostics;
        }

        /// <summary>
        /// The reference to the client encryption key that allows additional operations on it.
        /// </summary>
        public virtual ClientEncryptionKey ClientEncryptionKey { get; }

        /// <inheritdoc/>
        public override Headers Headers { get; }

        /// <inheritdoc/>
        public override ClientEncryptionKeyProperties Resource { get; }

        /// <inheritdoc/>
        public override HttpStatusCode StatusCode { get; }

        /// <inheritdoc/>
        public override CosmosDiagnostics Diagnostics { get; }

        /// <inheritdoc/>
        public override double RequestCharge => this.Headers?.RequestCharge ?? 0;

        /// <inheritdoc/>
        public override string ActivityId => this.Headers?.ActivityId;

        /// <inheritdoc/>
        public override string ETag => this.Headers?.ETag;

        /// <summary>
        /// Get the client encryption key implicitly from an encryption key response.
        /// </summary>
        /// <param name="response">Response from which to get the client encryption key.</param>
        public static implicit operator ClientEncryptionKey(ClientEncryptionKeyResponse response)
        {
            return response.ClientEncryptionKey;
        }
    }
}