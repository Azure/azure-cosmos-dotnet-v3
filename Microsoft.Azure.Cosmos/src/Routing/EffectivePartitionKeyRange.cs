// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;

    internal readonly struct EffectivePartitionKeyRange : IComparable<EffectivePartitionKeyRange>, IEquatable<EffectivePartitionKeyRange>
    {
        public EffectivePartitionKeyRange(EffectivePartitionKey startInclusive, EffectivePartitionKey endExclusive)
        {
            if (endExclusive.CompareTo(startInclusive) < 0)
            {
                throw new ArgumentOutOfRangeException($"{nameof(startInclusive)} must be less than {nameof(endExclusive)}.");
            }

            this.StartInclusive = startInclusive;
            this.EndExclusive = endExclusive;
        }

        public EffectivePartitionKey StartInclusive { get; }

        public EffectivePartitionKey EndExclusive { get; }

        public int CompareTo(EffectivePartitionKeyRange other)
        {
            // Provide a total sort order by first comparing on the start and then going to the end.
            int cmp = this.StartInclusive.CompareTo(other.StartInclusive);
            if (cmp != 0)
            {
                return cmp;
            }

            return this.EndExclusive.CompareTo(other.EndExclusive);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is EffectivePartitionKeyRange effectivePartitionKeyRange))
            {
                return false;
            }

            return this.Equals(effectivePartitionKeyRange);
        }

        public bool Equals(EffectivePartitionKeyRange other)
        {
            return this.StartInclusive.Equals(other.StartInclusive) && this.EndExclusive.Equals(other.EndExclusive);
        }

        public override int GetHashCode()
        {
            int startHashCode = this.StartInclusive.GetHashCode();
            int endHashCode = this.EndExclusive.GetHashCode();
            return startHashCode ^ endHashCode;
        }
    }
}
