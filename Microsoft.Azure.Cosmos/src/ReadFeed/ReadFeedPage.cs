// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Cosmos.CosmosElements;

    #if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif 
        sealed class ReadFeedPage
    {
        public ReadFeedPage(
            CosmosArray documents,
            double requestCharge,  
            string activityId, 
            ReadFeedCrossFeedRangeState? state,
            ImmutableDictionary<string, string> additionalHeaders)
        {
            this.Documents = documents ?? throw new ArgumentNullException(nameof(documents));
            this.RequestCharge = requestCharge < 0 ? throw new ArgumentOutOfRangeException(nameof(requestCharge)) : requestCharge;
            this.ActivityId = activityId ?? throw new ArgumentNullException(nameof(activityId));
            this.State = state;
            this.AdditionalHeaders = additionalHeaders;
        }

        public CosmosArray Documents { get; }

        public double RequestCharge { get; }

        public string ActivityId { get; }

        public ReadFeedCrossFeedRangeState? State { get; }

        public ImmutableDictionary<string, string> AdditionalHeaders { get; }
    }
}
