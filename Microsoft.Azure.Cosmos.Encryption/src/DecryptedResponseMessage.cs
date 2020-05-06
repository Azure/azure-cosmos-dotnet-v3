//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using Microsoft.Azure.Cosmos;
    using System.IO;
    using System.Net;

    internal sealed class DecryptedResponseMessage : ResponseMessage
    {
        private ResponseMessage responseMessage;
        private Stream decryptedContent;

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
    }
}
