// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Newtonsoft.Json;

    [JsonConverter(typeof(FeedRangeInternalConverter))]
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

        public override string ToJsonString() => JsonConvert.SerializeObject(this);

        public static bool TryParse(
            string jsonString,
            out FeedRangeInternal feedRangeInternal)
        {
            try
            {
                feedRangeInternal = JsonConvert.DeserializeObject<FeedRangeInternal>(jsonString);
                return true;
            }
            catch (JsonReaderException)
            {
                DefaultTrace.TraceError("Unable to parse FeedRange from string.");
                feedRangeInternal = null;
                return false;
            }
        }
    }
}
