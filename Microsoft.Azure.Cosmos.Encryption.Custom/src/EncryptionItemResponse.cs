// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.Net;

    internal sealed class EncryptionItemResponse<T> : ItemResponse<T>
    {
        private readonly ResponseMessage responseMessage;
        private readonly T resource;

        public EncryptionItemResponse(
            ResponseMessage responseMessage,
            T item)
        {
            this.responseMessage = responseMessage;
            this.resource = item;
        }

        public override Headers Headers => this.responseMessage.Headers;

        public override T Resource => this.resource;

        public override HttpStatusCode StatusCode => this.responseMessage.StatusCode;

        public override CosmosDiagnostics Diagnostics => this.responseMessage.Diagnostics;
    }
}
