// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;

    [JsonConverter(typeof(FeedTokenInternalConverter))]
    internal sealed class FeedTokenPartitionKey : FeedTokenInternal
    {
        internal readonly PartitionKey PartitionKey;
        private string continuationToken;
        private bool isDone = false;

        public FeedTokenPartitionKey(PartitionKey partitionKey)
        {
            this.PartitionKey = partitionKey;
        }

        public override void EnrichRequest(RequestMessage request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            request.Headers.PartitionKey = this.PartitionKey.ToJsonString();
        }

        public override string GetContinuation() => this.continuationToken;

        public override bool IsDone => this.isDone;

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

        public static bool TryParseInstance(string toStringValue, out FeedToken feedToken)
        {
            try
            {
                feedToken = JsonConvert.DeserializeObject<FeedTokenPartitionKey>(toStringValue);
                return true;
            }
            catch
            {
                feedToken = null;
                return false;
            }
        }
    }
}
