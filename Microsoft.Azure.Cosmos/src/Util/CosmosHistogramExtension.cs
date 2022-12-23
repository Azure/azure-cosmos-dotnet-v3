//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using HdrHistogram;

    internal static class CosmosHistogramExtension
    {
        public static long GetMinValue(this HistogramBase histogram)
        {
            long? min = histogram.RecordedValues().FirstOrDefault()?.ValueIteratedTo;
            if (min.HasValue)
            {
                return histogram.LowestEquivalentValue(min.Value);
            }
            return 0;
        }
    }
}
