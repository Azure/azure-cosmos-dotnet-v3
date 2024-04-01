//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Cosmos.Samples.ReEncryption
{
    using System.Net;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// ReEncryption Response Message.
    /// </summary>
    public sealed class ReEncryptionResponseMessage : ResponseMessage
    {
        private readonly ResponseMessage responseMessage;

        private readonly string reEncryptionContinuationToken;

        /// <summary>
        /// Gets reEncryption Bulk operation response.
        /// </summary>
        public ReEncryptionBulkOperationResponse<JObject> ReEncryptionBulkOperationResponse { get; }

        public ReEncryptionResponseMessage(
            ResponseMessage responseMessage,
            string reEncryptionContinuationToken,
            ReEncryptionBulkOperationResponse<JObject> reEncryptionBulkOperationResponse)
        {
            this.responseMessage = responseMessage;
            this.reEncryptionContinuationToken = reEncryptionContinuationToken;
            this.ReEncryptionBulkOperationResponse = reEncryptionBulkOperationResponse;
        }

        /// <inheritdoc/>
        public override string ContinuationToken => this.reEncryptionContinuationToken;

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
        public override bool IsSuccessStatusCode => this.responseMessage.StatusCode == HttpStatusCode.OK && this.responseMessage.IsSuccessStatusCode;

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            this.responseMessage.Dispose();
        }
    }
}
