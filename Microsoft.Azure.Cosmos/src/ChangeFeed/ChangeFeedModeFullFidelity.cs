// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using Microsoft.Azure.Documents;

    internal sealed class ChangeFeedModeFullFidelity : ChangeFeedMode
    {
        public static readonly string FullFidelityHeader = HttpConstants.A_IMHeaderValues.FullFidelityFeed;
#if PREVIEW
        public
#else
        internal
#endif 
            static readonly string ChangeFeedWireFormatVersion = Constants.ChangeFeedWireFormatVersions.SeparateMetadataWithCrts;

        internal static ChangeFeedMode Instance { get; } = new ChangeFeedModeFullFidelity();

        internal override void Accept(RequestMessage requestMessage)
        {
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.A_IM, ChangeFeedModeFullFidelity.FullFidelityHeader);
#if PREVIEW
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ChangeFeedWireFormatVersion, ChangeFeedModeFullFidelity.ChangeFeedWireFormatVersion);
#endif
        }
    }
}