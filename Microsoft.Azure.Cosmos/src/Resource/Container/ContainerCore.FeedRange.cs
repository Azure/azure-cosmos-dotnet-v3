//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if PREVIEW
#nullable enable
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    internal abstract partial class ContainerCore
    {
        public override Task<FeedRange?> TryGetFeedRangeForPartitionKeyAsync(
            IReadOnlyList<FeedRange> feedRanges,
            PartitionKey partitionKey,
            CancellationToken cancellationToken = default)
        {
            return this.TryGetFeedRangeForPartitionKeyAsync(feedRanges, partitionKey, NoOpTrace.Singleton, cancellationToken);
        }

        internal async Task<FeedRange?> TryGetFeedRangeForPartitionKeyAsync(
            IReadOnlyList<FeedRange> feedRanges,
            PartitionKey partitionKey,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            FeedRangeEpkIndex index = FeedRangeEpkIndex.Create(
                feedRanges,
                this.ClientContext.ClientOptions.UseLengthAwareRangeComparer,
                cancellationToken);

            try
            {
                PartitionKeyDefinition partitionKeyDefinition = await this.GetPartitionKeyDefinitionAsync(cancellationToken);
                PartitionKeyInternal internalKey = partitionKey.IsNone
                    ? await this.GetNonePartitionKeyValueAsync(trace, cancellationToken)
                    : partitionKey.InternalKey ?? PartitionKey.Null.InternalKey;

                // Empty is the complete missing-key representation for legacy, unpartitioned containers.
                if (!(partitionKey.IsNone && internalKey.Equals(PartitionKeyInternal.Empty)) &&
                    internalKey.Components.Count != partitionKeyDefinition.Paths.Count)
                {
                    throw new ArgumentException("The partition key must specify every component of the container's partition key.", nameof(partitionKey));
                }

                string effectivePartitionKey = internalKey.GetEffectivePartitionKeyString(partitionKeyDefinition);
                cancellationToken.ThrowIfCancellationRequested();
                return index.TryGetFeedRange(effectivePartitionKey);
            }
            catch (DocumentClientException exception)
            {
                throw CosmosExceptionFactory.Create(exception, trace);
            }
        }

        internal sealed class FeedRangeEpkIndex
        {
            private readonly (FeedRange Original, Range<string> Range)[] entries;
            private readonly IComparer<Range<string>> minComparer;
            private readonly IComparer<Range<string>> maxComparer;

            private FeedRangeEpkIndex(
                (FeedRange Original, Range<string> Range)[] entries,
                IComparer<Range<string>> minComparer,
                IComparer<Range<string>> maxComparer)
            {
                this.entries = entries;
                this.minComparer = minComparer;
                this.maxComparer = maxComparer;
            }

            internal static FeedRangeEpkIndex Create(
                IReadOnlyList<FeedRange> feedRanges,
                bool useLengthAwareRangeComparer,
                CancellationToken cancellationToken)
            {
                if (feedRanges == null)
                {
                    throw new ArgumentNullException(nameof(feedRanges));
                }

                cancellationToken.ThrowIfCancellationRequested();
                (IComparer<Range<string>> minComparer, IComparer<Range<string>> maxComparer) =
                    RangeComparerProvider.GetComparers(useLengthAwareRangeComparer);
                (FeedRange Original, Range<string> Range)[] entries = new (FeedRange, Range<string>)[feedRanges.Count];

                for (int i = 0; i < entries.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    FeedRange original = feedRanges[i];
                    if (original == null)
                    {
                        throw new ArgumentException("The list must contain only non-null effective partition key feed ranges.", nameof(feedRanges));
                    }

                    FeedRangeInternal converted;
                    try
                    {
                        converted = ConvertToFeedRangeInternal(original, nameof(feedRanges));
                    }
                    catch (ArgumentException exception)
                    {
                        throw new ArgumentException("The list contains an invalid feed range representation.", nameof(feedRanges), exception);
                    }
                    catch (InvalidCastException exception)
                    {
                        throw new ArgumentException("The list contains an invalid feed range representation.", nameof(feedRanges), exception);
                    }

                    if (converted is not FeedRangeEpk epk)
                    {
                        throw new ArgumentException("The list must contain only effective partition key feed ranges.", nameof(feedRanges));
                    }

                    Range<string> range = epk.Range;
                    if (range.Min == null || range.Max == null || !range.IsMinInclusive ||
                        !IsEffectivePartitionKeyBoundary(range.Min) || !IsEffectivePartitionKeyBoundary(range.Max) ||
                        minComparer.Compare(Range<string>.GetPointRange(range.Max), Range<string>.GetPointRange(PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey)) > 0)
                    {
                        throw new ArgumentException("The list contains an invalid effective partition key range.", nameof(feedRanges));
                    }

                    int boundaryComparison = minComparer.Compare(Range<string>.GetPointRange(range.Min), Range<string>.GetPointRange(range.Max));
                    if (boundaryComparison > 0 || (boundaryComparison == 0 && !range.IsMaxInclusive))
                    {
                        throw new ArgumentException("The list contains an empty or inverted effective partition key range.", nameof(feedRanges));
                    }

                    // Copy boundaries so the index never depends on mutable range objects.
                    entries[i] = (original, new Range<string>(range.Min, range.Max, range.IsMinInclusive, range.IsMaxInclusive));
                }

                Array.Sort(entries, (left, right) => minComparer.Compare(left.Range, right.Range));
                cancellationToken.ThrowIfCancellationRequested();

                for (int i = 1; i < entries.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (maxComparer.Compare(entries[i - 1].Range, Range<string>.GetPointRange(entries[i].Range.Min)) >= 0)
                    {
                        throw new ArgumentException("The list must not contain overlapping or duplicate effective partition key ranges.", nameof(feedRanges));
                    }
                }

                return new FeedRangeEpkIndex(entries, minComparer, maxComparer);
            }

            private static bool IsEffectivePartitionKeyBoundary(string boundary)
            {
                foreach (char character in boundary)
                {
                    if (!((character >= '0' && character <= '9') || (character >= 'A' && character <= 'F')))
                    {
                        return false;
                    }
                }

                return true;
            }

            internal FeedRange? TryGetFeedRange(string effectivePartitionKey)
            {
                if (effectivePartitionKey == null)
                {
                    throw new ArgumentNullException(nameof(effectivePartitionKey));
                }

                Range<string> point = Range<string>.GetPointRange(effectivePartitionKey);
                int low = 0;
                int high = this.entries.Length - 1;
                while (low <= high)
                {
                    int middle = low + ((high - low) / 2);
                    if (this.minComparer.Compare(this.entries[middle].Range, point) <= 0)
                    {
                        low = middle + 1;
                    }
                    else
                    {
                        high = middle - 1;
                    }
                }

                return high >= 0 && this.maxComparer.Compare(this.entries[high].Range, point) >= 0
                    ? this.entries[high].Original
                    : null;
            }
        }
    }
}
#endif
