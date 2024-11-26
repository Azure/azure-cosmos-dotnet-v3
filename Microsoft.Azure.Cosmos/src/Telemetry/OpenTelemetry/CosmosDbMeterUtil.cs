// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;

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

        internal static double? CalculateLatency(
          TimeSpan? start,
          TimeSpan? end,
          TimeSpan? failed)
        {
            TimeSpan? requestend = end ?? failed;
            return start.HasValue && requestend.HasValue ? (requestend.Value - start.Value).TotalSeconds : (double?)null;
        }

        internal static bool TryGetDiagnostics(OpenTelemetryAttributes attributes,
          Exception ex,
          out CosmosTraceDiagnostics traces)
        {
            traces = null;

            // Retrieve diagnostics from the exception if applicable
            CosmosDiagnostics diagnostics = ex switch
            {
                CosmosOperationCanceledException cancelEx => cancelEx.Diagnostics,
                CosmosObjectDisposedException disposedEx => disposedEx.Diagnostics,
                CosmosNullReferenceException nullRefEx => nullRefEx.Diagnostics,
                CosmosException cosmosException => cosmosException.Diagnostics,
                _ when attributes != null => attributes.Diagnostics,
                _ => null
            };

            // Ensure diagnostics is not null and cast is valid
            if (diagnostics is CosmosTraceDiagnostics traceDiagnostics)
            {
                traces = traceDiagnostics;
                return true;
            }

            return false;
        }
    }
}
