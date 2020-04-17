// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// FeedRange that represents an exact Partition Key value.
    /// </summary>
    [JsonConverter(typeof(FeedRangePartitionKeyConverter))]
    internal sealed class FeedRangePartitionKey : FeedRangeInternal
    {
        public PartitionKey PartitionKey { get; }

        public FeedRangePartitionKey(PartitionKey partitionKey)
        {
            this.PartitionKey = partitionKey;
        }

        public override Task<List<Documents.Routing.Range<string>>> GetEffectiveRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition)
        {
            return Task.FromResult(new List<Documents.Routing.Range<string>>
                {
                    Documents.Routing.Range<string>.GetPointRange(
                        this.PartitionKey.InternalKey.GetEffectivePartitionKeyString(partitionKeyDefinition))
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
            return new List<string>() { range.Id };
        }

        public override void Accept(FeedRangeVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override string ToString() => this.PartitionKey.InternalKey.ToJsonString();

        public static new bool TryParse(
            JObject jObject,
            JsonSerializer serializer,
            out FeedRangeInternal feedRangeInternal)
        {
            try
            {
                feedRangeInternal = FeedRangePartitionKeyConverter.ReadJObject(jObject, serializer);
                return true;
            }
            catch (JsonReaderException)
            {
                feedRangeInternal = null;
                return false;
            }
        }
    }
}
