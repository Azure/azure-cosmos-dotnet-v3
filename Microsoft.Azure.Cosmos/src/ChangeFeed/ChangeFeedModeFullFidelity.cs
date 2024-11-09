// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using Microsoft.Azure.Documents;

    internal sealed class ChangeFeedModeFullFidelity : ChangeFeedMode
    {
        public static ChangeFeedMode Instance { get; } = new ChangeFeedModeFullFidelity();

        internal override void Accept(RequestMessage requestMessage)
        {
            requestMessage.UseGatewayMode = true;
            
            // Above, defaulting to Gateway is necessary for Full-Fidelity Change Feed for the Split-handling logic resides within Compute Gateway.
            // TODO: If and when, this changes, it will be necessary to remove this.

            requestMessage.Headers.Add(HttpConstants.HttpHeaders.A_IM, HttpConstants.A_IMHeaderValues.FullFidelityFeed);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ChangeFeedWireFormatVersion, Constants.ChangeFeedWireFormatVersions.SeparateMetadataWithCrts);
        }
    }
}