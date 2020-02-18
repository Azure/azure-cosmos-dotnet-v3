// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;

    internal readonly struct EffectivePartitionKeyRange : IComparable<EffectivePartitionKeyRange>, IEquatable<EffectivePartitionKeyRange>
    {
        public EffectivePartitionKeyRange(EffectivePartitionKey start, EffectivePartitionKey end)
        {
            if (end.CompareTo(start) < 0)
            {
                throw new ArgumentOutOfRangeException($"{nameof(start)} must be less than {nameof(end)}.");
            }

            this.Start = start;
            this.End = end;
        }

        public EffectivePartitionKey Start { get; }

        public EffectivePartitionKey End { get; }

        public UInt128 Width => this.End.Value - this.Start.Value;

        public int CompareTo(EffectivePartitionKeyRange other)
        {
            // Provide a total sort order by first comparing on the start and then going to the end.
            int cmp = this.Start.CompareTo(other.Start);
            if (cmp != 0)
            {
                return cmp;
            }

            return this.End.CompareTo(other.End);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is EffectivePartitionKeyRange effectivePartitionKeyRange))
            {
                return false;
            }

            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            return this.Equals(effectivePartitionKeyRange);
        }

        public bool Equals(EffectivePartitionKeyRange other)
        {
            return this.Start.Equals(other.Start) && this.End.Equals(other.End);
        }

        public override int GetHashCode()
        {
            int startHashCode = this.Start.GetHashCode();
            int endHashCode = this.End.GetHashCode();
            return startHashCode ^ endHashCode;
        }
    }
}
