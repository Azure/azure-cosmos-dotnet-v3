// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.Net;

    internal sealed class EncryptionContainerResponse : ContainerResponse
    {
        public EncryptionContainerResponse(
            ContainerResponse containerResponse,
            MdeContainer mdeContainer)
        {
            this.containerResponse = containerResponse;
            this.mdeContainer = mdeContainer;
        }

        private readonly ContainerResponse containerResponse;

        private readonly MdeContainer mdeContainer;

        public override Container Container => this.mdeContainer;

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
