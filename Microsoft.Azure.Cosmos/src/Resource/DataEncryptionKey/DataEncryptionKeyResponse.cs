//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;

    /// <summary>
    /// Response from the Cosmos DB service for a <see cref="DataEncryptionKey"/> related request.
    /// </summary>
    public class DataEncryptionKeyResponse : Response<DataEncryptionKeyProperties>
    {
        /// <summary>
        /// Creates an EncryptionKeyResponse as a no-op for mock testing.
        /// </summary>
        protected DataEncryptionKeyResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage.
        /// </summary>
        internal DataEncryptionKeyResponse(
            HttpStatusCode httpStatusCode,
            Headers headers,
            DataEncryptionKeyProperties keyProperties,
            DataEncryptionKey key,
            CosmosDiagnostics diagnostics)
        {
            this.StatusCode = httpStatusCode;
            this.Headers = headers;
            this.Resource = keyProperties;
            this.DataEncryptionKey = key;
            this.Diagnostics = diagnostics;
        }

        /// <summary>
        /// The reference to the data encryption key that allows additional operations on it.
        /// </summary>
        public virtual DataEncryptionKey DataEncryptionKey { get; private set; }

        /// <inheritdoc/>
        public override Headers Headers { get; }

        /// <inheritdoc/>
        public override DataEncryptionKeyProperties Resource { get; }

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
        /// Get the data encryption key implicitly from an encryption key response.
        /// </summary>
        /// <param name="response">Response from which to get the data encryption key.</param>
        public static implicit operator DataEncryptionKey(DataEncryptionKeyResponse response)
        {
            return response.DataEncryptionKey;
        }
    }
}
