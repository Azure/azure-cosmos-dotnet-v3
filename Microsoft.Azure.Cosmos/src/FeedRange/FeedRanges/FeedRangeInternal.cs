// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Newtonsoft.Json;

    [Serializable]
    [JsonConverter(typeof(FeedRangeInternalConverter))]
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif  
        abstract class FeedRangeInternal : FeedRange
    {
        internal abstract Task<List<Documents.Routing.Range<string>>> GetEffectiveRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            ITrace trace);

        internal abstract Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            IRoutingMapProvider routingMapProvider,
            string containerRid,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            CancellationToken cancellationToken,
            ITrace trace);

        internal abstract void Accept(IFeedRangeVisitor visitor);

        internal abstract void Accept<TInput>(IFeedRangeVisitor<TInput> visitor, TInput input);

        internal abstract TOutput Accept<TInput, TOutput>(IFeedRangeVisitor<TInput, TOutput> visitor, TInput input);

        internal abstract TResult Accept<TResult>(IFeedRangeTransformer<TResult> transformer);

        internal abstract Task<TResult> AcceptAsync<TResult>(IFeedRangeAsyncVisitor<TResult> visitor, CancellationToken cancellationToken = default);

        internal abstract Task<TResult> AcceptAsync<TResult, TArg>(
            IFeedRangeAsyncVisitor<TResult, TArg> visitor,
            TArg argument,
            CancellationToken cancellationToken);

        public abstract override string ToString();

        public override string ToJsonString()
        {
            return JsonConvert.SerializeObject(this);
        }

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
