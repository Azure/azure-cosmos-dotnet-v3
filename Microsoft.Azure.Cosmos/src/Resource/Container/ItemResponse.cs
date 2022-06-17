//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;

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
           RequestMessage requestMessage)
        {
            this.StatusCode = httpStatusCode;
            this.Headers = headers;
            this.Resource = item;
            this.Diagnostics = diagnostics;
            this.RequestMessage = requestMessage;
        }

        /// <inheritdoc/>
        internal override RequestMessage RequestMessage { get; }

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
    }
}