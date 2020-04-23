//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos item response
    /// </summary>
    public class ItemResponse<T> : Response<T>
    {
        /// <summary>
        /// Create a <see cref="ItemResponse{T}"/> as a no-op for mock testing
        /// </summary>
        protected ItemResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the CosmosResponseMessage
        /// </summary>
        internal ItemResponse(
            HttpStatusCode httpStatusCode,
            Headers headers,
            T item,
            CosmosDiagnostics diagnostics,
            DecryptionInfo decryptionInfo = null)
        {
            this.StatusCode = httpStatusCode;
            this.Headers = headers;
            this.Resource = item;
            this.Diagnostics = diagnostics;
            this.DecryptionInfo = decryptionInfo;
        }

        /// <inheritdoc/>
        public override Headers Headers { get; }

        /// <inheritdoc/>
        public override T Resource { get; }

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
        /// Decryption processing information
        /// </summary>
        public virtual DecryptionInfo DecryptionInfo { get; }
    }

    internal class ItemResponse : ResponseMessage
    {
        private readonly Stream Result;

        internal virtual DecryptionInfo DecryptionInfo { get; }

        public override Stream Content
        {
            get
            {
                return this.Result;
            }
        }

        internal ItemResponse(
            Stream result,
            DecryptionInfo decryptionInfo,
            HttpStatusCode statusCode,
            RequestMessage requestMessage,
            Headers responseHeaders,
            CosmosException cosmosException,
            CosmosDiagnosticsContext diagnostics)
            : base(
                statusCode: statusCode,
                requestMessage: requestMessage,
                headers: responseHeaders,
                cosmosException: cosmosException,
                diagnostics: diagnostics)
        {
            this.Result = result;
            this.DecryptionInfo = decryptionInfo;
        }        
    }
}