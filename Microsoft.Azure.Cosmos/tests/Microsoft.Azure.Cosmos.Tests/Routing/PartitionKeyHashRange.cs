// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Text;

    internal readonly struct PartitionKeyHashRange : IComparable<PartitionKeyHashRange>, IEquatable<PartitionKeyHashRange>
    {
        /// <summary>
        /// Range of PartitionKeyHash where both start and end can be nullable, which signals an open range.
        /// </summary>
        /// <param name="startInclusive">The inclusive start hash. If null this means that the range starts from negative infinity.</param>
        /// <param name="endExclusive">The exclusive end hash. If null this means that the range goes until positive infinity.</param>
        public PartitionKeyHashRange(PartitionKeyHash? startInclusive, PartitionKeyHash? endExclusive)
        {
            if (endExclusive.HasValue && startInclusive.HasValue && (endExclusive.Value.CompareTo(startInclusive.Value) < 0))
            {
                throw new ArgumentOutOfRangeException($"{nameof(startInclusive)} must be less than or equal to {nameof(endExclusive)}.");
            }

            this.StartInclusive = startInclusive;
            this.EndExclusive = endExclusive;
        }

        public PartitionKeyHash? StartInclusive { get; }

        public PartitionKeyHash? EndExclusive { get; }

        public bool Contains(PartitionKeyHash partitionKeyHash)
        {
            bool rangeStartsBefore = !this.StartInclusive.HasValue || (this.StartInclusive.Value <= partitionKeyHash);
            bool rangeEndsAfter = !this.EndExclusive.HasValue || (partitionKeyHash <= this.EndExclusive.Value);
            return rangeStartsBefore && rangeEndsAfter;
        }

        public bool Contains(PartitionKeyHashRange partitionKeyHashRange)
        {
            bool rangeStartsBefore = !this.StartInclusive.HasValue || (partitionKeyHashRange.StartInclusive.HasValue && (this.StartInclusive.Value <= partitionKeyHashRange.StartInclusive.Value));
            bool rangeEndsAfter = !this.EndExclusive.HasValue || (partitionKeyHashRange.EndExclusive.HasValue && (partitionKeyHashRange.EndExclusive.Value <= this.EndExclusive.Value));
            return rangeStartsBefore && rangeEndsAfter;
        }

        public bool TryGetOverlappingRange(PartitionKeyHashRange rangeToOverlapWith, out PartitionKeyHashRange overlappingRange)
        {
            PartitionKeyHash? maxOfStarts;
            if (this.StartInclusive.HasValue && rangeToOverlapWith.StartInclusive.HasValue)
            {
                maxOfStarts = this.StartInclusive.Value > rangeToOverlapWith.StartInclusive.Value ? this.StartInclusive.Value : rangeToOverlapWith.StartInclusive.Value;
            }
            else if (this.StartInclusive.HasValue && !rangeToOverlapWith.StartInclusive.HasValue)
            {
                maxOfStarts = this.StartInclusive.Value;
            }
            else
            {
                maxOfStarts = !this.StartInclusive.HasValue && rangeToOverlapWith.StartInclusive.HasValue ? rangeToOverlapWith.StartInclusive.Value : null;
            }

            PartitionKeyHash? minOfEnds;
            if (this.EndExclusive.HasValue && rangeToOverlapWith.EndExclusive.HasValue)
            {
                minOfEnds = this.EndExclusive.Value < rangeToOverlapWith.EndExclusive.Value ? this.EndExclusive.Value : rangeToOverlapWith.EndExclusive.Value;
            }
            else if (this.EndExclusive.HasValue && !rangeToOverlapWith.EndExclusive.HasValue)
            {
                minOfEnds = this.EndExclusive.Value;
            }
            else
            {
                minOfEnds = !this.EndExclusive.HasValue && rangeToOverlapWith.EndExclusive.HasValue ? rangeToOverlapWith.EndExclusive.Value : null;
            }

            if (maxOfStarts.HasValue && minOfEnds.HasValue && (maxOfStarts >= minOfEnds))
            {
                overlappingRange = default;
                return false;
            }

            overlappingRange = new PartitionKeyHashRange(maxOfStarts, minOfEnds);
            return true;
        }

        public int CompareTo(PartitionKeyHashRange other)
        {
            // Provide a total sort order by first comparing on the start and then going to the end.
            int cmp;
            if (this.StartInclusive.HasValue && other.StartInclusive.HasValue)
            {
                cmp = this.StartInclusive.Value.CompareTo(other.StartInclusive.Value);
            }
            else if (this.StartInclusive.HasValue && !other.StartInclusive.HasValue)
            {
                cmp = 1;
            }
            else
            {
                cmp = !this.StartInclusive.HasValue && other.StartInclusive.HasValue ? -1 : 0;
            }

            if (cmp != 0)
            {
                return cmp;
            }

            if (this.EndExclusive.HasValue && other.EndExclusive.HasValue)
            {
                cmp = this.EndExclusive.Value.CompareTo(other.EndExclusive.Value);
            }
            else if (this.EndExclusive.HasValue && !other.EndExclusive.HasValue)
            {
                cmp = -1;
            }
            else
            {
                cmp = !this.EndExclusive.HasValue && other.EndExclusive.HasValue ? 1 : 0;
            }

            return cmp;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is PartitionKeyHashRange partitionKeyHashRange))
            {
                return false;
            }

            return this.Equals(partitionKeyHashRange);
        }

        public bool Equals(PartitionKeyHashRange other)
        {
            return this.StartInclusive.Equals(other.StartInclusive) && this.EndExclusive.Equals(other.EndExclusive);
        }

        public override int GetHashCode()
        {
            int startHashCode = this.StartInclusive?.GetHashCode() ?? 0;
            int endHashCode = this.EndExclusive?.GetHashCode() ?? 0;
            return startHashCode ^ endHashCode;
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("[");

            if (this.StartInclusive.HasValue)
            {
                stringBuilder.Append(this.StartInclusive.Value.Value);
            }

            stringBuilder.Append(",");

            if (this.EndExclusive.HasValue)
            {
                stringBuilder.Append(this.EndExclusive.Value.Value);
            }

            stringBuilder.Append("]");

            return stringBuilder.ToString();
        }
    }
}