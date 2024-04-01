//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using Microsoft.Azure.Documents;

    internal class ProcessorOptions
    {
        public string LeaseToken { get; set; }

        public int? MaxItemCount { get; set; }

        public TimeSpan FeedPollDelay { get; set; }

        public string StartContinuation { get; set; }

        public bool StartFromBeginning { get; set; }

        public DateTime? StartTime { get; set; }

        public TimeSpan RequestTimeout { get; set; } = CosmosHttpClient.GatewayRequestTimeout;
    }
}