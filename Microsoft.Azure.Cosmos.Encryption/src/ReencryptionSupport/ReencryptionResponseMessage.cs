//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.Net;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Reencryption Response Message.
    /// </summary>
    public sealed class ReencryptionResponseMessage : ResponseMessage
    {
        private readonly ResponseMessage responseMessage;

        private readonly string reencryptionContinuationToken;

        /// <summary>
        /// Gets reencryption Bulk operation response.
        /// </summary>
        public ReencryptionBulkOperationResponse<JObject> ReencryptionBulkOperationResponse { get; }

        internal ReencryptionResponseMessage(
            ResponseMessage responseMessage,
            string reencryptionContinuationToken,
            ReencryptionBulkOperationResponse<JObject> reencryptionBulkOperationResponse)
        {
            this.responseMessage = responseMessage;
            this.reencryptionContinuationToken = reencryptionContinuationToken;
            this.ReencryptionBulkOperationResponse = reencryptionBulkOperationResponse;
        }

        /// <inheritdoc/>
        public override string ContinuationToken => this.reencryptionContinuationToken;

        /// <inheritdoc/>
        public override CosmosDiagnostics Diagnostics => this.responseMessage.Diagnostics;

        /// <inheritdoc/>
        public override string ErrorMessage => this.responseMessage.ErrorMessage;

        /// <inheritdoc/>
        public override Headers Headers => this.responseMessage.Headers;

        /// <inheritdoc/>
        public override RequestMessage RequestMessage => this.responseMessage.RequestMessage;

        /// <inheritdoc/>
        public override HttpStatusCode StatusCode => this.responseMessage.StatusCode;

        /// <inheritdoc/>
        public override bool IsSuccessStatusCode => this.responseMessage.IsSuccessStatusCode;

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            this.responseMessage.Dispose();
        }
    }
}
