// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;

    internal sealed class PartitionKeyRangeComparer : IComparer<PartitionKeyRange>
    {
        public static readonly PartitionKeyRangeComparer Singleton = new PartitionKeyRangeComparer();

        private PartitionKeyRangeComparer()
        {
        }

        public int Compare(PartitionKeyRange x, PartitionKeyRange y)
        {
            if (x == null)
            {
                throw new ArgumentNullException(nameof(x));
            }

            if (y == null)
            {
                throw new ArgumentNullException(nameof(y));
            }

            if (x.MinInclusive.Length == 0)
            {
                return -1;
            }

            if (y.MinInclusive.Length == 0)
            {
                return 1;
            }

            return x.MinInclusive.CompareTo(y.MinInclusive);
        }
    }
}
