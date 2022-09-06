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
            var min = histogram.RecordedValues().FirstOrDefault().ValueIteratedTo;
            return histogram.LowestEquivalentValue(min);
        }
    }
}
