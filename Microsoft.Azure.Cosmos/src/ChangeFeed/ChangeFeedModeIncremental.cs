// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using Microsoft.Azure.Documents;

    internal sealed class ChangeFeedModeIncremental : ChangeFeedMode
    {
        public static ChangeFeedMode Instance { get; } = new ChangeFeedModeIncremental();

        internal override void Accept(RequestMessage requestMessage)
        {
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.A_IM, HttpConstants.A_IMHeaderValues.IncrementalFeed);
        }
    }
}
