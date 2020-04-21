// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal abstract class FeedRangeInternal : FeedRange
    {
        public abstract Task<List<Documents.Routing.Range<string>>> GetEffectiveRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition);

        public abstract Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken);

        public abstract void Accept(FeedRangeVisitor visitor);

        public abstract override string ToString();

        public static bool TryParse(
            JObject jObject,
            JsonSerializer serializer,
            out FeedRangeInternal feedRangeInternal)
        {
            if (FeedRangeEPK.TryParse(jObject, serializer, out feedRangeInternal))
            {
                return true;
            }

            if (FeedRangePartitionKey.TryParse(jObject, serializer, out feedRangeInternal))
            {
                return true;
            }

            if (FeedRangePartitionKeyRange.TryParse(jObject, serializer, out feedRangeInternal))
            {
                return true;
            }

            feedRangeInternal = null;
            return false;
        }
    }
}
