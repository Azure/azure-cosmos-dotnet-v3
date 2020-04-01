// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Routing;
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

        public override Task<List<Documents.Routing.Range<string>>> GetAffectedRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition)
        {
            return Task.FromResult(new List<Documents.Routing.Range<string>>
                {
                    Documents.Routing.Range<string>.GetPointRange(this.PartitionKey.InternalKey.GetEffectivePartitionKeyString(partitionKeyDefinition))
                });
        }

        public override async Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken)
        {
            string effectivePartitionKeyString = this.PartitionKey.InternalKey.GetEffectivePartitionKeyString(partitionKeyDefinition);
            Documents.PartitionKeyRange range = await routingMapProvider.TryGetRangeByEffectivePartitionKeyAsync(containerRid, effectivePartitionKeyString);
            IEnumerable<string> result = new List<string>() { range.Id };
            return result;
        }

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
