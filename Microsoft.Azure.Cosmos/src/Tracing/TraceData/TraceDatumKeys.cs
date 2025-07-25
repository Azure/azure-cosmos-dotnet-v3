// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    internal static class TraceDatumKeys
    {
        public const string ClientSideRequestStats = "Client Side Request Stats";
        public const string TransportRequest = "Microsoft.Azure.Documents.ServerStoreModel Transport Request";
        public const string GetCosmosElementResponse = "Get Cosmos Element Response";
        public const string QueryMetrics = "Query Metrics";
        public const string QueryResponseSerialization = "Query Response Serialization";
        public const string FeedResponseSerialization = "Feed Response Serialization";
        public const string ChangeFeedResponseSerialization = "ChangeFeed Response Serialization";
    }
}
