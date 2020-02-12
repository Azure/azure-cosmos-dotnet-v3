// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

    [JsonConverter(typeof(FeedTokenInternalConverter))]
    internal sealed class FeedTokenPartitionKey : FeedTokenInternal
    {
        internal readonly PartitionKey PartitionKey;
        private string continuationToken;

        public FeedTokenPartitionKey(PartitionKey partitionKey)
        {
            this.PartitionKey = partitionKey;
        }

        public override void FillHeaders(
            CosmosClientContext cosmosClientContext,
            RequestMessage request)
        {
            ChangeFeedRequestOptions.FillContinuationToken(request, this.continuationToken);
            request.Headers.PartitionKey = this.PartitionKey.ToJsonString();
        }

        public override string GetContinuation() => this.continuationToken;

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public override void UpdateContinuation(string continuationToken)
        {
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
