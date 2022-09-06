//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.IO;
    using System.Net;
    using Microsoft.Azure.Cosmos;

    internal sealed class DecryptedResponseMessage : ResponseMessage
    {
        private readonly ResponseMessage responseMessage;
        private Stream decryptedContent;
        private bool isDisposed = false;

        public DecryptedResponseMessage(
            ResponseMessage responseMessage,
            Stream decryptedContent)
        {
            this.responseMessage = responseMessage;
            this.decryptedContent = decryptedContent;
        }

        public override Stream Content
        {
            get => this.decryptedContent;
            set => this.decryptedContent = value;
        }

        public override string ContinuationToken => this.responseMessage.ContinuationToken;

        public override CosmosDiagnostics Diagnostics => this.responseMessage.Diagnostics;

        public override string ErrorMessage => this.responseMessage.ErrorMessage;

        public override Headers Headers => this.responseMessage.Headers;

        public override RequestMessage RequestMessage => this.responseMessage.RequestMessage;

        public override HttpStatusCode StatusCode => this.responseMessage.StatusCode;

        public override bool IsSuccessStatusCode => this.responseMessage.IsSuccessStatusCode;

        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.isDisposed)
            {
                this.isDisposed = true;
                if (this.decryptedContent != null)
                {
                    this.decryptedContent.Dispose();
                    this.decryptedContent = null;
                }

                if (this.responseMessage != null)
                {
                    this.responseMessage.Dispose();
                }
            }
        }
    }
}
