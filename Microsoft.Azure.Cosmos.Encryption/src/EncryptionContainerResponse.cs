// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Net;

    internal sealed class EncryptionContainerResponse : ContainerResponse
    {
        public EncryptionContainerResponse(
            ContainerResponse containerResponse,
            EncryptionContainer encryptionContainer)
        {
            this.containerResponse = containerResponse ?? throw new ArgumentNullException(nameof(containerResponse));
            this.encryptionContainer = encryptionContainer ?? throw new ArgumentNullException(nameof(encryptionContainer));
        }

        private readonly ContainerResponse containerResponse;

        private readonly EncryptionContainer encryptionContainer;

        public override Container Container => this.encryptionContainer;

        public override CosmosDiagnostics Diagnostics => this.containerResponse.Diagnostics;

        public override string ActivityId => this.containerResponse.ActivityId;

        public override string ETag => this.containerResponse.ETag;

        public override Headers Headers => this.containerResponse.Headers;

        public override double RequestCharge => this.containerResponse.RequestCharge;

        public override ContainerProperties Resource => this.containerResponse.Resource;

        public override HttpStatusCode StatusCode => this.containerResponse.StatusCode;

        /// <summary>
        /// Get <see cref="Cosmos.Container"/> implicitly from <see cref="EncryptionContainerResponse"/>
        /// </summary>
        /// <param name="response">ContainerResponse</param>
        public static implicit operator Container(EncryptionContainerResponse response)
        {
            return response.Container;
        }
    }
}
