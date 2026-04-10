// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;

    internal sealed class PartitionKeyHashRangeDictionary<T>
    {
        private readonly SortedDictionary<PartitionKeyHashRange, (bool, T)> dictionary;

        public PartitionKeyHashRangeDictionary(PartitionKeyHashRanges partitionKeyHashRanges)
        {
            if (partitionKeyHashRanges == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyHashRanges));
            }

            this.dictionary = new SortedDictionary<PartitionKeyHashRange, (bool, T)>();
            foreach (PartitionKeyHashRange partitionKeyHashRange in partitionKeyHashRanges)
            {
                this.dictionary.Add(partitionKeyHashRange, (false, default));
            }
        }

        public bool TryGetValue(PartitionKeyHash partitionKeyHash, out T value)
        {
            if (!this.TryGetContainingRange(partitionKeyHash, out PartitionKeyHashRange range))
            {
                value = default;
                return false;
            }

            return this.TryGetValue(range, out value);
        }

        public bool TryGetValue(PartitionKeyHashRange partitionKeyHashRange, out T value)
        {
            if (!this.dictionary.TryGetValue(partitionKeyHashRange, out (bool valueSet, T value) nullableValue))
            {
                value = default;
                return false;
            }

            if (!nullableValue.valueSet)
            {
                value = default;
                return false;
            }

            value = nullableValue.value;
            return true;
        }

        public T this[PartitionKeyHash key]
        {
            get
            {
                if (!this.TryGetValue(key, out T value))
                {
                    throw new KeyNotFoundException();
                }

                return value;
            }
            set
            {
                if (!this.TryGetContainingRange(key, out PartitionKeyHashRange range))
                {
                    throw new NotSupportedException("Dictionary does not support adding new elements.");
                }

                this.dictionary[range] = (true, value);
            }
        }

        public T this[PartitionKeyHashRange key]
        {
            get
            {
                if (!this.TryGetValue(key, out T value))
                {
                    throw new KeyNotFoundException();
                }

                return value;
            }
            set
            {
                if (!this.dictionary.TryGetValue(key, out _))
                {
                    throw new NotSupportedException("Dictionary does not support adding new elements.");
                }

                this.dictionary[key] = (true, value);
            }
        }

        private bool TryGetContainingRange(PartitionKeyHash partitionKeyHash, out PartitionKeyHashRange range)
        {
            foreach (PartitionKeyHashRange candidateRange in this.dictionary.Keys)
            {
                if (candidateRange.Contains(partitionKeyHash))
                {
                    range = candidateRange;
                    return true;
                }
            }

            range = default;
            return false;
        }
    }
}