// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using Microsoft.Azure.Documents;

    internal sealed class ChangeFeedModeFullFidelity : ChangeFeedMode
    {
        public static readonly string FullFidelityHeader = "Full-Fidelity Feed"; // HttpConstants.A_IMHeaderValues.FullFidelityFeed

        public static ChangeFeedMode Instance { get; } = new ChangeFeedModeFullFidelity();

        internal override void Accept(RequestMessage requestMessage)
        {
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.A_IM, ChangeFeedModeFullFidelity.FullFidelityHeader);
        }
    }
}
