// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Newtonsoft.Json;

    /// <summary>
    /// Backward compatibility implementation to transition from V2 SDK queries that were filtering by PKRangeId
    /// </summary>
    /// <remarks>
    /// Not split proof. 
    /// </remarks>
    [JsonConverter(typeof(FeedTokenInternalConverter))]
    internal sealed class FeedTokenPartitionKeyRange : FeedTokenInternal
    {
        internal readonly string PartitionKeyRangeId;
        internal FeedTokenEPKRange FeedTokenEPKRange; // If the initial token splits, it will use this token;
        private string continuationToken;

        public FeedTokenPartitionKeyRange(string partitionKeyRangeId)
        {
            this.PartitionKeyRangeId = partitionKeyRangeId;
        }

        public override void EnrichRequest(RequestMessage request)
        {
            if (this.FeedTokenEPKRange == null)
            {
                ChangeFeedRequestOptions.FillPartitionKeyRangeId(request, this.PartitionKeyRangeId);
            }
            else
            {
                this.FeedTokenEPKRange.EnrichRequest(request);
            }
        }

        public override string GetContinuation()
        {
            if (this.FeedTokenEPKRange == null)
            {
                return this.continuationToken;
            }
            else
            {
                return this.FeedTokenEPKRange.GetContinuation();
            }
        }

        public override string ToString()
        {
            if (this.FeedTokenEPKRange == null)
            {
                return JsonConvert.SerializeObject(this);
            }
            else
            {
                // If it got split, then rather serialize the internal one and treat it as EPK range moving forward
                return JsonConvert.SerializeObject(this.FeedTokenEPKRange);
            }
        }

        public override void UpdateContinuation(string continuationToken)
        {
            if (this.FeedTokenEPKRange == null)
            {
                this.continuationToken = continuationToken;
            }
            else
            {
                this.FeedTokenEPKRange.UpdateContinuation(continuationToken);
            }
        }

        public static bool TryParseInstance(string toStringValue, out FeedToken feedToken)
        {
            try
            {
                feedToken = JsonConvert.DeserializeObject<FeedTokenPartitionKeyRange>(toStringValue);
                return true;
            }
            catch
            {
                // Special case, for backward compatibility, if the string represents a PKRangeId
                if (int.TryParse(toStringValue, out int pkRangeId))
                {
                    feedToken = new FeedTokenPartitionKeyRange(pkRangeId.ToString());
                    return true;
                }

                feedToken = null;
                return false;
            }
        }

        public override async Task<bool> ShouldRetryAsync(
            ContainerCore containerCore,
            ResponseMessage responseMessage,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (this.FeedTokenEPKRange != null)
            {
                return await this.FeedTokenEPKRange.ShouldRetryAsync(containerCore, responseMessage, cancellationToken);
            }

            if (responseMessage.IsSuccessStatusCode)
            {
                return false;
            }

            bool partitionSplit = responseMessage.StatusCode == HttpStatusCode.Gone
                && (responseMessage.Headers.SubStatusCode == Documents.SubStatusCodes.PartitionKeyRangeGone || responseMessage.Headers.SubStatusCode == Documents.SubStatusCodes.CompletingSplit);
            if (partitionSplit)
            {
                string containerRid = await containerCore.GetRIDAsync(cancellationToken);
                Routing.PartitionKeyRangeCache partitionKeyRangeCache = await containerCore.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
                IReadOnlyList<Documents.PartitionKeyRange> keyRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                containerRid,
                new Documents.Routing.Range<string>(
                    Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                    Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                    isMaxInclusive: false,
                    isMinInclusive: true),
                forceRefresh: true);

                List<Documents.PartitionKeyRange> addedRanges = keyRanges.Where(range => range.Parents.Contains(this.PartitionKeyRangeId)).ToList();
                if (addedRanges.Count == 0)
                {
                    DefaultTrace.TraceError("FeedTokenPartitionKeyRange - Could not obtain children after split for {0}", this.PartitionKeyRangeId);
                    return false;
                }

                this.FeedTokenEPKRange = new FeedTokenEPKRange(containerRid,
                    new Documents.Routing.Range<string>(addedRanges[0].MinInclusive, addedRanges[addedRanges.Count - 1].MaxExclusive, true, false),
                    addedRanges.Select(range => FeedTokenEPKRange.CreateCompositeContinuationTokenForRange(range.MinInclusive, range.MaxExclusive, this.continuationToken)).ToList());
                return true;
            }

            return false;
        }

        public override bool TrySplit(
            out IReadOnlyList<FeedToken> splitFeedTokens,
            int? maxTokens = null)
        {
            if (this.FeedTokenEPKRange == null)
            {
                splitFeedTokens = null;
                return false;
            }

            return this.FeedTokenEPKRange.TrySplit(out splitFeedTokens, maxTokens);
        }
    }
}
