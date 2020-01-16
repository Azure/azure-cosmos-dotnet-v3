// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    /// <summary>
    /// Extracts the aggregated <see cref="BackendMetrics"/> from a <see cref="CosmosDiagnostics"/>.
    /// </summary>
    internal sealed class BackendMetricsExtractor : CosmosDiagnosticsInternalVisitor<(bool, BackendMetrics)>
    {
        public static readonly BackendMetricsExtractor Singleton = new BackendMetricsExtractor();

        private BackendMetricsExtractor()
        {
            // Private default constructor.
        }

        public override (bool, BackendMetrics) Visit(CosmosDiagnosticsAggregate cosmosDiagnosticsAggregate)
        {
            BackendMetrics.Accumulator accumulator = default;
            foreach (CosmosDiagnosticsInternal singleCosmosDiagnostic in cosmosDiagnosticsAggregate)
            {
                (bool extracted, BackendMetrics extractedBackendMetrics) = singleCosmosDiagnostic.Accept(this);
                if (!extracted)
                {
                    return (false, default);
                }

                accumulator = accumulator.Accumulate(extractedBackendMetrics);
            }

            return (true, BackendMetrics.Accumulator.ToBackendMetrics(accumulator));
        }

        public override (bool, BackendMetrics) Visit(PointOperationStatistics pointOperationStatistics)
        {
            return (false, default);
        }

        public override (bool, BackendMetrics) Visit(QueryAggregateDiagnostics queryAggregateDiagnostics)
        {
            BackendMetrics.Accumulator accumulator = default;
            foreach (QueryPageDiagnostics queryPageDiagnostics in queryAggregateDiagnostics.Pages)
            {
                if (!BackendMetrics.TryParseFromDelimitedString(queryPageDiagnostics.QueryMetricText, out BackendMetrics parsedBackendMetrics))
                {
                    return (false, default);
                }

                accumulator = accumulator.Accumulate(parsedBackendMetrics);
            }

            return (true, BackendMetrics.Accumulator.ToBackendMetrics(accumulator));
        }
    }
}
