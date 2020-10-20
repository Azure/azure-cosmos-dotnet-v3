// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel
{
    using System;
    using System.Collections.Generic;

    internal sealed class EpkRangeComparer : IComparer<FeedRangeEpk>
    {
        public static readonly EpkRangeComparer Singleton = new EpkRangeComparer();

        private EpkRangeComparer()
        {
        }

        public int Compare(FeedRangeEpk x, FeedRangeEpk y)
        {
            if (x == null)
            {
                throw new ArgumentNullException(nameof(x));
            }

            if (y == null)
            {
                throw new ArgumentNullException(nameof(y));
            }

            if (x.Range.Min.Length == 0)
            {
                return -1;
            }

            if (y.Range.Min.Length == 0)
            {
                return 1;
            }

            return x.Range.Min.CompareTo(y.Range.Min);
        }
    }
}
