// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
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
        sealed class ChangeFeedPage
    {
        private ChangeFeedPage(
            CosmosArray documents,
            bool notModified,
            double requestCharge,
            string activityId,
            ChangeFeedCrossFeedRangeState state,
            ImmutableDictionary<string, string> additionalHeaders)
        {
            this.Documents = documents ?? throw new ArgumentOutOfRangeException(nameof(documents));
            this.NotModified = notModified;
            this.RequestCharge = requestCharge < 0 ? throw new ArgumentOutOfRangeException(nameof(requestCharge)) : requestCharge;
            this.ActivityId = activityId ?? throw new ArgumentNullException(nameof(activityId));
            this.State = state;
            this.AdditionalHeaders = additionalHeaders;
        }

        public CosmosArray Documents { get; }

        public bool NotModified { get; }

        public double RequestCharge { get; }

        public string ActivityId { get; }

        public ChangeFeedCrossFeedRangeState State { get; }

        public ImmutableDictionary<string, string> AdditionalHeaders { get; }

        public static ChangeFeedPage CreateNotModifiedPage(
            double requestCharge, 
            string activityId, 
            ChangeFeedCrossFeedRangeState state,
            ImmutableDictionary<string, string> additionalHeaders)
        {
            return new ChangeFeedPage(CosmosArray.Empty, notModified: true, requestCharge, activityId, state, additionalHeaders);
        }

        public static ChangeFeedPage CreatePageWithChanges(
            CosmosArray documents, 
            double requestCharge,
            string activityId, 
            ChangeFeedCrossFeedRangeState state, 
            ImmutableDictionary<string, string> additionalHeaders)
        {
            return new ChangeFeedPage(documents, notModified: false, requestCharge, activityId, state, additionalHeaders);
        }
    }
}
