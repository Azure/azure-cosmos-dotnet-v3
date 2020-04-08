// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Newtonsoft.Json;

    [JsonConverter(typeof(FeedRangeSimpleContinuationConverter))]
    internal sealed class FeedRangeSimpleContinuation : FeedRangeContinuation
    {
        private string continuationToken;
        private bool isDone = false;

        public FeedRangeSimpleContinuation(
            string containerRid,
            FeedRangeInternal feedRange,
            string continuation = null)
            : base(containerRid, feedRange)
        {
            this.continuationToken = continuation;
        }

        public override void Accept(
            FeedRangeVisitor visitor,
            Action<RequestMessage, string> fillContinuation)
        {
            visitor.Visit(this, fillContinuation);
        }

        public override string GetContinuation() => this.continuationToken;

        public override bool IsDone => this.isDone;

        public override TryCatch ValidateContainer(string containerRid) => TryCatch.FromResult();

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public override void UpdateContinuation(string continuationToken)
        {
            if (continuationToken == null)
            {
                // Queries and normal ReadFeed can signal termination by CT null, not NotModified
                // Change Feed never lands here, as it always provides a CT

                // Consider current range done, if this FeedToken contains multiple ranges due to splits, all of them need to be considered done
                this.isDone = true;
            }

            this.continuationToken = continuationToken;
        }

        public static bool TryParse(string toStringValue, out FeedRangeContinuation feedToken)
        {
            try
            {
                feedToken = JsonConvert.DeserializeObject<FeedRangeSimpleContinuation>(toStringValue);
                return true;
            }
            catch (JsonReaderException)
            {
                feedToken = null;
                return false;
            }
        }
    }
}
