//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents.Routing;

    internal class RangeComparerProvider
    {
        /// <summary>
        /// Gets the minimum and maximum range comparers based on the current configuration.
        /// </summary>
        /// <returns>Tuple of (minComparer, maxComparer)</returns>
        public static (IComparer<Range<string>> minComparer, IComparer<Range<string>> maxComparer) GetComparers(bool useLengthAwareComparison)
        {
            return (
                useLengthAwareComparison ? Range<string>.LengthAwareMinComparer.Instance : Range<string>.MinComparer.Instance,
                useLengthAwareComparison ? Range<string>.LengthAwareMaxComparer.Instance : Range<string>.MaxComparer.Instance
            );
        }

        public static Range<string> GetIntersection(
            Range<string> first,
            Range<string> second,
            bool useLengthAwareComparison)
        {
            if (first == null)
            {
                throw new ArgumentNullException(nameof(first));
            }

            if (second == null)
            {
                throw new ArgumentNullException(nameof(second));
            }

            (IComparer<Range<string>> minComparer, IComparer<Range<string>> maxComparer) =
                RangeComparerProvider.GetComparers(useLengthAwareComparison);
            Range<string> minRange = minComparer.Compare(first, second) > 0
                ? first
                : second;
            Range<string> maxRange = maxComparer.Compare(first, second) < 0
                ? first
                : second;

            return new Range<string>(
                minRange.Min,
                maxRange.Max,
                minRange.IsMinInclusive,
                maxRange.IsMaxInclusive);
        }

        public static Range<string> GetHull(
            IReadOnlyList<Range<string>> ranges,
            bool useLengthAwareComparison)
        {
            if (ranges == null)
            {
                throw new ArgumentNullException(nameof(ranges));
            }

            if (ranges.Count == 0)
            {
                throw new ArgumentException("At least one range is required.", nameof(ranges));
            }

            (IComparer<Range<string>> minComparer, IComparer<Range<string>> maxComparer) =
                RangeComparerProvider.GetComparers(useLengthAwareComparison);
            Range<string> minRange = ranges[0];
            Range<string> maxRange = ranges[0];
            for (int i = 1; i < ranges.Count; i++)
            {
                if (minComparer.Compare(ranges[i], minRange) < 0)
                {
                    minRange = ranges[i];
                }

                if (maxComparer.Compare(ranges[i], maxRange) > 0)
                {
                    maxRange = ranges[i];
                }
            }

            return new Range<string>(
                minRange.Min,
                maxRange.Max,
                minRange.IsMinInclusive,
                maxRange.IsMaxInclusive);
        }
    }
}
