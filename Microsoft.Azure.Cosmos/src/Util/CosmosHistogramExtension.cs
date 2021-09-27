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

        public static void RecordValues(this HistogramBase histogram, IList<long> values)
        {
            if (values == null)
            {
                return;
            }

            foreach (long value in values)
            {
                histogram.RecordValue(value);
            }
        }

        public static void ResetAndRecordValues(this HistogramBase histogram, IList<long> values)
        {
            histogram.Reset();

            if (values == null)
            {
                return;
            }

            foreach (long value in values)
            {
                histogram.RecordValue(value);
            }
        }
    }
}
