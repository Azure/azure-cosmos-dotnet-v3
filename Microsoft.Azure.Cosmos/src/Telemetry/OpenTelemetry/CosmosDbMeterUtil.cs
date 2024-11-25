// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal static class CosmosDbMeterUtil
    {
        internal static void RecordHistogramMetric<T>(
            object value,
            Func<KeyValuePair<string, object>[]> dimensionsFunc,
            Histogram<T> histogram,
            Func<object, T> converter = null)
            where T : struct
        {
            if (histogram == null || !histogram.Enabled || value == null)
            {
                return;
            }

            try
            {
                T convertedValue = converter != null ? converter(value) : (T)value;
                histogram.Record(convertedValue, dimensionsFunc());
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning($"Failed to record metric. {ex.StackTrace}");
            }
        }

    }
}
