// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using Microsoft.Azure.Documents;

    internal sealed class ChangeFeedModeIFullFidelity : ChangeFeedMode
    {
        private static readonly string HeaderFullFidelity = "Full-Fidelity Feed"; // HttpConstants.A_IMHeaderValues.FullFidelityFeed

        public static ChangeFeedMode Instance { get; } = new ChangeFeedModeIFullFidelity();

        internal override void Accept(RequestMessage requestMessage)
        {
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.A_IM, HeaderFullFidelity);
        }
    }
}
